# Deploying the CoreChoice backend

The backend runs as one container on the Hetzner VPS, behind the Caddy that already
serves Coldstart and PurePrep. Caddy handles TLS; this stack only runs the app.

## First deployment

```sh
ssh <hetzner-host>
sudo mkdir -p /opt/corechoice && cd /opt/corechoice
git clone <repo-url> .
cp deploy/.env.example .env
openssl rand -hex 32   # paste into IP_HASH_SALT
nano .env              # set GEMINI_API_KEY and IP_HASH_SALT
docker compose --env-file .env -f deploy/docker-compose.prod.yml up -d --build
```

Then add the hostname to Coldstart's Caddyfile:

```
api.corechoice.lechdigital.nl {
    reverse_proxy corechoice:8080
}
```

and reload Caddy. Point the DNS A record at the host first, or Caddy's certificate
request will fail and retry with a backoff.

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
