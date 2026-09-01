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

cmd = "docker logs --tail 200 radatik-app 2>&1"
stdin, stdout, stderr = client.exec_command(cmd, timeout=60)
print(stdout.read().decode("utf-8", "replace"))
err = stderr.read().decode("utf-8", "replace")
if err:
    print(err)
client.close()
