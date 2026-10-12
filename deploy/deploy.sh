#!/usr/bin/env bash
# Deploys a backend image tag on the server and rolls back the API if it fails its health check.
#   Usage: ./deploy.sh <image-tag>        e.g. ./deploy.sh 007ff06...   or   ./deploy.sh latest
# Steps: pull -> run migrations -> restart API -> wait for /health -> record tag (or roll back).
# NOTE: rollback restores the previous API image only. Migrations are not reverted, so every
# migration must stay compatible with the previously deployed code.
set -euo pipefail
cd "$(dirname "$0")"

TAG="${1:?usage: ./deploy.sh <image-tag>}"
STATE_FILE=".deployed-tag"
HEALTH_URL="http://127.0.0.1:8080/health"
PREVIOUS="$(cat "$STATE_FILE" 2>/dev/null || true)"

log() { echo "[deploy $(date -u +%H:%M:%S)] $*"; }

[ -f .env ] || { log "missing .env next to docker-compose.yml"; exit 1; }

export IMAGE_TAG="$TAG"
log "deploying tag $TAG (previous: ${PREVIOUS:-none})"

log "pulling images"
docker compose --profile tools pull api migrator

log "applying database migrations"
docker compose --profile tools run --rm migrator

log "restarting API"
docker compose up -d api

log "waiting for $HEALTH_URL"
for _ in $(seq 1 30); do
  if [ "$(curl -fs "$HEALTH_URL" 2>/dev/null || true)" = "Healthy" ]; then
    echo "$TAG" > "$STATE_FILE"
    log "healthy - deployed $TAG"
    docker image prune -f >/dev/null || true
    exit 0
  fi
  sleep 2
done

log "health check FAILED for $TAG"
docker compose logs --tail 80 api || true

if [ -n "$PREVIOUS" ] && [ "$PREVIOUS" != "$TAG" ]; then
  log "rolling back API to $PREVIOUS"
  IMAGE_TAG="$PREVIOUS" docker compose up -d api
fi
exit 1
