# Self-Hosting OpenQuiz

A complete OpenQuiz stack (SQL Server + .NET API + React web) starts from a single
`docker compose up`. Everything in this guide assumes Docker 24+ with the Compose plugin.

## 1. Prerequisites

- Docker Engine 24+ and `docker compose`
- A Google OAuth 2.0 **Web** client ID (only thing keeping a Google dependency — for Sign-In).
- (Optional) An SMTP relay for password reset e-mails.

## 2. Configure

```bash
cp .env.example .env
# edit .env: set MSSQL_SA_PASSWORD, JWT_SIGNING_KEY, GOOGLE_CLIENT_ID, ADMIN_EMAIL …
```

Generate a JWT key:

```bash
openssl rand -base64 64
```

### Google Sign-In setup

1. https://console.cloud.google.com/apis/credentials → **Create credentials** → **OAuth client ID** → **Web application**
2. **Authorized JavaScript origins**: add your `PUBLIC_URL` (e.g. `http://localhost:8080`)
3. No redirect URIs are required — the frontend uses the One Tap / ID token flow.
4. Copy the client ID to `GOOGLE_CLIENT_ID` in `.env`.

> **No other Google service is used.** No Firebase, no Firestore, no FCM, no Hosting.

## 3. Run

```bash
docker compose up -d --build
```

Three containers come up:

| Service | Default port | Notes |
|---|---|---|
| `db` (SQL Server 2022) | `1433` | data persisted in `mssql-data` volume |
| `api` (.NET 10 / Kestrel) | `5080` | applies EF migrations on boot, logs to `api-logs` volume |
| `web` (nginx + React build) | `8080` | SPA served from `/usr/share/nginx/html` |

Open the app at `http://localhost:8080`.

All three run as non-root (`app`, `nginx`, `mssql`), start with
`no-new-privileges`, restart `unless-stopped`, and carry a healthcheck. `web`
additionally runs with a read-only root filesystem. The `api` container waits
for `db` to report healthy, and `web` waits for `api`, so the first request
after `up` never lands on a half-started stack.

CPU and memory ceilings come from `.env` (`API_MEMORY_LIMIT`, `DB_CPU_LIMIT`,
…). SQL Server refuses to start below 2 GB, so keep `DB_MEMORY_LIMIT` above
that.

## 4. First admin

Set `ADMIN_EMAIL` in `.env` **before first login**. When that e-mail logs in (Google or email/password), the user is auto-elevated to admin and granted `CanCreate`.

After that, the admin can authorize other users via **Admin Paneli → Yetkili Kullanıcılar**.

## 5. Common operations

```bash
# follow logs
docker compose logs -f api
docker compose logs -f web

# rebuild after pulling new code
docker compose up -d --build

# wipe & restart (DESTRUCTIVE — drops all data)
docker compose down -v
```

### Health probes

| Endpoint | Answers | Use it for |
|---|---|---|
| `/health/live` | Is the process running? Never touches the database. | Restart / liveness probes |
| `/health/ready` | Can this instance serve traffic? Opens a database connection. | Load-balancer and startup gates |
| `/health` | Same as `/health/ready`. | Existing probes |

Keep liveness off the database. A probe that fails during a database outage
restarts every API instance in a loop, which turns a recoverable outage into a
total one.

### Logs

The API writes to stdout (`docker compose logs -f api`) *and* to a daily
rolling file under `/app/logs`, mounted as the `api-logs` volume so it survives
a container restart. Files roll at 50 MB and the last 14 are kept.

```bash
# read the current file
docker compose exec api sh -c 'tail -f /app/logs/openquiz-*.log'

# copy the whole history out
docker compose cp api:/app/logs ./api-logs
```

Health probes log at Debug so they do not drown the requests you are looking
for. Raise `Serilog__MinimumLevel__Default=Debug` to see them.

Question images are stored on disk at `/app/data/media` and mounted as the
`api-media` volume so they survive a container recreate. The API's
`SessionScheduler` hosted service mails a reminder 15 minutes before a
scheduled start (skipped when SMTP is not configured) and then activates the
session as the creator.

## 6. Rate limiting

Anonymous endpoints are rate limited per caller. Signed-in callers get a bucket keyed on
their user id, everyone else is bucketed by IP address. Defaults live under `RateLimiting`
in `appsettings.json` and can be overridden with environment variables:

| Policy | Endpoints | Default |
|---|---|---|
| `Auth` | register, login, refresh, Google sign-in, password reset | 10 / minute |
| `Participation` | poll fetch, join, votes, open answers, word cloud, aggregates | 240 / minute |
| `Reactions` | emoji reactions | 300 / minute |

```bash
RateLimiting__Participation__PermitLimit=480
RateLimiting__Participation__WindowSeconds=60
```

A whole room usually shares one NAT address, so the participation budget is per venue
rather than per person — raise it for large audiences.

Behind a reverse proxy, set `RateLimiting__TrustForwardedHeaders=true` so the API buckets
on `X-Forwarded-For` instead of the proxy address. **Only enable this when the API is
unreachable except through the proxy**, otherwise a client can forge the header and mint
itself a fresh quota. With the bundled Compose file that means removing the `5080` port
mapping from the `api` service first.

## 7. Running more than one API instance

