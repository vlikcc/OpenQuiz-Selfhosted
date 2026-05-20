# OpenQuiz — Self-Hosted

Self-hostable, real-time quiz / survey / exam / **word cloud** platform.

- **Backend:** ASP.NET Core 10 + EF Core 10
- **Database:** Microsoft SQL Server 2022
- **Frontend:** React 19 + Vite + TailwindCSS
- **Real-time:** SignalR
- **Auth:** JWT (email/password) + Google Sign-In (ID token verified server-side)
- **Deployment:** Docker Compose

> **Note:** This project is a migration of [vlikcc/OpenQuiz](https://github.com/vlikcc/OpenQuiz) from Firebase to a self-hostable stack. The only remaining Google dependency is **Google Sign-In** (OAuth ID token verification). No Firestore, no Firebase Hosting, no FCM.

## Status

🚧 **Phase 1 — Backend skeleton** (in progress)

See [MIGRATION_PLAN.md](MIGRATION_PLAN.md) for the full roadmap.

## Project Structure

```
.
├── backend/                 # .NET 10 solution
│   ├── src/
│   │   ├── OpenQuiz.Api/
│   │   ├── OpenQuiz.Application/
│   │   ├── OpenQuiz.Domain/
│   │   └── OpenQuiz.Infrastructure/
│   └── tests/
├── frontend/                # React app (to be added in Phase 4)
├── docs/                    # Architecture & API docs (to be added)
├── docker-compose.yml       # (to be added in Phase 7)
└── MIGRATION_PLAN.md
```

## Local Development

### Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet run --project src/OpenQuiz.Api
```

Default URLs:
- API:     `http://localhost:5080`
- Swagger: `http://localhost:5080/swagger`

### Database

A local SQL Server instance is required. The fastest path:

```bash
docker run -d --name openquiz-mssql \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='Your_Strong_Password!' \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

Then update `backend/src/OpenQuiz.Api/appsettings.Development.json` connection string.

## License

MIT
