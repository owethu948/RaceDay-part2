# RaceDay – Part 2: RESTful API

[![CI](../../actions/workflows/ci.yml/badge.svg)](../../actions/workflows/ci.yml)

RaceDay is an event management platform for South African road running, walking and cycling events
(Comrades, Cape Town Cycle Tour, Soweto Marathon, park runs, charity rides ...). **Part 2** is the REST API
that powers the whole system: ASP.NET Core Web API, Entity Framework Core (Code-First) on SQL Server,
server-side session authentication with two roles, Swagger documentation, unit tests and a GitHub Actions pipeline.

| | |
|---|---|
| **YouTube demo (unlisted)** | `PASTE-YOUR-UNLISTED-YOUTUBE-LINK-HERE` |
| **CI status** | see the screenshot below |

---

## 1. The two roles

| Role | Can do | Cannot do |
|---|---|---|
| **Organiser** | Create / update / delete **their own** events; add / update / delete categories for their events; view all enrolments for their events; capture and correct results; cancel enrolments in their events; view and update their own profile. | Enrol in events or view "my enrolments / my results" (Participant functionality). |
| **Participant** | Browse events and categories; enrol in an event by choosing a category; view and cancel their own enrolments; view their own results; view and update their own profile. | Create, change or delete events / categories; view an event's enrolments; capture results. |

Anyone (even without logging in) can browse events, categories and event results.

**How do people become Organisers?** Anyone may register as a Participant. To stop users simply promoting
themselves, registering as an **Organiser** also needs the *organiser registration code* (`Auth:OrganiserRegistrationCode`
in `appsettings.json`, default `RACEDAY-ORGANISER-2026`). Wrong or missing code gives **403**.

### Authentication & sessions

1. `POST /api/auth/register` – creates the user and profile in one transaction. The password is stored **only** as a
   salted PBKDF2-HMAC-SHA256 hash (100 000 iterations, random 16-byte salt per password) – see `Services/PasswordService.cs`.
2. `POST /api/auth/login` – verifies the hash, then stores the **user id and role in a server-side session**
   (`Auth/SessionKeys.cs`). The browser only keeps the `.RaceDay.Session` cookie.
3. Every protected endpoint carries `[SessionAuthorize]` (`Auth/SessionAuthorizeAttribute.cs`):
   * no valid session → **401 Unauthorized**
   * logged in with the wrong role → **403 Forbidden**
   * Organisers are additionally checked for *ownership* (you can only change your own events) → **403**
4. `POST /api/auth/logout` clears the session. Sessions also expire after 30 minutes of inactivity.

---

## 2. Setup and run

### Prerequisites
* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* SQL Server – the default connection string uses **SQL Server LocalDB**, which is installed with Visual Studio
  (workload *ASP.NET and web development*). Any SQL Server instance also works.
* Visual Studio 2026 (or VS Code / the `dotnet` CLI)

### Steps
```bash
git clone <your-repo-url>
cd RaceDay-part2

# (optional) point the API at a different SQL Server
#   edit ConnectionStrings:DefaultConnection in appsettings.json

dotnet run --project RaceDay-part2.csproj
```
Or in Visual Studio: open `RaceDay-part2.slnx`, set **RaceDay-part2** as the startup project and press **F5**.

On first start the API **creates the database `RaceDayDB_Part2` automatically** (EF Core, Code-First) and seeds demo data
(2 Organisers, 2 Participants, 3 events, categories, enrolments and results). Swagger opens at
`http://localhost:5146/swagger` (the root URL redirects there).

`database/RaceDay_Schema_Part2.sql` is the Part 1 SQL script updated for Part 2 (adds `Events.EventType` and
`Events.DistanceKm`). It is kept for comparison in SSMS – the API builds its own schema from the EF model, which matches it.

### Demo accounts (password for all: `Password123!`)
| Role | Email |
|---|---|
| Organiser | `thabo.organiser@raceday.co.za` |
| Organiser | `lindiwe.organiser@raceday.co.za` |
| Participant | `sipho.runner@raceday.co.za` |
| Participant | `anke.cyclist@raceday.co.za` |

### Trying the API in Swagger
1. Open `/swagger`, expand **POST /api/auth/login**, click *Try it out*, enter a demo account and *Execute*.
2. The browser now holds the session cookie, so every other endpoint in Swagger is authenticated as that user.
3. Try an Organiser-only endpoint while logged in as a Participant to see the **403**, or log out to see the **401**.

Every endpoint in Swagger shows its purpose, an **Access** line (public / any logged-in user / role), the request body
and every response code.

---

## 3. Endpoints (matches the Part 1 plan)

