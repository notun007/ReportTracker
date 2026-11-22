# ReportTracker

Reporting Tracker records internal handovers and final submissions to configured recipient organizations. It stores tracking metadata, not business report datasets, report files, email bodies or report attachments.

## Projects

- `src/ReportTracker.Domain`: tracking entities and status vocabulary.
- `src/ReportTracker.Application`: contracts, validation and deadline rules.
- `src/ReportTracker.Infrastructure`: EF Core 10, Oracle 19c and scoped tracking services.
- `src/ReportTracker.Api`: ASP.NET Core 10 REST API, local sessions and optional SSO bearer validation.
- `../ReportTracker.Web`: Angular 22 standalone client in the sibling repository.
- `database/ReportTracker.Oracle.sql`: Oracle 19c baseline deployment script using `rt_`-prefixed snake-case identifiers.
- `database/ReportTracker.sql`: retained historical SQL Server deployment script; do not apply it to Oracle.

## Configuration

Configure `ConnectionStrings:Oracle` in ASP.NET Core user secrets for this API project (loaded automatically in Development), or set `ConnectionStrings__Oracle` in the API process environment. A legacy `ConnectionStrings:ReportTracker` SQL Server secret is not used by the Oracle provider. A working connection in SQL Developer or SQL*Plus does not configure the API's connection. Do not commit database passwords.

```powershell
$env:ConnectionStrings__Oracle = 'User Id=YOUR_SCHEMA;Password=YOUR_PASSWORD;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=YOUR_HOST)(PORT=1521))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=YOUR_SERVICE)));Pooling=true;Connection Timeout=60;'
```

Local accounts are enabled by default. Passwords use ASP.NET Core's versioned PBKDF2 password hasher. Five failed sign-in attempts lock the account for 15 minutes. Sessions use HttpOnly cookies with an eight-hour absolute lifetime. Cookie-authenticated changes require the antiforgery header supplied by Angular. The browser uses the application-specific `ReportTracker.XSRF-TOKEN` cookie so other localhost apps cannot overwrite its request token. Sign-in/setup/logout refresh the token first. Only an explicit `csrf_token_invalid` response from the pre-action validation filter triggers one automatic refresh and retry; business validation and authentication failures are not replayed. Restart the API and refresh the client together after updating this token configuration.

Set `EvidenceStorage__Root` to a protected server directory outside the public web root. Configure `Notifications__Email__*` for the bank SMTP relay and `Notifications__Sms__*` for the approved SMS HTTP provider. Keep provider credentials in user secrets or the deployment secret store. Outlook integration is intentionally excluded.

Recurring cycle and notification workers are enabled by default. Set `Automation__Enabled=false` for schema checks, maintenance windows or read-only smoke tests; re-enable it for normal operation.

## Create or upgrade the database

Have the DBA create the Oracle schema with the required object privileges and tablespace quota, then apply `database/ReportTracker.Oracle.sql` while connected as that schema. Supply credentials through an approved secret store and never commit them.

The application owns 37 `rt_`-prefixed domain tables plus the EF migration ledger `rt_ef_migrations_history`. Domain tables and columns use quoted lower-snake-case Oracle identifiers.

```powershell
sqlplus YOUR_SCHEMA@YOUR_SERVICE @database/ReportTracker.Oracle.sql
```

To regenerate the deployment script:

```powershell
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet tool run dotnet-ef migrations script 0 RenameAssignmentAssignees --project src/ReportTracker.Infrastructure --startup-project src/ReportTracker.Api --output database/ReportTracker.Oracle.sql
```

Do not apply migrations automatically during application startup.

### Assignee terminology upgrade

Deploy the API, Angular client and Oracle schema together. The API now uses `primaryAssigneeId` / `alternateAssigneeId` (and matching display-name fields), plus `primarySubmittingAssigneeId` / `alternateSubmittingAssigneeId` for final-submission defaults. The responsibilities endpoint is `/api/report-definitions/{reportId}/responsibility-assignments`; its division sections expose `assignees`.

For an existing Oracle schema at `PreventDuplicateReportCycleDates`, stop the API and its automation workers, then apply `database/20261121_RenameAssignmentAssignees.Oracle.sql` using the schema's approved credentials. The migration renames the six assignment columns in components, period tasks and responsibility history, preserving their values and nullability. It changes the reminder recipient token `Deputy` to `Alternate`; the planner also accepts the legacy token during upgrade. Audit JSON and contributor-role snapshots remain unchanged. Restart the updated API and refresh the Angular client after migration. Do not run the full baseline script against an existing schema.

