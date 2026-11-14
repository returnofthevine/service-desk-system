# Service Desk System

Service Desk System connects employees who need help with technicians who handle their requests. Employees report issues, follow progress, and reply from a browser. Technicians use a separate workspace to triage and assign those same tickets, communicate with the requester, and record the resolution. Both views use one ASP.NET Core API and one SQLite database stored on the computer running the app.

[Download the app](https://github.com/returnofthevine/service-desk-system/releases/latest) · [Watch the employee-to-technician demo](docs/service-desk-workflow.mp4)

| Role | What you can do |
| --- | --- |
| Employee | Submit and track your own requests, read replies and status updates shared with you, and reply to the technician. A registered workstation is optional. |
| Technician | See the service desk queue, prioritize and assign requests, link a workstation, reply to the employee or record an internal note, and resolve the ticket. Create employee and technician accounts, import assets, and view incident metrics. |
| Viewer | Read the staff queue, ticket details, and dashboard with an existing Viewer account. Employees have their own role and can access only their tickets. |

## Install and run

Download the archive for your operating system and processor from [GitHub Releases](https://github.com/returnofthevine/service-desk-system/releases/latest). The downloads include the web interface, API, .NET runtime, and SQLite engine. Downloading from the private repository requires GitHub access.

| Computer | Download suffix | Start from the extracted app folder |
| --- | --- | --- |
| Windows x64 or ARM64 | `win-x64.zip` or `win-arm64.zip` | `./ServiceDesk.Api.exe` in PowerShell |
| Mac with Apple silicon or Intel | `osx-arm64.tar.gz` or `osx-x64.tar.gz` | `./ServiceDesk.Api` in Terminal |
| Linux x64 or ARM64 | `linux-x64.tar.gz` or `linux-arm64.tar.gz` | `./ServiceDesk.Api` in a terminal |

1. Extract the entire archive and open a terminal in the `service-desk-system` folder. Run the command in the table.
2. Open [http://127.0.0.1:5087/](http://127.0.0.1:5087/) on the host computer. If port 5087 is in use, append `--urls http://127.0.0.1:5090` to the command and open port 5090 instead.
3. On first launch, copy the one-time setup code from the terminal and create a technician account in the browser. Sign in with the account you just created.
4. In the technician workspace, open **Accounts** to create an employee account. Use a separate browser profile or private window to sign in as that employee while keeping the technician session open.
5. Leave the host app running while either portal is in use. Press Ctrl+C in the host terminal to stop it.

The Linux packages target glibc-based distributions with the native libraries required by .NET. The macOS packages lack Developer ID signing and notarization, so Gatekeeper may block them under some security settings.

### Use one database across computers

Run one copy of the app on the computer that will host the service desk. Employees and technicians open that host's HTTPS URL in their browsers. The app reads and writes the database file on the host, so every user sees the same ticket numbers and updates.

Complete the first-launch technician setup on the host using the loopback instructions above. Before enabling local network access, arrange a local DNS name and a TLS certificate with that name in its subject alternative names. The employee devices must trust the certificate's issuer and resolve the hostname to the host's LAN IP. Restrict inbound access to the selected port to your local network in the host firewall.

Stop the loopback instance, then start the app in shared mode. For example, on macOS or Linux:

```sh
./ServiceDesk.Api --urls https://0.0.0.0:5087 \
  --ServiceDesk:NetworkHost desk.example.test \
  --ServiceDesk:Tls:CertificatePath /absolute/path/to/desk.pfx
```

On Windows, use the same options on one PowerShell line:

```powershell
./ServiceDesk.Api.exe --urls https://0.0.0.0:5087 --ServiceDesk:NetworkHost desk.example.test --ServiceDesk:Tls:CertificatePath C:\certs\desk.pfx
```

Supply the PFX password to the app process through the `ServiceDesk__Tls__CertificatePassword` environment variable. Employees and technicians can now open `https://desk.example.test:5087/` from their devices. Replace the example DNS name with the hostname on your certificate. Keep the SQLite database in the host's local app-data folder.

### Update, back up, or move data

Close the running app before launching an update. Extract the new version to a separate folder and run it as the same OS user. The app uses the existing per-user `EndpointServiceDesk` data directory and applies pending database migrations at startup. The terminal prints the full data path. The upgrade preserves existing accounts, assets, tickets, and history. Tickets without a recorded requester remain visible to staff.

To back up or carry over data from a source checkout or another host, stop the old app and use SQLite's backup command to make a consistent copy of `servicedesk.db`. Copy the backup into an empty folder that only the host OS user can access. Set `ServiceDesk__DataDirectory` to that folder's absolute path before launch.

```sh
export ServiceDesk__DataDirectory="/absolute/path/to/service-desk-data"
./ServiceDesk.Api
```

```powershell
$env:ServiceDesk__DataDirectory = 'C:\absolute\path\to\service-desk-data'
./ServiceDesk.Api.exe
```

Existing credentials carry over with the database. Users should sign in again on the new host.

## Try the service desk

[Watch the captioned walkthrough](docs/service-desk-workflow.mp4) to follow one employee request from submission through resolution.

1. Sign in as a technician and create an employee account from **Accounts**. Open a second browser profile or private window and sign in as that employee.
2. In the employee portal, open **Report an issue**, choose a category, enter a summary and description, then submit the ticket. It starts open, unassigned, and at priority P3. Save the ticket number shown in **My tickets**.
3. In the technician's **Queue**, open the same ticket number. Set its priority, assign it to yourself or another technician, and start work. Send an employee-visible reply and add an internal note.
4. As the employee, reopen the ticket and check the updated status and reply. Send a reply of your own. Internal notes stay in the staff workspace.
5. As the technician, review the reply and resolve the ticket with an employee-visible resolution note. In the employee browser, refresh the ticket to see the outcome. The updates and messages persist in SQLite.

### Workstation and dashboard demo

The original [endpoint triage video](docs/demo.mp4) walks through ticket **#1** on asset **WS-001**, from the reported BitLocker and TPM states through resolution and a page refresh. It shows the earlier Endpoint Service Desk interface. In a fresh demo database, open ticket #1 from the technician queue to follow along.

Technicians can also upload the [sample asset CSV](fixtures/assets-import-example.csv) in **Import assets**. It contains intentional errors that demonstrate row-level validation. The app saves the batch after all rows pass validation.

On **Dashboard**, select a UTC start date and exclusive end date. The dashboard counts tickets created or resolved in that interval. **Open now** counts the current open and in-progress backlog across all dates. Average resolution time is the mean elapsed time for tickets resolved in the interval. With untouched demo data, **2026-10-27** through the exclusive end date **2026-11-02** shows 20 created, 5 resolved, 15 open now, and a 204-minute average.

## Develop from source

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and [Node.js 24 LTS](https://nodejs.org/en/download). The EF Core SQLite package supplies the database engine.

From the repository root, restore the local EF CLI and set different local passwords. Replace the example values with passwords of your own that include an uppercase letter, lowercase letter, digit, and punctuation. .NET user secrets stores these settings outside the repository.

```sh
dotnet tool restore
dotnet user-secrets set 'DemoAccounts:TechnicianPassword' '<your-technician-password>' --project src/ServiceDesk.Api
dotnet user-secrets set 'DemoAccounts:ViewerPassword' '<your-viewer-password>' --project src/ServiceDesk.Api
```

Start the API in one terminal after applying the database migrations:

```sh
cd src/ServiceDesk.Api
dotnet tool run dotnet-ef database update
dotnet run --no-launch-profile -e DOTNET_ENVIRONMENT=Development -- --urls http://127.0.0.1:5087
```

From the repository root in a second terminal, start the React app:

```sh
cd web
npm ci
npm run dev
```

Open <http://127.0.0.1:5173/> and sign in as `demo-tech` or `demo-viewer` using the corresponding password. Use the technician's **Accounts** page to create a test employee. Vite forwards browser API requests to the local ASP.NET Core API.

## API reference

| Method and route | Access | Behavior |
| --- | --- | --- |
| `GET /api/health` and `GET /api/setup/status` | Public | Report service health and whether packaged first-launch setup is required. |
| `POST /api/setup/technician` | One-time setup code | Creates the first technician on a fresh packaged installation. |
| `GET /api/auth/csrf` and `POST /api/auth/sign-in` | Public | Get an antiforgery token and sign in to a cookie session. |
| `GET /api/auth/me` and `POST /api/auth/sign-out` | Signed in | Read or end the current session. |
| `GET /api/accounts/employees` and `POST /api/accounts/employees` | Technician | List and create employee accounts. |
| `GET /api/accounts/technicians` and `POST /api/accounts/technicians` | Technician | List and create technician accounts. |
| `GET /api/employee/tickets` and `POST /api/employee/tickets` | Employee | List your requests or submit a new request. |
| `GET /api/employee/tickets/{id}` and `POST /api/employee/tickets/{id}/replies` | Ticket requester | Read employee-visible ticket details or reply on that ticket. |
| `GET /api/tickets` and `GET /api/v2/tickets` | Technician, Viewer | Read the staff queue. The v2 route supports pagination. |
| `GET /api/tickets/{id}` | Technician, Viewer | Read staff ticket details, workstation data, messages, and audit history. |
| `POST /api/tickets` | Technician | Create a ticket from the existing staff API. |
| `GET /api/tickets/options` and `PATCH /api/tickets/{id}/triage` | Technician | Read technician and asset choices or update a ticket's assignee, priority, and linked asset. |
| `PATCH /api/tickets/{id}/status` | Technician | Move a ticket through open, in progress, and resolved. Resolution requires a note. |
| `POST /api/tickets/{id}/replies` | Technician | Add an employee-visible reply or an internal note. |
| `POST /api/assets/import` | Technician | Import workstation data from CSV. |
| `GET /api/dashboard` | Technician, Viewer | Read incident metrics for a UTC date range. |

For API writes, keep the cookies across requests, fetch a fresh token from `/api/auth/csrf`, and include it in the `X-CSRF-TOKEN` header. Fetch the token again after signing in. Example JSON request bodies live in [fixtures](fixtures/).

## Build and test

The xUnit integration tests start the real ASP.NET Core API with isolated SQLite databases and apply the committed migrations. GitHub Actions runs the API and web checks on pushes to `main` and on pull requests. The release workflow builds and smoke-tests the installation archives on Windows, macOS, and Linux runners.

```sh
dotnet test EndpointServiceDesk.slnx --configuration Release
npm --prefix web ci
npm --prefix web run lint
npm --prefix web run build
```

To reset a source-checkout demo, stop the API and run the commands below from `src/ServiceDesk.Api/`. This deletes the local demo database, including any accounts, ticket changes, or imports you added. The next Development startup recreates the demo accounts from the existing user secrets.

```sh
dotnet tool run dotnet-ef database drop --force
dotnet tool run dotnet-ef database update
```

| Path | Contents |
| --- | --- |
| `src/ServiceDesk.Api/` | ASP.NET Core API, Identity authorization, EF Core migrations, and SQLite data. |
| `tests/ServiceDesk.Api.Tests/` | API, authorization, CSV, dashboard, and database concurrency tests. |
| `web/` | React employee and technician views, API client, and Vite configuration. |
| `fixtures/` | Sample JSON requests and workstation CSV data. |
| `scripts/` | Cross-platform archive assembly and installation smoke checks. |
