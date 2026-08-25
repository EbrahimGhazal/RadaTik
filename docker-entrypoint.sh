#!/bin/bash
# Wait until SQL Server accepts TCP connections, then start the app.
set -euo pipefail

host="${SQL_SERVER_HOST:-radatik-sqlserver}"
port="${SQL_SERVER_PORT:-1433}"
keys_dir="${RADATIK_DATA_PROTECTION_KEYS_PATH:-/var/radatik/dp-keys}"

mkdir -p "${keys_dir}"
chmod 700 "${keys_dir}" || true

echo "Waiting for SQL Server at ${host}:${port}..."
for i in $(seq 1 90); do
  if bash -c "echo >/dev/tcp/${host}/${port}" 2>/dev/null; then
    echo "SQL Server is reachable."
    exec dotnet RadaTik.dll "$@"
  fi
  echo "Attempt ${i}/90: SQL not ready yet, sleeping 2s..."
  sleep 2
done

echo "Timed out waiting for SQL Server at ${host}:${port}" >&2
exit 1
