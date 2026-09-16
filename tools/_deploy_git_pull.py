import os
import sys
import time
from pathlib import Path

import paramiko

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
sys.stderr.reconfigure(encoding="utf-8", errors="replace")

HOST = "186.240.159.216"
USER = "root"
KEY_PATH = Path.home() / ".ssh" / "radatik_deploy"


def connect():
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())

    if KEY_PATH.exists():
        try:
            client.connect(
                HOST,
                username=USER,
                key_filename=str(KEY_PATH),
                timeout=30,
                allow_agent=False,
                look_for_keys=False,
            )
            print(f"auth=ssh-key ({KEY_PATH.name})")
            return client
        except Exception as ex:
            print(f"ssh-key auth failed: {ex}")

    password = os.environ.get("RADATIK_SSH_PASS")
    if not password:
        raise SystemExit(
            "No SSH key auth and RADATIK_SSH_PASS is unset. "
            f"Install key at {KEY_PATH} or set RADATIK_SSH_PASS once."
        )

    client.connect(
        HOST,
        username=USER,
        password=password,
        timeout=30,
        allow_agent=False,
        look_for_keys=False,
    )
    print("auth=password (fallback)")
    return client


def main():
    client = connect()

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
