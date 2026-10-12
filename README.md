# Prime Innovation Network API

Clean Architecture backend for Prime Innovation Network / Prime Digital Academy.

## Solution structure

```
backend/
├── PrimeInnovation.sln
├── PrimeInnovation.Api/                 # Presentation (controllers, Program.cs)
└── src/
    ├── PrimeInnovation.Domain/          # Entities
    ├── PrimeInnovation.Application/     # DTOs, interfaces, mappers
    └── PrimeInnovation.Infrastructure/  # EF Core, JWT, Paystack, seeding
```

### Controllers (one file each)

`Auth`, `Courses`, `Enrollments`, `Payments`, `Learn`, `Dashboard`, `Certificates`, `Instructor`, `Content`

## Run

Requires local **SQL Server Express** (`localhost\SQLEXPRESS`). Connection string is in `PrimeInnovation.Api/appsettings.json`.

```bash
cd backend
dotnet restore PrimeInnovation.sln --configfile NuGet.Config
dotnet run --project PrimeInnovation.Api --launch-profile http
```

On start, the API applies EF Core migrations; in Development (`Seed:Enabled`) it also seeds demo data into an empty database.

Or open `PrimeInnovation.sln` in Visual Studio.

- API: http://localhost:5080  
- Swagger: http://localhost:5080/swagger (Development only)  
- Health: http://localhost:5080/health  

### Migrations

```bash
dotnet ef migrations add <Name> -p src/PrimeInnovation.Infrastructure -s PrimeInnovation.Api -o Persistence/Migrations
```

A database created before migrations were introduced (via `EnsureCreated`) must be dropped once:
`dotnet ef database drop -p src/PrimeInnovation.Infrastructure -s PrimeInnovation.Api`.

### Secrets

Nothing secret lives in `appsettings.json`. Locally use user-secrets; in production use environment variables
(full list in [`deploy/.env.example`](deploy/.env.example)).

```bash
dotnet user-secrets set "Paystack:SecretKey" "sk_test_..." --project PrimeInnovation.Api
dotnet user-secrets set "Paystack:PublicKey" "pk_test_..." --project PrimeInnovation.Api
```

Development uses a dev-only JWT key from `appsettings.Development.json`. Outside Development the API refuses to start
unless `Jwt__Key` is a unique value of at least 32 bytes.

### SQL Server connection

Default (Windows auth):

```
Server=localhost\SQLEXPRESS;Database=PrimeInnovationDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

For SQL login instead, use:

```
Server=localhost\SQLEXPRESS;Database=PrimeInnovationDb;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true
```

## Demo accounts

| Role | Email | Password |
|------|-------|----------|
| Student | alex@example.com | Student@123 |
| Instructor | ada@primedigitalacademy.com | Instructor@123 |
| Admin | admin@primeinnovation.network | Admin@12345 |

Demo data is seeded only when `Seed:Enabled` is true (Development). Public sign-up always creates Students;
promote instructors/admins directly in the `Users` table.

## Paystack

Set `Paystack:SecretKey` / `Paystack:PublicKey` via user-secrets or environment variables (see above).

Register the webhook in the Paystack dashboard so buyers are enrolled even if they close the tab after paying:
`https://<api-domain>/api/payments/paystack/webhook` (signature-verified with your secret key).

## Google sign-in

Disabled unless `Google:ClientId` (OAuth Web Client ID) is set. `POST /api/auth/google` takes `{ "idToken": "..." }`
and verifies it with Google before signing the user in.

## Deployment

Runs as Docker containers built by CI (`.github/workflows/ci.yml`):

- **PRs:** build, build the `promix-api` and `promix-migrator` images, run `scripts/smoke-test.sh`
  (throwaway SQL Server → migrations → API `/health`).
- **main:** same, then push both images to `ghcr.io/yusuflateef/` tagged with the commit SHA and `latest`.

On the server (`/opt/prod-app/backend/`), copy the files from [`deploy/`](deploy/):

| File | Purpose |
|------|---------|
| `docker-compose.yml` | `api` (host networking, `127.0.0.1:8080`, uploads volume) and on-demand `migrator` |
| `.env` (from `.env.example`, `chmod 600`) | Secrets: connection string, JWT key, Paystack, notifications |
| `deploy.sh` | `./deploy.sh <tag>`: pull → migrate → restart → `/health` → roll back API on failure |

Migrations run as a separate step (the API runs with `Database__MigrateOnStartup=false`). Rollback restores the
previous API image only, so every migration must remain compatible with the previous release.

Local image test: `docker build -t promix-api:local . && docker build --target migrator -t promix-migrator:local .`
then `bash scripts/smoke-test.sh promix-api:local promix-migrator:local`.

## Email (SMTP) + SMS (Termii — Nigeria)

```json
"Email": {
  "Enabled": true,
  "Host": "smtp.gmail.com",
  "Port": 587,
  "Username": "...",
  "Password": "...",
  "FromEmail": "noreply@primeinnovation.network",
  "AdminEmail": "hello@primeinnovation.network"
},
"Termii": {
  "ApiKey": "your-termii-api-key",
  "SenderId": "PrimePIN",
  "Channel": "generic",
  "BaseUrl": "https://api.ng.termii.com"
}
```

Get a Termii key at https://accounts.termii.com — used for welcome, password reset, and enrollment SMS.
