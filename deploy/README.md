# Deploying Ukweli

One EC2 instance running the same `docker-compose.yml` used locally: Postgres,
the API, the web bundle, and Caddy terminating TLS. Pushes to `main` redeploy
automatically once CI is green on that commit.

## Read this first

**Postgres runs as a container on the application host, with its data in a
Docker volume.** That is a deliberate choice for a hackathon demo and the wrong
one for anything with real users: terminating the instance destroys the
database, there are no backups, and there is no failover. Before this carries
anything that matters, move it to RDS — the only change is `DATABASE_URL`.

## One-time setup

### 1. The instance

- Ubuntu 24.04, `t4g.small` (ARM) or `t3.small`
- An **Elastic IP**, so a stop/start does not change the address your DNS points at
- Security group: inbound `22` from your address only, `80` and `443` from anywhere
- 20 GB of disk

### 2. DNS

Point an `A` record for your hostname at the Elastic IP **before** first boot.
Caddy asks Let's Encrypt for a certificate on startup, and that fails if the
name does not yet resolve to this machine.

### 3. Configuration

```sh
ssh ubuntu@<elastic-ip>
sudo mkdir -p /opt/ukweli && sudo chown ubuntu:ubuntu /opt/ukweli
# paste deploy/env.production.example, filled in, to /opt/ukweli/.env
chmod 600 /opt/ukweli/.env
```

### 4. Provision

```sh
curl -fsSL https://raw.githubusercontent.com/emmanueluko/ukweli/main/deploy/provision.sh | bash
```

Installs Docker, clones the repository, builds and starts the stack, waits for
`/healthz`, and seeds the curated sources. It is idempotent — safe to re-run,
which is what you want when something failed halfway.

### 5. Continuous deployment

Generate a key **for deployment only**, not a personal one:

```sh
ssh-keygen -t ed25519 -f ~/.ssh/ukweli_deploy -N '' -C 'ukweli-deploy'
ssh-copy-id -i ~/.ssh/ukweli_deploy.pub ubuntu@<elastic-ip>
ssh-keyscan -H <elastic-ip>          # for DEPLOY_KNOWN_HOSTS
```

Then set these repository secrets:

| Secret | Value |
| --- | --- |
| `DEPLOY_HOST` | the Elastic IP |
| `DEPLOY_USER` | `ubuntu` |
| `DEPLOY_SSH_KEY` | the **private** key, whole file including header and footer |
| `DEPLOY_KNOWN_HOSTS` | the `ssh-keyscan` output |
| `DEPLOY_APP_URL` | `https://your-hostname` |

## How a deploy runs

A push to `main` triggers CI. Only if CI passes on that exact commit does the
deploy workflow run: it checks out the commit CI approved, SSHes in, resets the
host to it, rebuilds, applies migrations on boot, reseeds the sources, then
polls `/healthz` from the public URL.

That last check requires `db: true`, not merely a 200 — a reachable API sitting
on an unreachable database is a broken deployment wearing a green tick. If it
fails, the job prints `docker compose ps` and the API logs.

Deploys are serialised, so two pushes in quick succession cannot interleave.

## Operating it

```sh
cd /opt/ukweli
docker compose ps
docker compose logs -f api
docker compose restart api
docker compose exec -T api dotnet /app/cli/Ukweli.Cli.dll verify-sources
```

### Adding curated sources

Sources ship in the repository, so curation is an ordinary pull request: edit
`src/Ukweli.Evidence/data/sources.json`, run `make verify-sources` locally, open
the PR. Merging it redeploys and reseeds.

### Rolling back

```sh
cd /opt/ukweli
git reset --hard <previous-sha>
docker compose up -d --build
```

A migration that has already run is not undone by this. Check
`src/Ukweli.Data/Migrations` before rolling back across one.

### Backups (not configured)

There are none. For the demo that is acceptable; the corpus is in git and the
analyses are reproducible. If that stops being true:

```sh
docker compose exec -T postgres pg_dump -U ukweli ukweli | gzip > ukweli-$(date +%F).sql.gz
```