New schemas use the updated `database/ReportTracker.Oracle.sql`. Historical migration sources and the former SQL Server script are retained unchanged. EF's `RenameAssignmentAssignees` migration includes the reverse column renames for rollback; roll back the API/client together if reverting the schema.


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

If Oracle cannot be reached, the session check returns HTTP 503 with code `database_unavailable`, safe problem details and a `Retry-After` header. The startup screen displays **Database unavailable** and a **Retry connection** action. A session check that does not finish within 30 seconds displays **Connection timed out** instead of leaving the loading screen indefinitely. An unavailable database never opens first-time workspace setup. Oracle details remain in server logs with a trace identifier; the browser does not receive connection strings, host details or raw Oracle exceptions. Restart the API and refresh the frontend together after updating this handling.

Canceled requests are handled separately from database outages. If the browser has disconnected or canceled its request, the API stops without writing another response (499 for server-side status tracking). If request execution is canceled while the browser remains connected, the API returns HTTP 408 with code `request_timed_out` and a safe message asking the user to refresh and check the latest status before trying again. Save operations are not automatically replayed. Visual Studio may pause before exception handling runs, depending on debugger exception settings; continue execution to let the request handler finish. Rebuild and restart the API after changing its middleware.

The first visit shows workspace setup. Create the coordinating division and initial account with a password of at least 12 characters. This account receives **two explicit roles**: System Administrator and Report Coordinator. Initial setup is permitted from a loopback connection in Development. Other environments require `Authentication__SetupKey`; enter it in the setup form. Setup closes permanently once an account exists.

1. Use Administration to create divisions, user accounts, submission recipients and recipient departments. A user optionally receives a division-scoped Report Coordinator role; all active officers can record their assigned tasks.
2. Use Report Setup to create a report definition with a submission recipient, one or more of its departments, a main deadline time and deadline lead days. To configure an existing report, use its **Submission destination** action. A cycle still has one final submission; its destination records the organization and all selected departments. Destination changes affect new cycles and preserve existing cycle and submission records. The directory migration imports existing recipient names and leaves their department selections unset.
3. Use Components and Responsibilities to define mandatory inputs, preparing and receiving divisions, officers, evidence requirements and receipt-confirmation requirements.
4. Create a reporting cycle with exactly one final BB obligation and any internal components that need tracking. Component deadlines cannot be later than the final deadline.
5. Assigned officers record partial or full sending, including actual time, channel and reference. Actual times are entered in Bangladesh time and stored as UTC. Sending history preserves the actor and separate system-entry timestamp.
6. Submission Readiness is derived from all required linked components and their sending, evidence and receipt states, including components prepared by other divisions. Main Report Submission keeps every authorized period visible and lists pending component statuses. Its submission action stays disabled until all required components are sent, required receipts are confirmed and required evidence is verified; the API also enforces this rule. Optional, cancelled and not-applicable components do not block submission. Refresh or sign in again to see persisted records. There is no demo-data fallback.

Submission controls show actions for the current user's responsibility. Sending officers (or explicitly scoped recording delegates) upload confirmation evidence. A coordinator with `reports.manage` for that report reviews another officer's evidence; uploaders cannot verify their own proof. Internal receipt confirmation requires an explicit, currently effective receiving-officer assignment for that report and the task's receiving division. The officer must remain an active member of that division; preparing contributors and officers who recorded its sending events cannot confirm their own handover. Named receiving officers can read the incoming task and its history without gaining sending, upload or amendment access. For final BB tasks, a separate report coordinator records the actual BB acknowledgment. Receipt time cannot precede sending time.

Component contributors (Lead Preparer, Preparer, Backup Preparer and Reviewer) can read their assigned period components, sending/receipt history and confirmation evidence. Contributor assignments also appear in My Pending and the assigned-to-me filter. Division membership alone does not grant preparing or receiving visibility. Contributor-based visibility does not grant sending, evidence upload, receipt confirmation, evidence verification or amendment authority; existing primary/deputy assignments and explicit recording grants still determine writing access. The contributor Reviewer role does not replace the report coordinator's evidence-review permission. Existing coordinator and explicitly scoped read permissions remain separate access paths. Accounts must be active.

