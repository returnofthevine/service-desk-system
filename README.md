# Endpoint Service Desk

Endpoint Service Desk is a local help desk for a fictional organization. It gives technicians one place to triage workstation issues, check reported BitLocker and Trusted Platform Module (TPM) states, and record how each ticket was resolved. A SQLite database keeps tickets, workstation records, and status history across browser refreshes and server restarts. The demo includes 12 workstations and 20 tickets.

## What you can do

- Filter the queue by priority and status. Filters and page numbers are stored in the URL so you can return to the same view from a ticket.
- Open a ticket to review its workstation, reported security states, assignee, timestamps, and audit history. The states come from seed data or a CSV import.
- Sign in as a **Technician** to start work, resolve tickets with a note, create tickets through the API, and import workstations from CSV. Sign in as a **Viewer** to read the queue, ticket details, and dashboard.
- Track ticket volume, resolutions, backlog, and average resolution time on the incident dashboard.

## Run locally

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and [Node.js 24 LTS](https://nodejs.org/en/download). The EF Core SQLite package supplies the database engine.

From the repository root, restore the local EF CLI and set two different local passwords. Replace the example values with passwords of your own that include an uppercase letter, lowercase letter, digit, and punctuation. The settings are stored outside the repository by .NET user secrets.

```sh
dotnet tool restore
dotnet user-secrets set 'DemoAccounts:TechnicianPassword' '<your-technician-password>' --project src/ServiceDesk.Api
dotnet user-secrets set 'DemoAccounts:ViewerPassword' '<your-viewer-password>' --project src/ServiceDesk.Api
```

Start the API in one terminal. Apply the migrations before starting it so the demo users can be provisioned in Development.

```sh
cd src/ServiceDesk.Api
dotnet tool run dotnet-ef database update
dotnet run --no-launch-profile -e DOTNET_ENVIRONMENT=Development -- --urls http://127.0.0.1:5087
```

From the repository root in a second terminal, start the React app.

```sh
cd web
npm ci
npm run dev
```

Open <http://127.0.0.1:5173/>. Sign in as `demo-tech` or `demo-viewer` using the corresponding password. The Vite development server forwards `/api` requests to `http://127.0.0.1:5087`.

## Try the ticket workflow

1. Sign in as `demo-tech`. In the queue, select **Open** and **P2**. Open ticket **#1**, the BitLocker suspension report for `WS-001`.
2. Check the workstation card. It reports BitLocker **Suspended** and TPM **Ready**. Select **Start work**. The app assigns the ticket to your account and records the change.
3. Try resolving with a blank note to see the validation message. Enter `Checked the reported BitLocker and TPM states. Recorded the follow-up.` and resolve the ticket.
4. Refresh the detail page. The resolved timestamp and both audit events are loaded from SQLite. Sign in as `demo-viewer` to see the same history with read access.

To see how stale edits are handled, open a different open ticket in two tabs. Start work in the first tab, then try starting work in the second. The second request receives `409 Conflict` and the page reloads the saved status. The API checks the expected status before editing. EF Core also compares the original status when saving, so the ticket and its audit entry commit together even when two requests arrive at the same time.

## Import workstation data

As a technician, open **Import assets** and upload [the sample CSV](fixtures/assets-import-example.csv). It contains an intentional duplicate tag and two invalid fields. The page lists the errors by row and field, and the API saves zero assets. Fix those values and upload again to create all three assets. CSV uploads accept up to 1 MiB and 1,000 data rows. Asset tags are compared case-insensitively.

```csv
assetTag,hostname,bitLockerState,tpmState,lastCheckedAtUtc
WS-025,LAB-WS-01,protected,ready,2026-11-01T13:30:00Z
```

## Read the incident dashboard

Select a start date and an exclusive end date. The app uses UTC midnight for both dates. The dashboard counts tickets created within that interval and tickets resolved within it, even if they were created earlier. **Open now** counts the current open and in-progress backlog across all dates. Average resolution time is the mean of `resolvedAtUtc - createdAtUtc` for tickets resolved during the interval. For example, 30, 60, and 180 minutes average to 90 minutes.

With untouched demo data, choose **2026-10-27** through the exclusive end date **2026-11-02** to see 20 created, 5 resolved, 15 open now, and a 204-minute average.

## API reference

| Method and route | Access | Behavior |
| --- | --- | --- |
| `GET /api/health` | Public | Reports whether the API process is running. |
| `GET /api/auth/csrf` | Public | Sets the antiforgery cookie and returns a request token. |
| `POST /api/auth/sign-in` | Public | Exchanges a username and password for a session cookie. |
| `GET /api/auth/me` and `POST /api/auth/sign-out` | Signed in | Read or end the session. |
| `GET /api/tickets` | Technician, Viewer | Lists tickets with optional `status` and `priority` filters. |
| `GET /api/v2/tickets` | Technician, Viewer | Adds `page` and `pageSize` with defaults of 1 and 10. |
| `GET /api/tickets/{id}` | Technician, Viewer | Returns the ticket, workstation, and chronological history. |
| `POST /api/tickets` | Technician | Creates an open ticket for an existing asset. Returns `201` and a `Location` header. |
| `PATCH /api/tickets/{id}/status` | Technician | Moves `open` → `in-progress` → `resolved`. Resolution requires a note. |
| `POST /api/assets/import` | Technician | Validates a multipart `file` and saves the batch in one operation. |
| `GET /api/dashboard?from=2026-10-27&to=2026-11-02` | Technician, Viewer | Reads the incident metrics for a UTC date range. |

For API writes, keep the cookies across requests, fetch a fresh token from `/api/auth/csrf`, and include it in the `X-CSRF-TOKEN` header. Fetch the token again after signing in. Example JSON request bodies live in [fixtures](fixtures/).

## Build, test, and reset

The ASP.NET Core API uses Entity Framework Core and SQLite. React and React Router provide the web interface. The xUnit integration tests start the real API with isolated SQLite databases and apply the committed migrations. GitHub Actions runs the API and web checks on each push to `main` and on pull requests.

```sh
dotnet test EndpointServiceDesk.slnx --configuration Release
npm --prefix web ci
npm --prefix web run lint
npm --prefix web run build
```

To reset the demo, stop the API and run the following from `src/ServiceDesk.Api/`. This deletes the local demo database, including any ticket changes and imports. The next Development startup recreates the demo accounts from your existing local password settings.

```sh
dotnet tool run dotnet-ef database drop --force
dotnet tool run dotnet-ef database update
```

Existing demo accounts keep their passwords across restarts. Reset the local database to recreate them after changing the user secrets.

| Path | Contents |
| --- | --- |
| `src/ServiceDesk.Api/` | HTTP endpoints, Identity roles, EF Core migrations, and demo seed data. |
| `tests/ServiceDesk.Api.Tests/` | API, authorization, CSV, dashboard, and database concurrency tests. |
| `web/` | React pages, API client, and Vite configuration. |
| `fixtures/` | Sample JSON requests and workstation CSV data. |
