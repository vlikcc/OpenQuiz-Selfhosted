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

# In Development the API auto-generates a JWT signing key when none is set.
# Provide a stable key by exporting JWT_SIGNING_KEY (e.g. as a secret) to keep
# issued tokens valid across restarts.
if [ -n "${JWT_SIGNING_KEY:-}" ]; then
  export Jwt__SigningKey="$JWT_SIGNING_KEY"
fi

cd "$ROOT/backend"
exec dotnet run --project src/OpenQuiz.Api --no-launch-profile --urls "http://0.0.0.0:5142"
