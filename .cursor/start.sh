#!/usr/bin/env bash
# Per-boot reconciliation: bring up SQL Server (no systemd in the VM) and
# wait until it accepts connections, then return so the app terminals can
# start against a ready database. Idempotent: a already-running instance is
# left untouched.
set -euo pipefail

SA_PASSWORD="${MSSQL_SA_PASSWORD:-OpenQuiz_Dev_P@ss2026}"

if pgrep -x sqlservr >/dev/null 2>&1; then
  echo "==> SQL Server already running"
else
  echo "==> Starting SQL Server (sqlservr)"
  sudo -u mssql env \
    MSSQL_SA_PASSWORD="$SA_PASSWORD" \
    ACCEPT_EULA=Y \
    setsid /opt/mssql/bin/sqlservr >/tmp/mssql.log 2>&1 < /dev/null &
fi

echo "==> Waiting for SQL Server to accept connections"
for i in $(seq 1 60); do
  if /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -Q "SELECT 1" >/dev/null 2>&1; then
    echo "==> SQL Server is ready"
    exit 0
  fi
  sleep 2
done

echo "!! SQL Server did not become ready in time; see /tmp/mssql.log" >&2
tail -n 40 /tmp/mssql.log >&2 || true
exit 1
