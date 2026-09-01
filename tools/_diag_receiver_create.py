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

# Reproduce hit and grab recent error logs
cmds = [
    # trigger page without auth may redirect; just pull error logs
    "docker logs --tail 800 radatik-app 2>&1 | grep -E 'fail:|Exception|Unhandled|Receiver/Create|NullReference|InvalidCast|RuntimeBinder|RZ|does not contain' | tail -n 80",
    "ls -lt /opt/radatik/RadaTik/Logs 2>/dev/null | head -5; ls -lt /var/log 2>/dev/null | head -5",
    "find /opt/radatik -name '*.log' 2>/dev/null | head -20",
]

for cmd in cmds:
    print(f"\n===== {cmd} =====")
    stdin, stdout, stderr = client.exec_command(cmd, timeout=60)
    out = stdout.read().decode("utf-8", "replace")
    err = stderr.read().decode("utf-8", "replace")
    print(out[-15000:] if len(out) > 15000 else out)
    if err:
        print(err[-2000:])

client.close()
