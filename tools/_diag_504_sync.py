import os
import sys
import paramiko

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect(
    "186.240.159.216",
    username="root",
    password=os.environ["RADATIK_SSH_PASS"],
    timeout=30,
    allow_agent=False,
    look_for_keys=False,
)

cmds = [
    "docker logs --since 30m radatik-app 2>&1 | grep -E 'SyncWithMikroTik|504|timeout|MikroTik|Exception|fail:|Gateway|Clients' | tail -n 100",
    "docker ps --filter name=radatik --format '{{.Names}} {{.Status}}'",
    "docker stats --no-stream --format '{{.Name}} CPU={{.CPUPerc}} MEM={{.MemUsage}}' $(docker ps -q --filter name=radatik)",
]

for cmd in cmds:
    print(f"\n===== {cmd} =====")
    stdin, stdout, stderr = client.exec_command(cmd, timeout=90)
    out = stdout.read().decode("utf-8", "replace")
    err = stderr.read().decode("utf-8", "replace")
    print(out[-20000:] if len(out) > 20000 else out)
    if err.strip():
        print(err[-3000:])

client.close()
