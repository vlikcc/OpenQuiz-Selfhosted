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

## 6. Customizing

- **Different domain / TLS** — put nginx/Caddy/Traefik in front, terminate TLS there, set
  `PUBLIC_URL` and `VITE_API_BASE_URL` accordingly, rebuild `web`.
- **External DB** — drop the `db` service, point `ConnectionStrings__Default` at your
  managed SQL Server.
- **Disable email** — leave `SMTP_HOST` blank. Password-reset endpoint then returns 503.

## 7. Troubleshooting

- `db` keeps restarting → the SA password fails MSSQL's complexity policy. Pick a 8+ char
  password with upper/lower/digit/symbol.
- `api` logs *Database not ready yet…* — normal during first 20–30 s while MSSQL warms up.
- `Google ile giriş başarısız` — your `GOOGLE_CLIENT_ID` doesn't list the current origin
  in **Authorized JavaScript origins**.
- `SMTP is not configured on this server` (503) — expected when `SMTP_HOST` is blank.
