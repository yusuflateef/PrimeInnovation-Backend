#!/usr/bin/env bash
# Smoke-tests the API and migrator images against a throwaway SQL Server container:
#   migrator applies migrations -> migrator is idempotent -> API starts with startup migrations off
#   -> /health is Healthy -> a public endpoint answers.
# Usage: scripts/smoke-test.sh <api-image> <migrator-image>
# Used by CI; also runs locally (Docker required).
set -euo pipefail

API_IMAGE="${1:?api image required}"
MIGRATOR_IMAGE="${2:?migrator image required}"
SQL_IMAGE="${SQL_IMAGE:-mcr.microsoft.com/mssql/server:2022-latest}"
PORT="${SMOKE_PORT:-18080}"

SUFFIX="$RANDOM"
NET="promix-smoke-$SUFFIX"
SQL="promix-smoke-sql-$SUFFIX"
API="promix-smoke-api-$SUFFIX"
SA_PASSWORD="Smoke-$(openssl rand -hex 8)-Aa1"
JWT_KEY="$(openssl rand -base64 48)"
CS="Server=$SQL,1433;Database=PrimeInnovationDb;User Id=sa;Password=$SA_PASSWORD;TrustServerCertificate=True"

fail() {
  echo "SMOKE TEST FAILED: $*" >&2
  echo "--- api logs ---" >&2; docker logs "$API" 2>&1 | tail -50 >&2 || true
  exit 1
}
cleanup() {
  docker rm -f "$API" "$SQL" >/dev/null 2>&1 || true
  docker network rm "$NET" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker network create "$NET" >/dev/null
docker run -d --name "$SQL" --network "$NET" \
  -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=$SA_PASSWORD" "$SQL_IMAGE" >/dev/null

migrate() { docker run --rm --network "$NET" -e "ConnectionStrings__Default=$CS" "$MIGRATOR_IMAGE" 2>&1; }

echo "==> Applying migrations (retrying while SQL Server starts up)"
migrated=false
for attempt in $(seq 1 30); do
  if output="$(migrate)"; then
    migrated=true; break
  fi
  echo "    SQL Server not ready yet (attempt $attempt), retrying in 5s..."
  sleep 5
done
$migrated || { echo "$output" | tail -5 >&2; fail "migrator never succeeded"; }
echo "$output" | grep -E "Applying migration|Done" | sed 's/^/    /'

echo "==> Re-running migrator (must be a no-op)"
output="$(migrate)" || { echo "$output" | tail -5 >&2; fail "second migrator run errored"; }
case "$output" in
  *"already up to date"*) echo "    already up to date" ;;
  *) echo "$output" | tail -5 >&2; fail "second migrator run was not a no-op" ;;
esac

echo "==> Starting API (Production, startup migrations off)"
docker run -d --name "$API" --network "$NET" -p "127.0.0.1:$PORT:8080" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e "ConnectionStrings__Default=$CS" \
  -e "Jwt__Key=$JWT_KEY" \
  -e Database__MigrateOnStartup=false \
  "$API_IMAGE" >/dev/null

healthy=false
for _ in $(seq 1 30); do
  if [ "$(curl -s "http://127.0.0.1:$PORT/health" || true)" = "Healthy" ]; then healthy=true; break; fi
  sleep 2
done
$healthy || fail "/health never reported Healthy"
echo "    /health: Healthy"

code="$(curl -s -w '\n%{http_code}' "http://127.0.0.1:$PORT/api/courses/categories" | tail -n1)"
[ "$code" = "200" ] || fail "/api/courses/categories returned $code"
echo "    /api/courses/categories: 200"

[ "$(docker exec "$API" id -un)" = "app" ] || fail "API container is not running as the non-root 'app' user"
echo "    runs as non-root user 'app'"

echo "SMOKE TEST PASSED"