| Method | Route | Access | Purpose |
|---|---|---|---|
| POST | `/api/auth/register` | Public | Register as Organiser (needs code) or Participant |
| POST | `/api/auth/login` | Public | Log in, start a server-side session |
| POST | `/api/auth/logout` | Any logged-in | End the session |
| GET | `/api/users/me` | Any logged-in | View own profile |
| PUT | `/api/users/me` | Any logged-in | Update own profile |
| GET | `/api/users/{id}` | Organiser | Another user's public profile |
| GET | `/api/events` | Public | List events (`search`, `location`, `upcoming`, `page`, `pageSize`) |
| GET | `/api/events/{id}` | Public | Event details with categories |
| POST | `/api/events` | Organiser | Create event (name, description, date, location, distance, type Run/Walk/Cycle) |
| PUT | `/api/events/{id}` | Organiser (owner) | Update event |
| DELETE | `/api/events/{id}` | Organiser (owner) | Delete event (cascades) |
| GET | `/api/events/{eventId}/categories` | Public | List categories |
| POST | `/api/events/{eventId}/categories` | Organiser (owner) | Add category |
| PUT | `/api/categories/{id}` | Organiser (owner) | Update category |
| DELETE | `/api/categories/{id}` | Organiser (owner) | Delete category |
| POST | `/api/categories/{categoryId}/enrolments` | Participant | Enrol in an event via a category |
| GET | `/api/users/me/enrolments` | Participant | My enrolments |
| GET | `/api/events/{eventId}/enrolments` | Organiser (owner) | All enrolments for my event |
| DELETE | `/api/enrolments/{id}` | Participant (owner) or event Organiser | Cancel an enrolment |
| POST | `/api/enrolments/{enrolmentId}/result` | Organiser (owner) | Capture finish time / position / status |
| PUT | `/api/results/{id}` | Organiser (owner) | Correct a result |
| GET | `/api/users/me/results` | Participant | My results |
| GET | `/api/events/{eventId}/results` | Public | Event leaderboard |

> The Part 1 plan mentioned JWT; the Part 2 brief requires **session-based** authentication, so sessions are used.

---

## 4. Code structure

```
RaceDay-part2/
├── Program.cs                  # DI, EF Core, session, Swagger, pipeline
├── Auth/                       # SessionAuthorizeAttribute (401/403) + SessionKeys
├── Controllers/                # Auth, Users, Events, Categories, Enrolments, Results
├── Data/                       # RaceDayDbContext (Code-First model) + DbSeeder
├── Models/                     # EF entities (Users, UserProfiles, Events, Categories, Enrolments, Results)
├── Dtos/                       # Request/response contracts + validation attributes
├── Services/                   # PasswordService (PBKDF2 hashing)
├── Swagger/                    # Operation filter that documents access rules / 401 / 403
├── database/                   # RaceDay_Schema_Part2.sql
├── RaceDay.Tests/              # xUnit tests (in-memory API + SQLite)
└── .github/workflows/ci.yml    # Build + test on every push
```

---

## 5. Unit tests

```bash
dotnet test RaceDay-part2.slnx
```

The tests start the **real API in memory** (`WebApplicationFactory`) with an in-memory **SQLite** database instead of
SQL Server, so they need no installed database and run identically on GitHub Actions. Each test logs in through the real
`/api/auth/login` endpoint and uses the session cookie, exactly like a browser.

| File | What it proves |
|---|---|
| `AuthTests` | Registration (roles, organiser code, duplicates, validation), password stored hashed, login success/failure, logout, session identifies the user, profile view/update for both roles |
| `PasswordServiceTests` | Hashing is salted, verification works, malformed hashes are rejected |
| `EventTests` | Organisers create/update/delete events; owner-only rules; participants and anonymous users rejected; search and paging |
| `CategoryTests` | Organisers manage categories; everyone can view them; role + ownership enforced |
| `EnrolmentTests` | Participants enrol and the Participant–Event–Category link is recorded; duplicates, full categories and past events rejected; organiser views entrants; cancelling |
| `ResultTests` | Organisers capture/correct results; participants see only their own |
| `RoleEnforcementTests` | Every protected endpoint: **401** without a session, **403** for the wrong role |

---

## 6. CI/CD (GitHub Actions)

`.github/workflows/ci.yml` runs on every push and pull request: validates the repository structure, restores,
builds in Release, runs all tests, lists vulnerable NuGet packages and uploads the test results.

![Successful green build](docs/ci-green-build.png)

> **Add your screenshot:** after the first green run, open the *Actions* tab, screenshot the green run and save it as
> `docs/ci-green-build.png`.

---

## 7. Security notes

* Passwords are never stored in plain text (PBKDF2 + per-user salt, constant-time comparison).
* Session cookie is `HttpOnly`; the server holds the user id and role.
* **Dependency fix:** `Microsoft.Data.SqlClient` pulled in `System.Security.Cryptography.Pkcs 9.0.11`, which is affected by
  GHSA-846f-f764-76mx (high) and GHSA-2fmv-rv2c-59x7 (moderate). Both are fixed in **10.0.13**, which is now referenced
  directly in `RaceDay-part2.csproj`. `NuGetAudit` is set to `all` so future transitive vulnerabilities show as build warnings.
* Session state is held in memory (fine for a single instance / this assignment). A production system would use a
  distributed cache such as Redis or SQL Server.
