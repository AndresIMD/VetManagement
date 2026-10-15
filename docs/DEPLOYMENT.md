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
| `Proxy:ForwardLimit` | Number of proxies in front of the API (cloud front ends, CDN). Client IPs (rate limits) and the scheme (HTTPS redirect) are then read from `X-Forwarded-For` / `-Proto`. Azure App Service = `1`; App Service behind Cloudflare's proxy (orange cloud) = `2`. Unset on a server in the clinic's own network. Too low: the whole clinic shares one login limit; too high: a client can fake its IP. |
| `Modules:Enabled` | Modules this clinic has: `Inventory`, `Clients`, `Exams`, `Clinical`, `Scheduling`, `Billing`, `Reports`. Absent = all. A module that is off grants no permissions (not even to Admin) and its public endpoints answer 404. Exams, Clinical, Scheduling and Billing need `Clients`; startup fails otherwise. |
| `TestData:Enabled` | Sample-data page on a non-production demo/staging server. Ignored in Production (always off there). |

Health check: `GET /healthz` (includes the database).

## Business configuration (no deploy needed)

Everything business-related is edited by the clinic admin in the staff web, no deploy needed:
Agenda settings (hours, services, deposit %, refund mode, notifications, branding; starts disabled),
Billing settings (payment methods, discount limit, stock on sale, VAT), Clinical settings (vaccine and
deworming protocols, reminders, client portal) and Exams → Report Design (branding and templates of lab reports).
See [SCHEDULING.md](modules/SCHEDULING.md), [BILLING.md](modules/BILLING.md), [CLINICAL.md](modules/CLINICAL.md)
and [EXAM_REPORTS.md](modules/EXAM_REPORTS.md).

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

## Hosting on Azure

One set of resources per clinic, on subdomains of the clinic's own domain (e.g. `app.` staff web, `api.` API,
`reservas.` booking portal). The clinic's values (resource names, domains, modules) are kept in the clinic's own
private repository, not here.

| Piece | Azure service | Notes |
|---|---|---|
| Database | Azure SQL Database | Free offer or Basic is enough to start; 7-day point-in-time restore included. Firewall: allow Azure services. |
| API | App Service (Linux, .NET 10), B1 or higher | Free/Shared tiers can't bind a custom domain with HTTPS. Turn **Web sockets** on (SignalR) and **Always on** (reminder and alert jobs run in the API). Health check path: `/healthz`. |
| Staff web, booking portal | Static Web Apps (Free) | `wwwroot/staticwebapp.config.json` routes every page to `index.html`. |

1. Create the database, App Service and Static Web App.
2. App Service → Environment variables: the keys of the API table above (`ConnectionStrings__DefaultConnection`,
   `Jwt__Key`, `AllowedOrigins__0`, `Modules__Enabled__0`, ...), `ASPNETCORE_ENVIRONMENT=Production` and
   `Proxy__ForwardLimit`. The first start creates the schema (migrations run on start-up) and the `SeedAdmin` user.
3. GitHub → Settings → Environments → one per clinic, with the variables and secrets listed at the top of
   `.github/workflows/deploy-azure.yml`; then Actions → **Deploy to Azure** → Run workflow with that environment. It runs
   the tests, publishes the API and the staff web, and writes the staff web's `config.json` (`ApiBaseUrl`).
4. Custom domains, DNS at Cloudflare: a `CNAME` per subdomain to the Azure default host name, plus the `TXT asuid.<sub>`
   record App Service asks for. Keep the records **DNS only (grey cloud)** until Azure has validated the domain and issued
   its certificate; with the proxy on afterwards, use SSL mode **Full (strict)** and set `Proxy:ForwardLimit` to 2.
5. Existing data: Azure SQL can't restore a `.bak`. Export the database as a `.bacpac` (SSMS → Tasks → Export Data-tier
   Application, or `SqlPackage /a:Export`) and import it into Azure SQL **before** the API's first start. Rehearse on a
   copy while the clinic keeps working on its current server.

A hosted system needs the internet: with no connection at the clinic there's no system, unlike a server on its network.

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
