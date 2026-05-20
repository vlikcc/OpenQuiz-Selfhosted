# OpenQuiz API reference

Base URL: `http://<host>:5080` (default Docker mapping).
OpenAPI document: `GET /openapi/v1.json` (Development env).
Errors follow RFC 7807 (`application/problem+json`).

All `Authorization: Bearer <accessToken>` headers must use the JWT returned by `/api/auth/*`.
Refresh tokens rotate on every `/api/auth/refresh` call.

## Auth

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/api/auth/google` | — | `{ idToken }` → tokens |
| POST | `/api/auth/register` | — | `{ email, password, displayName? }` |
| POST | `/api/auth/login` | — | `{ email, password }` |
| POST | `/api/auth/refresh` | — | `{ refreshToken }` |
| POST | `/api/auth/logout` | — | `{ refreshToken }` |
| GET | `/api/auth/me` | bearer | Current user |
| POST | `/api/auth/password-reset/request` | — | `{ email }`; 503 if SMTP unset |
| POST | `/api/auth/password-reset/confirm` | — | `{ token, newPassword }` |

`AuthResponse` shape:
```json
{
  "accessToken": "eyJ…",
  "refreshToken": "…",
  "accessTokenExpiresAt": "2026-05-20T20:00:00Z",
  "refreshTokenExpiresAt": "2026-06-19T20:00:00Z",
  "user": { "id": "…", "email": "…", "displayName": "…", "isAdmin": false, "canCreate": false }
}
```

## Polls

| Method | Path | Auth |
|---|---|---|
| GET | `/api/polls` | bearer (own; admin sees all) |
| GET | `/api/polls/{id}` | — |
| POST | `/api/polls` | bearer + `canCreate` |
| PUT | `/api/polls/{id}` | bearer (owner/admin) |
| DELETE | `/api/polls/{id}` | bearer (owner/admin) |
| POST | `/api/polls/{id}/activate` | bearer (owner/admin) |
| POST | `/api/polls/{id}/next-question` | bearer (owner/admin) |
| POST | `/api/polls/{id}/prev-question` | bearer (owner/admin) |
| POST | `/api/polls/{id}/end` | bearer (owner/admin) |
| POST | `/api/polls/{id}/join` | — | `{ userName }` |

`PollType` enum: `1=Contest 2=Survey 3=Quiz 4=Exam 5=WordCloud`.
`PollStatus`: `1=Waiting 2=Live 3=Ended`. `QuestionType`: `1=MultipleChoice 2=Open 3=WordCloud`.

## Votes

| Method | Path | Auth |
|---|---|---|
| POST | `/api/polls/{id}/votes` | — | `{ questionIndex, selectedIndices[], responseTimeMs?, userName }` |
| POST | `/api/polls/{id}/open-answers` | — | `{ questionIndex, answerText, userName }` |
| GET | `/api/polls/{id}/votes` | bearer (owner/admin) |
| GET | `/api/polls/{id}/open-answers` | bearer (owner/admin) |
| GET | `/api/polls/{id}/aggregates` | — | Per-question option counts |

## Word Cloud

| Method | Path | Auth |
|---|---|---|
| POST | `/api/polls/{id}/wordcloud/submit` | — | `{ questionIndex, terms[], userName }` |
| GET | `/api/polls/{id}/wordcloud/{questionIndex}?topN=50` | — |

Terms are normalized server-side (lowercase, trim, regex filter) and de-duplicated.

## Scores & Users

- `GET /api/scores?pollId=…&top=100` — leaderboard
- `GET /api/users/authorized` — admin
- `POST /api/users/authorized` — admin (`{ email }`)
- `DELETE /api/users/authorized/{email}` — admin
- `GET /api/users/registered` — admin

## Reactions

- `POST /api/polls/{id}/reactions` — `{ emoji, sender }`. Broadcasts via SignalR only.

## SignalR Hub: `/hubs/poll`

Client → server: `JoinPoll(pollId)`, `LeavePoll(pollId)`.
Authenticate via `?access_token=…` query string or `Authorization` header (Bearer).

Server → client events:

| Event | Payload |
|---|---|
| `PollUpdated` | full `PollDto` |
| `VoteCountsUpdated` | `{ questionIndex, questionId, totalRespondents, optionCounts }` (throttled 250 ms) |
| `WordCloudUpdated` | `{ questionIndex, terms: [{ term, count }] }` (throttled 250 ms) |
| `OpenAnswerSubmitted` | `{ questionIndex, userName }` |
| `ReactionBurst` | `{ emoji, sender, at }` |