Coordinators select named receiving officers under **Components and Responsibilities → Receiving officers**, or **Manage → Receipt history → Receiving officers** for a task, including legacy reports with different receiving divisions. Assignments apply to all internal components for that report routed to the selected division, including existing and future periods. Saving an empty selection revokes receiving access; previous assignments and receipt history are retained. The endpoint requires the report's scoped `reports.manage` permission, validates active division members and checks a concurrency token. It stores dated `ComponentReceiver` records in the existing responsibility-assignment table, so no schema migration is needed. Existing division membership is not automatically converted to a receiving assignment. After upgrading, coordinators must explicitly designate receivers before they can confirm internal receipts. **Component Submission** shows assigned preparing work; **Incoming Components** shows explicitly assigned receiving work.

**Report Configuration → Responsibilities**, immediately after **Cycles**, manages the coordinating, receiving and final submitting responsibilities. Each division has one required **Primary Assignee** and a different optional **Alternate Assignee**. All three pairs save together with audit history and a concurrency check. Existing cycles, component assignments and account permissions are preserved. Receiver pairs use the existing receiving assignments; teams larger than two must be reviewed before using the pair form.

**Component Teams** is the dedicated team-entry screen. It provides one reusable team-member and roles entry line. Save member, Update member and Remove persist immediately through per-member endpoints; there is no temporary team draft or separate Save team step. Each successful write is followed by a directory GET, and the grid displays only the retrieved saved team. Per-member updates preserve other currently stored members. A rejected write preserves the entry. If a write succeeds but its readback fails, the stale grid is hidden and editing stays blocked until Refresh teams succeeds; refreshing does not repeat the write. Existing contributor roles, multi-role selections and team validation are unchanged. A division-scoped Report Coordinator can manage teams only for components prepared by their own active division, including components of reports coordinated elsewhere; report-specific grants remain enforced. Explicit global report managers retain cross-division management. Ordinary assigned preparers do not receive team-management authority automatically. The directory returns only authorized current-version components and active members of their preparing divisions. Component definition screens retain read-only team summaries and no longer contain team-entry controls. Components may be defined before their team is entered; cycle generation continues to reject incomplete preparing assignments. Team saves retain the existing audit and period-snapshot behavior. No database schema migration or automatic account-role changes are needed.

Access uses the contributor snapshot saved on each period's task, not the current component definition. Changing contributors for future cycles does not change access to existing periods. Historical tasks without a contributor snapshot retain their existing primary/deputy access; no contributors are inferred from today's defaults and no automatic data backfill is performed. This authorization change requires no database migration.

Recurring components copy their configured receiving division into each new task. Manual internal tasks requiring receipt confirmation must specify a receiving division. For older internal tasks, a coordinator can use **Manage → Receipt history → Receiving responsibility** to assign it. Existing explicit division GUID recipients are migrated automatically; free-text recipients are not used to guess receipt authority. Evidence and receipt histories remain available to authorized readers, with forms and review buttons shown only when the API grants the corresponding action.

Duplicate report codes, account identifiers and report periods return HTTP 409. Sending updates include an application-generated Oracle `RAW(8)` concurrency token; a stale or already-completed record cannot be overwritten. The API derives the audit actor from the authenticated principal and enforces assignment or explicit scoped grants.

Report responsibilities distinguish four functions: component preparing division, component receiving division, report coordinating division, and final submitting division. For example, A and B prepare components, C receives them and coordinates the report, and D submits to BB. Coordinating and final submitting divisions may be the same or different. In the API/database, the existing `OwnerDivisionId` remains the coordinating division for compatibility; it is no longer used as the automatic final submitting division.

Create report now includes a common receiving division and an **Add component** side panel. The panel uses the component name, description, preparing division, primary/deputy officers, deadline lead days, display order, mandatory flag and receipt/evidence requirements. Components inherit the report's receiving division and appear as editable/removable drafts before submission. Preparing divisions are summarized from those drafts. One Create report request validates the entire bundle, then persists the report, definition version, submitting assignment, components and audits in a single EF Core SaveChanges transaction; failed validation preserves the client draft and creates no partial report. The separate Components and Responsibilities screen remains available after creation and enforces the common receiving division for new reports. Legacy reports with no common receiver retain their existing per-component assignments.

Select the coordinating, receiving and final submitting divisions in **Report Configuration → Create**. Reports can be created before assignees are known. Assign the primary and optional alternate later under **Responsibilities**. Assignees must be active members of the corresponding division. Automatic cycle generation uses the final submitting pair and warns/skips a report with missing or invalid assignments. New manual cycles prefill the saved final submitting assignees. Changes affect future cycles; existing task assignments and history are preserved.

## Future bank SSO

