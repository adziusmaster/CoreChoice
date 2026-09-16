# Deploying the CoreChoice backend

The backend runs as one container on the Hetzner VPS, behind the Caddy that already
serves Coldstart and PurePrep. Caddy handles TLS; this stack only runs the app.

## The host

`coldstart-prod` (in `~/.ssh/config`) — Hetzner, **167.233.145.128**, 4 GB. It already runs
Coldstart, Umami and PurePrep behind one Caddy. CoreChoice lives at `/opt/corechoice` and joins
the shared `coldstart_default` Docker network so that Caddy can reach it.

DNS: `api.corechoice.lechdigital.nl` → A → `167.233.145.128`.

## First deployment — as actually performed

There is no git remote, so the source is rsynced, exactly as PurePrep does it:

```sh
ssh coldstart-prod 'mkdir -p /opt/corechoice/src /opt/corechoice/deploy'
rsync -az --delete --exclude 'bin/' --exclude 'obj/' --exclude '.git/' \
  src/CoreChoice.Core src/CoreChoice.Server coldstart-prod:/opt/corechoice/src/
rsync -az Dockerfile NuGet.config coldstart-prod:/opt/corechoice/
rsync -az deploy/docker-compose.prod.yml coldstart-prod:/opt/corechoice/deploy/
```

Write `/opt/corechoice/.env` on the server. Generate the salt there and pipe the Gemini key over
stdin so neither value is ever echoed into a terminal, a log or a shell history:

```sh
ssh coldstart-prod 'umask 077
IFS= read -r KEY
SALT=$(openssl rand -hex 32)
cat > /opt/corechoice/.env <<EOF
GEMINI_API_KEY=$KEY
GEMINI_MODEL=gemini-flash-lite-latest
DEV_SECRET=
IP_HASH_SALT=$SALT
FIRST_CONTACT_GRANT=5
PROFILE_COMPLETION_GRANT=5
EOF
chmod 600 /opt/corechoice/.env' < ~/keys/corechoice-gemini.key
```

`IP_HASH_SALT` must stay stable: changing it resets the free-coin origin cap. It is generated once,
on the server, and never leaves it.

```sh
ssh coldstart-prod 'cd /opt/corechoice && \
  docker compose --env-file .env -f deploy/docker-compose.prod.yml up -d --build'
```

## Caddy

Caddy is `coldstart-caddy-1`, config at `/opt/coldstart/Caddyfile` on the host. Append:

```
api.corechoice.lechdigital.nl {
	reverse_proxy corechoice-api:8080
}
```

**The upstream is the CONTAINER name (`corechoice-api`), never the compose service name
(`corechoice`).** Compose publishes the service name as a DNS alias on the shared network, so a
service-name upstream can resolve to a neighbouring stack's container. That exact mistake once made
Coldstart's site serve PurePrep's API responses, and the Caddyfile carries a comment about it.

Back up, validate, then reload — never restart, since this Caddy is the only ingress for two live
sites:

```sh
ssh coldstart-prod 'cp /opt/coldstart/Caddyfile /opt/coldstart/Caddyfile.bak-$(date +%F)
docker exec coldstart-caddy-1 caddy validate --config /etc/caddy/Caddyfile &&
docker exec coldstart-caddy-1 caddy reload --config /etc/caddy/Caddyfile'
```

Then confirm the neighbours are untouched:

```sh
for u in https://getcoldstart.nl https://api.pureprep.lechdigital.nl/health https://pureprep.lechdigital.nl; do
  curl -s -o /dev/null -w "$u %{http_code}\n" -m 15 "$u"
done
```

Caddy cannot issue the certificate until the A record exists; until then it retries in the
background and the other sites are unaffected.

## Verify

```sh
curl -s https://api.corechoice.lechdigital.nl/health
curl -s https://api.corechoice.lechdigital.nl/api/personas
docker compose -f deploy/docker-compose.prod.yml logs --tail 50 corechoice
```

A healthy first boot shows standard ASP.NET Core hosting lines and no errors (e.g., `Now listening on: http://+:8080`,
`Application started...`). `/api/personas` returning six entries confirms the seed ran against the volume.

## Redeploy

```sh
cd /opt/corechoice && git pull
docker compose --env-file .env -f deploy/docker-compose.prod.yml up -d --build
```

The volume survives. Schema changes made after this first deploy must be added to
`SchemaInitializer` as explicit idempotent statements — `EnsureCreated` will not
alter a table that already exists, so a new column added only to the entity class
will build fine, deploy fine, and then fail at runtime on the live database.

## Changing a prompt without a release

```sh
docker compose -f deploy/docker-compose.prod.yml exec corechoice \
  sqlite3 /data/corechoice.server.db \
  "UPDATE PromptTemplates SET IsActive = 0 WHERE PersonaId = 'pure-logic';
   INSERT INTO PromptTemplates (PersonaId, Version, Template, IsActive, CreatedAt)
   VALUES ('pure-logic', 2, '<new template>', 1, strftime('%s','now') * 10000000 + 621355968000000000);"
```

`UsageLog.PromptVersion` then records which version answered each request, so the
effect of the change is measurable rather than a matter of impression.

## Rollback

```sh
cd /opt/corechoice && git checkout <previous-sha>
docker compose --env-file .env -f deploy/docker-compose.prod.yml up -d --build
```

Roll back code freely; the database is additive and older code ignores columns it
does not know about.
