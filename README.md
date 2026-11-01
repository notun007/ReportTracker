# ReportTracker

BB Reporting Tracker records internal handovers and Bangladesh Bank sending events. It stores tracking metadata, not business report datasets, report files, email bodies or report attachments.

## Projects

- `src/ReportTracker.Domain`: tracking entities and status vocabulary.
- `src/ReportTracker.Application`: contracts, validation and deadline rules.
- `src/ReportTracker.Infrastructure`: EF Core 10, SQL Server and scoped tracking services.
- `src/ReportTracker.Api`: ASP.NET Core 10 REST API, local sessions and optional SSO bearer validation.
- `../ReportTracker.Web`: Angular 22 standalone client in the sibling repository.
- `database/ReportTracker.sql`: idempotent SQL Server deployment script with three migrations and 32 domain tables.

## Configuration

The existing local connection is stored in ASP.NET Core user secrets for this API project and loads automatically in Development. For another machine or deployment, configure its own secret or environment variable. Do not commit database passwords.

```powershell
$env:ConnectionStrings__ReportTracker = 'Server=YOUR_SERVER;Database=ReportTracker;User ID=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True'
```

Local accounts are enabled by default. Passwords use ASP.NET Core's versioned PBKDF2 password hasher. Five failed sign-in attempts lock the account for 15 minutes. Sessions use HttpOnly cookies with an eight-hour absolute lifetime. Cookie-authenticated changes require the antiforgery header supplied by Angular.

## Create or upgrade the database

Create the target database, then apply `database/ReportTracker.sql`. For SQLCMD, use `-I` to enable QUOTED_IDENTIFIER, which the filtered indexes require, and `-b` to stop on errors. Supply credentials through a protected mechanism such as `SQLCMDPASSWORD`, not committed scripts.

```powershell
sqlcmd -S YOUR_SERVER -d ReportTracker -U YOUR_USER -C -I -b -i database/ReportTracker.sql
```

To regenerate the deployment script:

```powershell
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet tool run dotnet-ef migrations script --idempotent --project src/ReportTracker.Infrastructure --startup-project src/ReportTracker.Api --output database/ReportTracker.sql
```

Do not apply migrations automatically during application startup.

## Run

Use Node 22.22.3+, 24.15.0+, or another version supported by the installed Angular CLI. Run these commands in separate terminals:

```powershell
dotnet run --project src/ReportTracker.Api --launch-profile http
```

```powershell
cd ../ReportTracker.Web
npm ci
npm start
```

Open `http://localhost:4200`. The development proxy targets `http://localhost:5092`. In Visual Studio, select the `http` project profile rather than `IIS Express` to use that port. IIS Express has separate ports; using it requires a matching development proxy target and a client-server restart.

The first visit shows workspace setup. Create the coordinating division and initial account with a password of at least 12 characters. This account receives **two explicit roles**: System Administrator and Report Coordinator. Initial setup is permitted from a loopback connection in Development. Other environments require `Authentication__SetupKey`; enter it in the setup form. Setup closes permanently once an account exists.

1. Use Administration to create divisions and user accounts. A user optionally receives a division-scoped Report Coordinator role; all active officers can record their assigned tasks.
2. Use Report Setup to create a report definition and a reporting cycle with approved deadlines. Include at least one BB obligation and any internal handovers that actually need tracking.
3. Assigned officers record partial or full sending, including actual time, channel and reference. Actual times are entered in Bangladesh time and stored as UTC. Sending history preserves the actor and separate system-entry timestamp.
4. Refresh or sign in again to see persisted records. There is no demo-data fallback.

Duplicate report codes, account identifiers and report periods return HTTP 409. Sending updates include the SQL rowversion; a stale or already-completed record cannot be overwritten. The API derives the audit actor from the authenticated principal and enforces assignment or explicit scoped grants.

## Future bank SSO

The API already accepts bearer tokens through a separate authentication scheme. When the bank documentation is available, configure:

```powershell
$env:Authentication__Authority = 'https://YOUR_BANK_AUTHORITY/'
$env:Authentication__Audience = 'YOUR_API_AUDIENCE'
```

The provider must issue a validated `user_id` claim containing the GUID of an active tracker user, or an explicit subject-to-user mapping must be implemented. Identity, role and scope grants remain in the tracker database. The browser authorization-code/PKCE flow still needs provider-specific integration. After SSO is verified, set `Authentication__LocalEnabled=false` to disable local login and bootstrap. Existing local sessions expire within eight hours.

Use a same-origin HTTPS reverse proxy for the deployed client and API. Persist and protect ASP.NET Core Data Protection keys across deployments; multi-instance deployments must share the key ring and application name. Configure a bank-approved secret store and a least-privilege SQL application account.

## Verify

```powershell
dotnet build ReportTracker.slnx -c Release
dotnet test ReportTracker.slnx -c Release --no-build
cd ../ReportTracker.Web
npm run build
```

Backend tests cover assignment isolation, trusted audit identity, partial sending, input validation, actual-time timeliness and Dhaka calendar boundaries. End-to-end verification should use a separate database so test accounts and report records do not enter the operational workspace.

## Implementation status

Implemented: local sign-in/setup/logout; division and account creation; scoped coordinator access; report definitions; manually created reporting cycles; internal/BB trackers; partial/full sending; concurrency checks; sending history; live dashboard filters; deadline agenda and sending-status monitoring.

Remaining from `BB_Reporting_Tracker_Plan.md`: automatic recurring cycle generation and schedule/version editing; receipt confirmation/dispute workflows; confirmation-only evidence storage and verification; corrections; extensions and approval decisions; event amendments; delegation; reminder/delivery workers; full month/week calendar and exports; advanced management metrics; account password reset/change and deactivation; and provider-specific SSO/Outlook integration. Evidence requirements remain visible in the tracker even though evidence upload is not yet implemented.


