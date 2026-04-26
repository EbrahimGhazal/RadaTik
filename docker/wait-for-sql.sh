#!/bin/bash
# Wait until SQL Server accepts TCP connections, then start the app.
set -euo pipefail

host="${SQL_SERVER_HOST:-radtik-sqlserver}"
port="${SQL_SERVER_PORT:-1433}"

echo "Waiting for SQL Server at ${host}:${port}..."
for i in $(seq 1 90); do
  if bash -c "echo >/dev/tcp/${host}/${port}" 2>/dev/null; then
    echo "SQL Server is reachable."
    exec dotnet RadTik.dll "$@"
  fi
  echo "Attempt ${i}/90: SQL not ready yet, sleeping 2s..."
  sleep 2
done

echo "Timed out waiting for SQL Server at ${host}:${port}" >&2
exit 1