SignalR keeps its groups in the memory of the process that owns the connection.
With a single `api` container that is fine. Add a second and the room splits:
a participant connected to instance A never hears an event published by
instance B, so their question never advances.

Point both instances at one Redis to fan the events out:

```bash
Realtime__RedisConnectionString=redis:6379
Realtime__ChannelPrefix=openquiz   # only matters if several deployments share a Redis
```

```yaml
  redis:
    image: redis:7-alpine
    command: ["redis-server", "--save", "", "--appendonly", "no"]
    restart: unless-stopped
```

Nothing is persisted in Redis — it carries live events only, so it can be
restarted and needs no volume. The API logs which mode it is in at startup:

```
SignalR is using the Redis backplane; this instance can be one of several.
SignalR is running without a backplane. Run a single API instance, or set Realtime:RedisConnectionString.
```

Vote-count and word-cloud broadcasts are coalesced per process, so N instances
can emit up to N batches per window instead of one. That trades a little extra
traffic for correctness and needs no configuration.

## 8. Metrics and traces

Instrumentation is registered only when there is somewhere to send it, so the
default deployment pays nothing for it.

```bash
# Prometheus scrape endpoint on the API port
PROMETHEUS_ENABLED=true      # exposes http://api:8080/metrics

# or push to an OpenTelemetry collector (metrics + traces)
OTLP_ENDPOINT=http://otel-collector:4317
```

Both can be on at once. What you get: ASP.NET Core request duration and count
by route and status, `HttpClient` call timings, and .NET runtime counters (GC,
thread pool, exceptions).

`/metrics` has no authentication. It exposes route names and status-code
distributions, which is not a secret but is not something to publish either —
leave the `5080` mapping bound to localhost, or scrape it over the Compose
network only.

## 9. Backing up the database

Everything durable lives in the `mssql-data` volume. There are two ways to
protect it; the first is the one to use in production.

### Native SQL Server backup (recommended)

A `.bak` is transactionally consistent and can be restored into any SQL Server
2022 instance, including one running elsewhere.

```bash
# Back up (writes inside the volume, then copies out)
docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -No \
  -Q "BACKUP DATABASE OpenQuiz TO DISK='/var/opt/mssql/data/OpenQuiz.bak' WITH INIT, COMPRESSION, CHECKSUM"

docker compose cp db:/var/opt/mssql/data/OpenQuiz.bak ./OpenQuiz-$(date +%F).bak
```

```bash
# Restore into a running stack
docker compose cp ./OpenQuiz-2026-01-31.bak db:/var/opt/mssql/data/restore.bak

docker compose stop api   # no writers during the restore

docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -No \
  -Q "ALTER DATABASE OpenQuiz SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
      RESTORE DATABASE OpenQuiz FROM DISK='/var/opt/mssql/data/restore.bak' WITH REPLACE;
      ALTER DATABASE OpenQuiz SET MULTI_USER;"

docker compose start api
```

The API applies any pending EF migrations on the next boot, so restoring an
older backup into a newer build is safe.

### Volume snapshot (whole-machine moves)

Copying the files underneath a running SQL Server produces a backup that may
not restore, so stop the database first.

```bash
docker compose down                                  # stop every writer

docker run --rm -v openquiz_mssql-data:/data -v "$PWD":/backup alpine \
  tar czf /backup/mssql-data-$(date +%F).tgz -C /data .

docker compose up -d
```

```bash
# Restore into an empty volume
docker compose down -v
docker volume create openquiz_mssql-data
docker run --rm -v openquiz_mssql-data:/data -v "$PWD":/backup alpine \
  sh -c 'tar xzf /backup/mssql-data-2026-01-31.tgz -C /data && chown -R 10001:0 /data'
docker compose up -d
```

The volume name is prefixed with the Compose project (the directory name), so
check `docker volume ls` if `openquiz_mssql-data` does not exist.

Whichever route you take, **restore into a throwaway stack once before you
need it**. An untested backup is not a backup.

## 10. Customizing

- **Different domain / TLS** — put nginx/Caddy/Traefik in front, terminate TLS there, set
  `PUBLIC_URL` and `VITE_API_BASE_URL` accordingly, rebuild `web`.
- **External DB** — drop the `db` service, point `ConnectionStrings__Default` at your
  managed SQL Server.
- **Disable email** — leave `SMTP_HOST` blank. Password-reset endpoint then returns 503.
- **API on another origin** — the bundled `web` container proxies `/api` and `/hubs`, so
  the shipped Content-Security-Policy only allows `connect-src 'self'`. If you point
  `VITE_API_BASE_URL` at a different host, add that origin to `connect-src` in
  `frontend/security-headers.conf` and rebuild `web`, or the browser blocks every call.

## 11. Troubleshooting

- `db` keeps restarting → the SA password fails MSSQL's complexity policy. Pick a 8+ char
  password with upper/lower/digit/symbol.
- `api` logs *Database not ready yet…* — normal during first 20–30 s while MSSQL warms up.
- `Google ile giriş başarısız` — your `GOOGLE_CLIENT_ID` doesn't list the current origin
  in **Authorized JavaScript origins**.
- `SMTP is not configured on this server` (503) — expected when `SMTP_HOST` is blank.
- Blank page and `Refused to connect …` in the browser console — the Content-Security-Policy
  in `frontend/security-headers.conf` does not list the origin you moved the API to. See
  **Customizing** above.
