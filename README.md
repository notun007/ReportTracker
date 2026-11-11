# ReportTracker

Reporting Tracker records internal handovers and final submissions to configured recipient organizations. It stores tracking metadata, not business report datasets, report files, email bodies or report attachments.

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

Local accounts are enabled by default. Passwords use ASP.NET Core's versioned PBKDF2 password hasher. Five failed sign-in attempts lock the account for 15 minutes. Sessions use HttpOnly cookies with an eight-hour absolute lifetime. Cookie-authenticated changes require the antiforgery header supplied by Angular. The browser uses the application-specific `ReportTracker.XSRF-TOKEN` cookie so other localhost apps cannot overwrite its request token. Sign-in/setup/logout refresh the token first. Only an explicit `csrf_token_invalid` response from the pre-action validation filter triggers one automatic refresh and retry; business validation and authentication failures are not replayed. Restart the API and refresh the client together after updating this token configuration.

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
npm run start:http
```

Open `http://localhost:4200`. The default development proxy targets the repository's IIS Express HTTPS profile at `https://localhost:44332`. When running the API with `dotnet run --launch-profile http` on port `5092`, start the client with `npm run start:http` instead. Restart the Angular development server after changing API profiles because proxy configuration is loaded only at startup.

The first visit shows workspace setup. Create the coordinating division and initial account with a password of at least 12 characters. This account receives **two explicit roles**: System Administrator and Report Coordinator. Initial setup is permitted from a loopback connection in Development. Other environments require `Authentication__SetupKey`; enter it in the setup form. Setup closes permanently once an account exists.

1. Use Administration to create divisions, user accounts, submission recipients and recipient departments. A user optionally receives a division-scoped Report Coordinator role; all active officers can record their assigned tasks.
2. Use Report Setup to create a report definition with a submission recipient, one or more of its departments, a main deadline time and deadline lead days. To configure an existing report, use its **Submission destination** action. A cycle still has one final submission; its destination records the organization and all selected departments. Destination changes affect new cycles and preserve existing cycle and submission records. The directory migration imports existing recipient names and leaves their department selections unset.
3. Use Components and Responsibilities to define mandatory inputs, preparing and receiving divisions, officers, evidence requirements and receipt-confirmation requirements.
4. Create a reporting cycle with exactly one final BB obligation and any internal components that need tracking. Component deadlines cannot be later than the final deadline.
5. Assigned officers record partial or full sending, including actual time, channel and reference. Actual times are entered in Bangladesh time and stored as UTC. Sending history preserves the actor and separate system-entry timestamp.
6. Submission Readiness is derived from the linked components and their sending, evidence and receipt states. Refresh or sign in again to see persisted records. There is no demo-data fallback.

Submission controls show actions for the current user's responsibility. Sending officers (or explicitly scoped recording delegates) upload confirmation evidence. A coordinator with `reports.manage` for that report reviews another officer's evidence; uploaders cannot verify their own proof. Internal receipt confirmation belongs to active officers in the task's receiving division and excludes its assigned senders and officers who recorded its sending events. Those receiving officers can read the incoming task and its history without gaining sending, upload or amendment access. For final BB tasks, a separate report coordinator records the actual BB acknowledgment. Receipt time cannot precede sending time.

Recurring components copy their configured receiving division into each new task. Manual internal tasks requiring receipt confirmation must specify a receiving division. For older internal tasks, a coordinator can use **Manage → Receipt history → Receiving responsibility** to assign it. Existing explicit division GUID recipients are migrated automatically; free-text recipients are not used to guess receipt authority. Evidence and receipt histories remain available to authorized readers, with forms and review buttons shown only when the API grants the corresponding action.

Duplicate report codes, account identifiers and report periods return HTTP 409. Sending updates include the SQL rowversion; a stale or already-completed record cannot be overwritten. The API derives the audit actor from the authenticated principal and enforces assignment or explicit scoped grants.

Report responsibilities distinguish four functions: component preparing division, component receiving division, report coordinating division, and final submitting division. For example, A and B prepare components, C receives them and coordinates the report, and D submits to BB. Coordinating and final submitting divisions may be the same or different. In the API/database, the existing `OwnerDivisionId` remains the coordinating division for compatibility; it is no longer used as the automatic final submitting division.

Create report now includes a common receiving division and an **Add component** side panel. The panel uses the component name, description, preparing division, primary/deputy officers, deadline lead days, display order, mandatory flag and receipt/evidence requirements. Components inherit the report's receiving division and appear as editable/removable drafts before submission. Preparing divisions are summarized from those drafts. One Create report request validates the entire bundle, then persists the report, definition version, submitting assignment, components and audits in a single EF Core SaveChanges transaction; failed validation preserves the client draft and creates no partial report. The separate Components and Responsibilities screen remains available after creation and enforces the common receiving division for new reports. Legacy reports with no common receiver retain their existing per-component assignments.

Set the coordinating division, final submitting division, primary submitting officer and optional deputy in **Report Setup → Create report**. This form is for creation only; an existing-report editing interface is deferred. The responsibilities API remains available for authorized configuration updates. Officers must be active members of the selected final submitting division. Automatic generation uses these explicit assignments and warns/skips a report with missing or invalid assignments instead of choosing an arbitrary officer. Manual final BB tasks must use the configured final submitting division and prefill its configured officers. Responsibility changes affect new cycles; existing task divisions, assigned officers and history are preserved. Migration preserves an existing valid MainSender division when present, otherwise the previous coordinating division is retained as the legacy final submitting division; it does not invent an officer assignment.

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




