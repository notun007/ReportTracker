# ReportTracker

BB Reporting Tracker is an auditable register of internal handovers and Bangladesh Bank submission events. It deliberately does **not** store report datasets, report files, message bodies, or report attachments.

## Projects

- `ReportTracker.Domain` — tracking entities and status vocabulary.
- `ReportTracker.Application` — use-case contracts and DTOs.
- `ReportTracker.Infrastructure` — EF Core 10, SQL Server mappings, and tracking services.
- `ReportTracker.Api` — ASP.NET Core 10 authenticated REST API.
- `ReportTracker.Web` — Angular 22 standalone client.
- `database/ReportTracker.sql` — idempotent SQL Server 2019-compatible deployment script.

## Configure securely

The supplied SQL login is intentionally not committed. Set it in the API process environment:

```powershell
$env:ConnectionStrings__ReportTracker = 'Server=YOUR_SERVER;Database=ReportTracker;User ID=YOUR_USER;Password=YOUR_PASSWORD;MultipleActiveResultSets=True;TrustServerCertificate=True;Connect Timeout=120'
$env:Authentication__Authority = 'https://your-identity-provider/'
$env:Authentication__Audience = 'report-tracker-api'
```

For local-only development, ASP.NET Core user-secrets are also suitable. Rotate any credential that has been shared in chat before production use.

## Create or upgrade the database

Review and run `database/ReportTracker.sql` against SQL Server 2019. It is idempotent and records both EF migrations. The script creates the `tracker` schema, 31 domain tables, concurrency columns, foreign keys, and deduplication indexes. It does not create report-data or report-file storage.

To regenerate the script after model changes:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations script --idempotent --project src/ReportTracker.Infrastructure --startup-project src/ReportTracker.Api --output database/ReportTracker.sql
```

## Run

```powershell
dotnet run --project src/ReportTracker.Api
cd src/ReportTracker.Web
npm install
npm start
```

Open `http://localhost:4200`. The Angular development proxy targets `http://localhost:5092`. Until an access token is integrated, the client falls back to clearly isolated demo tracking rows so the UI workflow remains reviewable; the API itself requires JWT authentication.

## Verify

```powershell
dotnet build ReportTracker.slnx
dotnet test ReportTracker.slnx --no-build
cd src/ReportTracker.Web
npm run build
```

## Security and scope notes

- Configure an approved OIDC/SSO authority before deployment.
- Store confirmation/permission documents in protected object storage; `EvidenceDocuments` stores metadata and a storage key only.
- Validate evidence purpose and content operationally; extensions alone do not establish that a document is safe.
- Use UTC in persistence and Asia/Dhaka for local deadlines/reminders.
- Do not enable automatic database migration in production; apply the reviewed script through change control.
# ReportTracker
