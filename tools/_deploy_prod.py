import os
import sys
import time
import paramiko

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")

HOST = "186.240.159.216"
ROOT = r"d:\SkyBeam\MyApp\RadTik\RadTik_20260225_Full_01"
PASSWORD = os.environ["RADATIK_SSH_PASS"]
REMOTE_ROOT = "/opt/radatik"
FILES = [
    "RadaTik/Controllers/RequestsManagementController.cs",
    "RadaTik/Views/Shared/_MaintenanceRequestDetailsBody.cshtml",
    "RadaTik/Areas/CompanyEmployee/Views/RequestsManagement/MaintenanceRequestDetails.cshtml",
    "RadaTik/Areas/CompanyAdmin/Views/RequestsManagement/MaintenanceRequestDetails.cshtml",
    "RadaTik/wwwroot/css/maintenance-request-details-cards.css",
    "RadaTik/wwwroot/js/maintenance-request-details.js",
    "RadaTik/wwwroot/js/form-once-submit.js",
    "RadaTik/Services/MikroTik/IMikroTikPppoeUserService.cs",
    "RadaTik/Services/MikroTik/MikroTikUsersFacade.cs",
    "RadaTik/Services/MikroTik/MikroTikApiSupport.cs",
    "RadaTik/Services/MikroTik/MikroTikUserService.cs",
    "RadaTik/Services/Clients/ClientListQueryService.cs",
    "RadaTik/Areas/CompanyEmployee/Views/Clients/Index.cshtml",
    "RadaTik/Areas/CompanyAdmin/Views/Clients/Index.cshtml",
    "RadaTik/Security/SensitiveDataProtector.cs",
    "RadaTik/Program.cs"
]


def main():
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(
        HOST,
        username="root",
        password=PASSWORD,
        timeout=60,
        banner_timeout=60,
        auth_timeout=60,
        allow_agent=False,
        look_for_keys=False,
    )
    sftp = client.open_sftp()
    for rel in FILES:
        local = os.path.join(ROOT, rel)
        if not os.path.isfile(local):
            raise FileNotFoundError(local)
        remote = REMOTE_ROOT + "/" + rel.replace("\\", "/")
        remote_dir = os.path.dirname(remote)
        try:
            sftp.stat(remote_dir)
        except FileNotFoundError:
            client.exec_command(f"mkdir -p {remote_dir}")
            time.sleep(0.2)
        print(f"upload {rel}")
        sftp.put(local, remote)
    sftp.close()

    cmd = (
        "cd /opt/radatik && date -Is > RadaTik/.deploy-stamp && "
        "docker compose up -d --build --no-deps --force-recreate app"
    )
    print(f"run: {cmd}")
    stdin, stdout, stderr = client.exec_command(cmd, timeout=480)
    while True:
        line = stdout.readline()
        if not line:
            break
        print(line, end="")
    exit_status = stdout.channel.recv_exit_status()
    err = stderr.read().decode("utf-8", errors="replace")
    if err:
        print(err, end="")
    print(f"exit={exit_status}")
    if exit_status != 0:
        sys.exit(exit_status)

    ok = False
    for i in range(60):
        stdin, stdout, stderr = client.exec_command(
            'curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:8080/'
        )
        code = stdout.read().decode("utf-8", "replace").strip()
        print(f"health {i + 1}: {code}")
        if code in ("200", "301", "302", "303"):
            ok = True
            break
        if i > 0 and i % 6 == 0:
            stdin, stdout, stderr = client.exec_command("tail -n 8 /tmp/radatik-rebuild.log")
            print(stdout.read().decode("utf-8", "replace"), end="")
        time.sleep(10)

    stdin, stdout, stderr = client.exec_command(
        "docker exec radatik-app sh -c '"
        "ls /app/wwwroot/css/maintenance-request-details-cards.css /app/wwwroot/js/maintenance-request-details.js; "
        "grep -n clients.employee.pageLength /app/Areas/CompanyEmployee/Views/Clients/Index.cshtml | head -n 3; "
        "grep -n GetActivePppSessionNamesByServerAsync /app/Services/Clients/ClientListQueryService.cs | head -n 3; "
        "grep -n _MaintenanceRequestDetailsBody /app/Areas/CompanyEmployee/Views/RequestsManagement/MaintenanceRequestDetails.cshtml | head -n 3"
        "'"
    )
    print(stdout.read().decode("utf-8", "replace"), end="")
    stdin, stdout, stderr = client.exec_command(
        "docker ps --filter name=radatik-app --format '{{.Names}} {{.Status}}'"
    )
    print(stdout.read().decode("utf-8", "replace"), end="")
    client.close()
    if not ok:
        sys.exit(1)


if __name__ == "__main__":
    main()
