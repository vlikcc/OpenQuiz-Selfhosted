# OpenQuiz — Self-Hosted

Self-hostable, real-time **quiz / survey / exam / word cloud** platform — a Firebase-free fork of [vlikcc/OpenQuiz](https://github.com/vlikcc/OpenQuiz).

- **Backend:** ASP.NET Core 10 + EF Core 10
- **Database:** Microsoft SQL Server 2022
- **Frontend:** React 19 + Vite + TailwindCSS
- **Real-time:** SignalR (auto-reconnecting WebSocket)
- **Auth:** JWT (email / password) **+** Google Sign-In (ID token verified server-side via `Google.Apis.Auth`)
- **E-mail:** MailKit SMTP (for password reset)
- **Deployment:** Docker Compose (one command)

> The only remaining Google dependency is **Google Sign-In** — no Firestore, no Firebase Hosting, no FCM, no Cloud Functions.

## Quick start

```bash
cp .env.example .env
# edit .env — set MSSQL_SA_PASSWORD, JWT_SIGNING_KEY, GOOGLE_CLIENT_ID, ADMIN_EMAIL
docker compose up -d --build
```

Then open <http://localhost:8080>.

Detailed instructions live in [docs/SELF_HOSTING.md](docs/SELF_HOSTING.md).

## Features

- **Yarışma** (contest) — multiple choice with correct answers and scoring
- **Anket** (survey) — multi/single select, no right answer
- **Quiz** — single-question burst poll
- **Sınav** (exam) — multi-choice + open-ended with KaTeX/Markdown
- **Kelime Bulutu** (word cloud) — live Mentimeter-style aggregation 🆕
- QR-based join, live emoji reactions, presenter mode, results export (PDF / Excel)

## Project structure

```
.
├── backend/                       # .NET 10 solution
│   ├── src/
│   │   ├── OpenQuiz.Domain/       # entities + enums
│   │   ├── OpenQuiz.Application/  # DTOs, validators, abstractions
│   │   ├── OpenQuiz.Infrastructure/ # EF Core, services, options, auth, SMTP
│   │   └── OpenQuiz.Api/          # controllers, SignalR hub, middleware
│   └── Dockerfile
├── frontend/                      # React app
│   ├── src/
│   │   ├── services/              # apiClient, *Service, realtimeService
│   │   ├── hooks/                 # useAuth
│   │   ├── components/            # screens (incl. wordcloud/*)
│   │   └── config/                # constants (enum maps, CONTENT_TYPES)
│   ├── Dockerfile
│   └── nginx.conf
├── docs/
│   ├── ARCHITECTURE.md
│   ├── API.md
│   └── SELF_HOSTING.md
├── docker-compose.yml
├── .env.example
├── MIGRATION_PLAN.md              # the full plan that drove this rewrite
└── README.md
```

## Local development (no Docker)

### Database

```bash
docker run -d --name openquiz-mssql \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='Your_Strong_Password!' \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

### Backend

```bash
cd backend
dotnet build OpenQuiz.slnx
dotnet run --project src/OpenQuiz.Api
# API on http://localhost:5142 (or http://localhost:5080 if ASPNETCORE_URLS is set)
# OpenAPI doc at /openapi/v1.json
```

Update `backend/src/OpenQuiz.Api/appsettings.json` (or override with env vars) to set
`ConnectionStrings:Default`, `Jwt:SigningKey`, `Google:ClientId`.

### Frontend

```bash
cd frontend
cp .env.example .env       # set VITE_API_BASE_URL + VITE_GOOGLE_CLIENT_ID
npm install
npm run dev
# Vite dev server on http://localhost:5173
```

## Documentation

- 📐 [Architecture](docs/ARCHITECTURE.md)
- 🛣️ [API reference](docs/API.md)
- 🛠️ [Self-hosting guide](docs/SELF_HOSTING.md)
- 📋 [Migration plan](MIGRATION_PLAN.md)

## License

MIT. Issues and PRs welcome.
