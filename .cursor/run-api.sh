#!/usr/bin/env bash
# Runs the OpenQuiz ASP.NET Core API against the local SQL Server.
# EF Core migrations are applied automatically on startup (see Program.cs).
set -euo pipefail

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

SA_PASSWORD="${MSSQL_SA_PASSWORD:-OpenQuiz_Dev_P@ss2026}"

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export ConnectionStrings__Default="Server=localhost,1433;Database=OpenQuiz;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;Encrypt=False"
export App__AdminEmail="${ADMIN_EMAIL:-admin@openquiz.local}"
export Cors__AllowedOrigins="${CORS_ALLOWED_ORIGINS:-http://localhost:5173,http://127.0.0.1:5173,http://localhost:8080}"

# JWT signing key. Token issuance requires a non-empty key, so we always
# provide one. Prefer an explicitly supplied key (e.g. a JWT_SIGNING_KEY
# secret); otherwise generate a stable local dev key once and reuse it so
# tokens stay valid across API restarts. The key file lives outside the repo
# so no secret is committed.
if [ -z "${JWT_SIGNING_KEY:-}" ]; then
  KEY_FILE="${OPENQUIZ_JWT_KEY_FILE:-$HOME/.openquiz/jwt-dev-key}"
  if [ ! -s "$KEY_FILE" ]; then
    mkdir -p "$(dirname "$KEY_FILE")"
    openssl rand -base64 64 | tr -d '\n' > "$KEY_FILE"
    chmod 600 "$KEY_FILE"
  fi
  JWT_SIGNING_KEY="$(cat "$KEY_FILE")"
fi
export Jwt__SigningKey="$JWT_SIGNING_KEY"

cd "$ROOT/backend"
exec dotnet run --project src/OpenQuiz.Api --no-launch-profile --urls "http://0.0.0.0:5142"
