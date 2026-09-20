#!/usr/bin/env bash
#
# Provisions the Ukweli host from a bare Ubuntu 24.04 EC2 instance.
#
# Run once, on the instance, as the default `ubuntu` user. It is idempotent:
# running it twice changes nothing the second time, which matters because the
# most likely reason to run it again is that something failed halfway.
set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/emmanueluko/ukweli.git}"
APP_DIR="${APP_DIR:-/opt/ukweli}"

log() { printf '\n\033[1;32m==>\033[0m %s\n' "$1"; }

log "Installing Docker"
if ! command -v docker >/dev/null 2>&1; then
  sudo apt-get update -qq
  sudo apt-get install -y -qq ca-certificates curl git
  sudo install -m 0755 -d /etc/apt/keyrings
  sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
    -o /etc/apt/keyrings/docker.asc
  sudo chmod a+r /etc/apt/keyrings/docker.asc
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
    | sudo tee /etc/apt/sources.list.d/docker.list >/dev/null
  sudo apt-get update -qq
  sudo apt-get install -y -qq docker-ce docker-ce-cli containerd.io \
    docker-buildx-plugin docker-compose-plugin
  sudo usermod -aG docker "$USER"
else
  echo "Docker already present: $(docker --version)"
fi

log "Fetching the application"
sudo mkdir -p "$APP_DIR"
sudo chown "$USER":"$USER" "$APP_DIR"

# Initialised in place rather than cloned. The setup order in deploy/README.md
# has .env written to this directory first, so it is never empty by the time we
# get here — and `git clone` refuses a non-empty target. Initialising keeps the
# untracked .env exactly where it is.
if [ ! -d "$APP_DIR/.git" ]; then
  git init --quiet --initial-branch=main "$APP_DIR"
  git -C "$APP_DIR" remote add origin "$REPO_URL"
fi

git -C "$APP_DIR" remote set-url origin "$REPO_URL"
git -C "$APP_DIR" fetch --quiet origin main
git -C "$APP_DIR" reset --hard --quiet origin/main

log "Checking configuration"
if [ ! -f "$APP_DIR/.env" ]; then
  cat <<'WARNING'

  No .env on the host yet.

  Secrets are deliberately NOT baked into this script or the repository: an
  API key in a git history is a key that has to be rotated. Write /opt/ukweli/.env
  on the host (deploy/env.production.example lists every variable), then re-run
  this script.

WARNING
  exit 1
fi

log "Building and starting the stack"
cd "$APP_DIR"
sg docker -c "docker compose up -d --build" 2>/dev/null || docker compose up -d --build

log "Waiting for the API to report healthy"
for attempt in $(seq 1 60); do
  if curl -fsS http://localhost:8787/healthz >/dev/null 2>&1; then
    echo "API is healthy."
    break
  fi
  if [ "$attempt" -eq 60 ]; then
    echo "The API did not become healthy. Recent logs:" >&2
    docker compose logs --tail 60 api >&2
    exit 1
  fi
  sleep 3
done

log "Seeding the curated evidence store"
docker compose exec -T api dotnet /app/cli/Ukweli.Cli.dll seed || {
  echo "Seeding failed. The API is up, but it has no sources to check against." >&2
  exit 1
}

log "Done"
docker compose ps
