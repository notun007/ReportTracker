# ReportTracker

BB Reporting Tracker records internal handovers and Bangladesh Bank sending events. It stores tracking metadata, not business report datasets, report files, email bodies or report attachments.

## Projects

- `src/ReportTracker.Domain`: tracking entities and status vocabulary.
- `src/ReportTracker.Application`: contracts, validation and deadline rules.
- `src/ReportTracker.Infrastructure`: EF Core 10, SQL Server and scoped tracking services.
- `src/ReportTracker.Api`: ASP.NET Core 10 REST API, local sessions and optional SSO bearer validation.
- `../ReportTracker.Web`: Angular 22 standalone client in the sibling repository.
- `database/ReportTracker.sql`: idempotent SQL Server deployment script containing the complete current migration history.

## Configuration

The existing local connection is stored in ASP.NET Core user secrets for this API project and loads automatically in Development. For another machine or deployment, configure its own secret or environment variable. Do not commit database passwords.

```powershell
$env:ConnectionStrings__ReportTracker = 'Server=YOUR_SERVER;Database=ReportTracker;User ID=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True'
```

Local accounts are enabled by default. Passwords use ASP.NET Core's versioned PBKDF2 password hasher. Five failed sign-in attempts lock the account for 15 minutes. Sessions use HttpOnly cookies with an eight-hour absolute lifetime. Cookie-authenticated changes require the antiforgery header supplied by Angular.

Set `EvidenceStorage__Root` to a protected server directory outside the public web root. Configure `Notifications__Email__*` for the bank SMTP relay and `Notifications__Sms__*` for the approved SMS HTTP provider. Keep provider credentials in user secrets or the deployment secret store. Outlook integration is intentionally excluded.

Recurring cycle and notification workers are enabled by default. Set `Automation__Enabled=false` for schema checks, maintenance windows or read-only smoke tests; re-enable it for normal operation.

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

Open `http://localhost:4200`. The default development proxy targets the repository's IIS Express profile at `http://localhost:62232`. When running the API with `dotnet run --launch-profile http` on port `5092`, start the client with `npm run start:http` instead. Restart the Angular development server after changing API profiles because proxy configuration is loaded only at startup.

The first visit shows workspace setup. Create the coordinating division and initial account with a password of at least 12 characters. This account receives **two explicit roles**: System Administrator and Report Coordinator. Initial setup is permitted from a loopback connection in Development. Other environments require `Authentication__SetupKey`; enter it in the setup form. Setup closes permanently once an account exists.

1. Use Administration to create divisions and user accounts. A user optionally receives a division-scoped Report Coordinator role; all active officers can record their assigned tasks.
2. Use Report Setup to create a report definition, including the main deadline time and default component lead days.
3. Use Components and Responsibilities to define mandatory inputs, preparing and receiving divisions, officers, evidence requirements and receipt-confirmation requirements.
4. Create a reporting cycle with exactly one final BB obligation and any internal components that need tracking. Component deadlines cannot be later than the final deadline.
5. Assigned officers record partial or full sending, including actual time, channel and reference. Actual times are entered in Bangladesh time and stored as UTC. Sending history preserves the actor and separate system-entry timestamp.
6. Submission Readiness is derived from the linked components and their sending, evidence and receipt states. Refresh or sign in again to see persisted records. There is no demo-data fallback.

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

Implemented: local sign-in/setup/logout; division and account lifecycle administration; scoped coordinator access; report definitions and component responsibilities; Biweekly and other recurring frequencies; automatic idempotent cycle generation; linked component/final obligations; derived readiness; partial/full sending and history; confirmation-only evidence upload, signature validation, review and protected download; receipt confirmation/dispute; extension request/decision with requester-approver separation; audited status amendments; reminder policies; background email/SMS planning, delivery, retry and monitoring; reporting calendar; management analytics; and responsive role-aware operational screens.

Deployment activities still requiring the bank environment are SMTP/SMS provider credentials and connectivity, a bank-approved malware scanner for uploaded evidence, backup/restore and disaster-recovery exercises, penetration/security testing, production accessibility review, user-acceptance testing and provider-specific SSO. Outlook integration remains intentionally deferred.


