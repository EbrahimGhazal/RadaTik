import os
import sys
import time
import paramiko

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")

HOST = "186.240.159.216"
PASSWORD = os.environ["RADATIK_SSH_PASS"]


def main():
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(
        HOST,
        username="root",
        password=PASSWORD,
        timeout=30,
        allow_agent=False,
        look_for_keys=False,
    )

    def run(cmd, timeout=900):
        print(f"\n$ {cmd}")
        stdin, stdout, stderr = client.exec_command(cmd, get_pty=True, timeout=timeout)
        while True:
            line = stdout.readline()
            if not line:
                break
            print(line, end="")
        code = stdout.channel.recv_exit_status()
        err = stderr.read().decode("utf-8", errors="replace")
        if err:
            print(err, end="")
        print(f"exit={code}")
        if code != 0:
            raise SystemExit(code)

    run("cd /opt/radatik && git status -sb && git rev-parse --short HEAD")
    run("cd /opt/radatik && git fetch origin main && git reset --hard origin/main")
    run("cd /opt/radatik && chmod +x docker-entrypoint.sh && git rev-parse --short HEAD && git log -3 --oneline")
    run(
        "cd /opt/radatik && date -Is > RadaTik/.deploy-stamp && "
        "docker compose up -d --build --no-deps --force-recreate app"
    )

    ok = False
    for i in range(36):
        stdin, stdout, stderr = client.exec_command(
            'curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:8080/'
        )
        code = stdout.read().decode("utf-8", "replace").strip()
        print(f"health {i + 1}: {code}")
        if code in ("200", "301", "302", "303"):
            ok = True
            break
        time.sleep(5)

    stdin, stdout, stderr = client.exec_command(
        "docker ps --filter name=radatik-app --format '{{.Names}} {{.Status}}'"
    )
    print(stdout.read().decode("utf-8", "replace"), end="")
    stdin, stdout, stderr = client.exec_command(
        "test -f /opt/radatik/RadaTik/wwwroot/css/sector-index-mobile.css && echo sector-css=yes; "
        "grep -n 'neutralizeCardTableWidths\\|display: block !important' "
        "/opt/radatik/RadaTik/wwwroot/js/radtk-ui-kit.js "
        "/opt/radatik/RadaTik/wwwroot/css/radtk-ui-kit.css | head -n 15"
    )
    print(stdout.read().decode("utf-8", "replace"), end="")
    client.close()
    if not ok:
        raise SystemExit(1)
    print("DEPLOY_OK")


if __name__ == "__main__":
    main()
