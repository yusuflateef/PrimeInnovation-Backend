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

On first start, EF creates the `PrimeInnovation` database and seeds demo data.

Or open `PrimeInnovation.sln` in Visual Studio.

- API: http://localhost:5080  
- Swagger: http://localhost:5080/swagger  

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

## Paystack

Set keys in `PrimeInnovation.Api/appsettings.json` under `Paystack`.

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
