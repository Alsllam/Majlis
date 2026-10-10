#!/usr/bin/env bash
# Runs the backend locally against the compose infrastructure:
#   1. reads ../.env, 2. applies migrations + seed, 3. starts Auth, Rooms, Realtime, the BFF and ai-service.
# Logs go to backend/.local/logs. Stop with: backend/scripts/stop-local.sh
set -euo pipefail
cd "$(dirname "$0")/.."
ROOT="$(cd .. && pwd)"
[ -f "$ROOT/.env" ] || { echo "Missing $ROOT/.env — copy .env.example and fill it in."; exit 1; }
set -a; source "$ROOT/.env"; set +a

export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__Default="Server=localhost,1433;Database=Majlis;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True"
export ConnectionStrings__Redis="localhost:6379"
export RabbitMq__Username="${RABBITMQ_USER:-majlis}" RabbitMq__Password="${RABBITMQ_PASSWORD}"
export Seed__Password="${MAJLIS_SEED_PASSWORD:-}" Seed__RealtimeClientSecret="${MAJLIS_REALTIME_CLIENT_SECRET:-}"
export InternalAuth__ClientSecret="${MAJLIS_REALTIME_CLIENT_SECRET:-}"

mkdir -p .local/logs
dotnet build Majlis.sln -v q -nologo
dotnet run --no-build --project Shared/Majlis.DbMigrator

for host in Auth Workspaces Rooms Realtime BFF; do
  dotnet run --no-build --project "Hosts/Majlis.$host.Host" > ".local/logs/$host.log" 2>&1 &
  echo $! > ".local/$host.pid"
  echo "Started $host (pid $!) — log: backend/.local/logs/$host.log"
done
# ai-service (needs uv); the model is reached only when AZURE_OPENAI_* are set.
if command -v uv >/dev/null; then
  (cd ../ai-service && RABBITMQ_URL="amqp://${RABBITMQ_USER:-majlis}:${RABBITMQ_PASSWORD}@localhost:5672/majlis" \
    uv run uvicorn ai_service.main:create_app --factory --port 8000 > ../backend/.local/logs/ai-service.log 2>&1 &
   echo $! > ../backend/.local/ai-service.pid)
  echo "Started ai-service — log: backend/.local/logs/ai-service.log"
fi
echo "Gateway: http://localhost:7000  ·  Swagger: http://localhost:7020/swagger, http://localhost:7001/swagger"
