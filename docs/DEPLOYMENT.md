# Deploying for a clinic

One deployment per clinic: its own database, API, staff web, booking portal and branded public site.
Secrets go in environment variables or user-secrets (Development only), never in committed files.
Environment variable names use `__` for sections, e.g. `Payments__WebPayPlus__ApiKeySecret`.

## API (`src/VetManagement.Api`)

| Key | Notes |
|---|---|
| `ConnectionStrings:DefaultConnection` | **Secret.** SQL Server; one database per clinic. Migrations: `dotnet ef database update` (tool in `src/VetManagement.Api/.config`). |
| `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` | **Key is secret.** Startup fails if missing. |
| `AllowedOrigins` | CORS: staff web origin **and** booking portal origin. |
| `BookingPortal:BaseUrl` | Public URL of the booking portal (the API redirects there after payment). |
| `BookingPortal:ApiBaseUrl` | Public URL of this API, used as the WebPay return URL. Defaults to the request host; set it behind a proxy. |
| `BookingPortal:AllowedReturnOrigins` | The clinic's public site(s); the portal only sends clients back to these. |
| `Payments:Provider` | `WebPayPlus`. `Simulated` is refused in Production. |
| `Payments:WebPayPlus:Environment` | `Production` (required in Production) or `Integration` (Transbank sandbox). |
| `Payments:WebPayPlus:CommerceCode`, `ApiKeySecret` | **ApiKeySecret is secret.** Issued by Transbank to the clinic. |
| `Smtp:Host`, `Port`, `User`, `Pass`, `From`, `EnableSsl` | **Pass is secret.** Client emails (confirmation, reminders, changes). |
| `RateLimits:PublicReadPerMinute`, `PublicWritePerMinute` | Public portal limits per IP (defaults 120 / 10). |
| `SeedAdmin:*` | First admin user; disable after first start. **Password is secret.** |
| `Security:*` | HTTPS redirection / HSTS (on by default). |
| `Modules:Enabled` | Modules this clinic has: `Inventory`, `Clients`, `Exams`, `Clinical`, `Scheduling`, `Billing`, `Reports`. Absent = all. A module that is off grants no permissions (not even to Admin) and its public endpoints answer 404. Exams, Clinical, Scheduling and Billing need `Clients`; startup fails otherwise. |
| `TestData:Enabled` | Sample-data page on a non-production demo/staging server. Ignored in Production (always off there). |

Health check: `GET /healthz` (includes the database).

## Business configuration (no deploy needed)

Everything business-related is edited by the clinic admin in the staff web, no deploy needed:
Agenda settings (hours, services, deposit %, refund mode, notifications, branding; starts disabled),
Billing settings (payment methods, discount limit, stock on sale) and Clinical settings (vaccine and
deworming protocols, reminders). See [modules/SCHEDULING.md](modules/SCHEDULING.md),
[BILLING.md](modules/BILLING.md) and [CLINICAL.md](modules/CLINICAL.md).

Permissions are stored in the login token: after an update that adds permissions, users log out and
in again to see the new screens.

## Booking portal (`src/VetManagement.Booking.Web`)

Static Blazor WebAssembly app. Set `wwwroot/appsettings.json` → `ApiBaseUrl` to the API's public URL.
Branding comes from the API at runtime, so the same build serves any clinic.

## Clinic public site (e.g. `sites/SPVetClinic`)

`wwwroot/appsettings.json` → `BookingUrl`. `{returnUrl}` is replaced with the site's address:

```json
{ "BookingUrl": "https://reservas.example.cl/?returnUrl={returnUrl}" }
```

SPVetClinic production still points to its current external booking system until the clinic switches.

## Moving a clinic from the legacy SPLabHybrid app (San Pablo)

The legacy app (EF migration `20250913015650_v0_0_3`, .NET 9 on IIS) has its own schema; this API can't migrate it in
place. The data is copied into a **new** database and the legacy one is left untouched (going back = point IIS at the
old site again). Tested on a copy of the clinic's database of 2026-10-06: every row of users, items, movements, audit
log and exams arrived; legacy passwords keep working.

1. Server: install the **ASP.NET Core 10 Hosting Bundle**, then `iisreset` (it coexists with .NET 9).
2. Backup of the legacy database (`COPY_ONLY`, so the clinic's own backup chain is not affected):
   `sqlcmd -S localhost\SQLEXPRESS -E -Q "BACKUP DATABASE [SPVetClinic] TO DISK=N'...\SPVetClinic_before.bak' WITH COPY_ONLY, INIT"`
3. Schema script, on the dev PC: `dotnet ef migrations script --idempotent --project src/VetManagement.Api -o schema.sql`
4. On the server, create the new database and its schema, then copy the data. **Before the API starts**: on start-up it
   seeds roles, and the copy refuses a database that isn't empty. Both commands need `-I` (filtered indexes).
   ```
   sqlcmd -S localhost\SQLEXPRESS -E -Q "CREATE DATABASE [VetManagement]"
   sqlcmd -S localhost\SQLEXPRESS -E -I -d VetManagement -b -i schema.sql
   sqlcmd -S localhost\SQLEXPRESS -E -I -d VetManagement -b -v Source=SPVetClinic -i deploy\migrate-from-splabhybrid.sql
   ```
   The last command prints source and target counts per table; they must match.
5. Publish the API and staff web to new IIS sites (application pool: *No Managed Code*), with the keys of the table above.
   New `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`; `AllowedOrigins` = the staff web URL. The pool identity needs access to
   the new database (as it had to the old one).
6. Set `Modules:Enabled` to `["Inventory", "Clients", "Exams"]` (what San Pablo uses today).
7. Everyone logs in again. On its first start the API turns the legacy roles into area roles with the same access
   (Employee → the five areas, Manager → the five areas + Supervisor, Laboratorio → Laboratory); then the admin narrows
   each person in Administration → Users. Items arrive with sale price = legacy price and cost 0 (all were 0).
