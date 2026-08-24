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
| `api` (.NET 10 / Kestrel) | `5080` | applies EF migrations on boot |
| `web` (nginx + React build) | `8080` | SPA served from `/usr/share/nginx/html` |

Open the app at `http://localhost:8080`.

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

## 8. Customizing

- **Different domain / TLS** — put nginx/Caddy/Traefik in front, terminate TLS there, set
  `PUBLIC_URL` and `VITE_API_BASE_URL` accordingly, rebuild `web`.
- **External DB** — drop the `db` service, point `ConnectionStrings__Default` at your
  managed SQL Server.
- **Disable email** — leave `SMTP_HOST` blank. Password-reset endpoint then returns 503.

## 9. Troubleshooting

- `db` keeps restarting → the SA password fails MSSQL's complexity policy. Pick a 8+ char
  password with upper/lower/digit/symbol.
- `api` logs *Database not ready yet…* — normal during first 20–30 s while MSSQL warms up.
- `Google ile giriş başarısız` — your `GOOGLE_CLIENT_ID` doesn't list the current origin
  in **Authorized JavaScript origins**.
- `SMTP is not configured on this server` (503) — expected when `SMTP_HOST` is blank.