The API already accepts bearer tokens through a separate authentication scheme. When the bank documentation is available, configure:

```powershell
$env:Authentication__Authority = 'https://YOUR_BANK_AUTHORITY/'
$env:Authentication__Audience = 'YOUR_API_AUDIENCE'
```

The provider must issue a validated `user_id` claim containing the GUID of an active tracker user, or an explicit subject-to-user mapping must be implemented. Identity, role and scope grants remain in the tracker database. The browser authorization-code/PKCE flow still needs provider-specific integration. After SSO is verified, set `Authentication__LocalEnabled=false` to disable local login and bootstrap. Existing local sessions expire within eight hours.

Use a same-origin HTTPS reverse proxy for the deployed client and API. Persist and protect ASP.NET Core Data Protection keys across deployments; multi-instance deployments must share the key ring and application name. Configure a bank-approved secret store and a least-privilege Oracle schema account.

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





## Roles and scoped access

Administration has three separate screens: **Users** for account maintenance, **Role Assignments** for assigning existing roles and scopes, and **Roles** for role permission configuration. The read-only permission catalogue is available within Roles. These screens retain the existing administrator permissions; no additional administrator roles are required. Users can receive multiple roles through a simple role checklist. The role-mapping screen accepts only selected role IDs; responsibilities and component-team assignments remain separate. The Users screen creates accounts without role assignments; assign access afterwards through Role Assignments. Accounts without roles receive no operational access. User profile changes and role/permission changes are audited. Effective permissions show each grant's scope and source roles.

| Stable code | Default purpose |
| --- | --- |
| SYS_ADMIN | Accounts, divisions, access administration, reference data and global automation |
| REPORT_ADMIN | Report configuration, cycles, responsibilities, preparing-division teams, monitoring and review |
| REPORT_PREPARER | View and record assigned components; upload evidence where recording is permitted |
| COMPONENT_RECEIVER | View and receive components covered by a current named receiving assignment |
| FINAL_SUBMITTER | View and record assigned final submissions after readiness checks |

A division membership or operational role alone does not expose every component. Existing contributor rules, period snapshots, receiving routes, and self-review restrictions continue to apply. Preparing-division team management additionally requires actual membership of that division, except for an explicit Global team-management grant. System administration does not implicitly grant business access. Permission and role revocations are checked from the database on each request; a browser session refresh updates displayed menus.

For an existing Oracle installation, stop the API, back up the database, apply `ScopedAccessManagement` (or `database/20261121_ScopedAccessManagement.Oracle.sql`), then run the API once with `--migrate-access` and the deployment's connection configuration/content root. Restart the API after both steps succeed. The one-time data upgrade preserves existing role IDs and scopes, expands old broad management grants into granular permissions, and derives report-scoped operational roles from existing assignments. It never changes saved component teams, period tasks or responsibility assignments. An audit marker prevents a later rerun from restoring revoked permissions. New installations initialize the catalogue during first-owner setup.

Rollback requires a coordinated database backup restore and the previous API deployment: dropping the new active flags alone would discard revocation history. Never roll back only the schema while serving requests.

## Sidebar navigation

The sidebar has two levels: Overview, Report Administration, Submissions, Monitoring, Access Management, and System Configuration. It shows only permitted destinations and hides empty groups. One group expands at a time; the active screen and its parent are highlighted. Group and selected-screen state are stored per user in browser local storage and restored on refresh, with the saved destination checked against current permissions. A fresh sign-in starts at Reporting Overview. The collapsed desktop rail opens the selected group; mobile navigation closes after selection or dismissal. System Configuration opens each reference-data screen directly, while Users, Role Assignments, and Roles remain separate destinations. SVG icons share glossy group-colored tiles without adding image assets or icon-library dependencies.

Role Assignments provides a searchable user table with a glossy slide-out role checklist. The table, filters and scroll position remain in place while the modal panel is open. The panel fills the screen on mobile, traps focus, restores focus when closed, and protects unsaved changes on Cancel, Escape or backdrop dismissal. Its save controls remain fixed while roles scroll. Existing roles are preselected; checking and unchecking roles changes the draft until Save assignments is clicked. GET/PUT /api/access/users/{userId}/roles expose only role IDs, role metadata and a concurrency token. No division, report or scope controls are used. Saved mappings are retrieved again before the success toast. Existing retained bindings are preserved, revoked bindings remain in history, and stale edits and removal of the last access administrator are rejected. No schema migration or automatic conversion of existing grants is required.
