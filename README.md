# CareerConnect Backend

ASP.NET Core 8 API using Entity Framework Core with PostgreSQL on Neon.

## Project layout

- `CareerConnect.Api`: the only runnable host; controllers, HTTP pipeline, Swagger, and dependency registration.
- `CareerConnect.Application`: use-case contracts, request/response DTOs, and application services/interfaces.
- `CareerConnect.Domain`: entities, enums, and domain helpers.
- `CareerConnect.Infrastructure`: database access, service implementations, and external integrations.

## Run locally

Configure `ConnectionStrings:DefaultConnection`, `JwtSettings:Key`, `GeminiSettings:ApiKey`, and the required email/Cloudinary settings with .NET User Secrets or environment variables. Keep credentials out of tracked configuration files.

```bash
dotnet run --project CareerConnect.Api
```

## Database migrations

Run Entity Framework commands with `CareerConnect.Api` as the startup project and `CareerConnect.Infrastructure` as the migrations project. The database context is `AppDbContext`.
