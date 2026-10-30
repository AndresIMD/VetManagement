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
| `RateLimits:UserPerMinute` | Requests per signed-in user per minute (default 100; development 1000 per 5 minutes). |
| `RateLimits:LoginPerMinute` | Sign-in attempts per IP per minute (default 10), on top of the account lockout after 5 failures. |
| `SeedAdmin:*` | First admin user; disable after first start. **Password is secret.** |
| `Security:*` | HTTPS redirection / HSTS (on by default). |
| `Proxy:ForwardLimit` | Number of proxies in front of the API (cloud front ends, CDN). Client IPs (rate limits) and the scheme (HTTPS redirect) are then read from `X-Forwarded-For` / `-Proto`. Azure App Service = `1`; App Service behind Cloudflare's proxy (orange cloud) = `2`. Unset on a server in the clinic's own network. Too low: the whole clinic shares one login limit; too high: a client can fake its IP. |
| `Modules:Enabled` | Modules this clinic has: `Inventory`, `Clients`, `Exams`, `Clinical`, `Scheduling`, `Billing`, `Reports`. Absent = all. A module that is off grants no permissions (not even to Admin) and its public endpoints answer 404. Exams, Clinical, Scheduling and Billing need `Clients`; startup fails otherwise. |
| `Localization:Culture` | The clinic's language for what the API writes to people: validation errors, client emails, defaults of new settings (payment methods, vaccine protocols, report footer). E.g. `es-CL`. Absent = English. Set it to the same culture as the staff web's `Culture`. Translations: `Domain/Resources/Texts.{language}.resx`. |
| `TestData:Enabled` | Sample-data page on a non-production demo/staging server. Ignored in Production (always off there). |

Health check: `GET /healthz` (includes the database).

### Keeping .NET patched

A package update doesn't patch a running clinic. The API runs on the ASP.NET Core runtime installed on the server, so a
.NET security release needs its Hosting Bundle (or runtime) installed there as well. The staff web and the booking portal
carry the browser runtime of the SDK that built them, so update the SDK on the machine that builds the packages too.

## Business configuration (no deploy needed)

Everything business-related is edited by the clinic admin in the staff web, no deploy needed:
Agenda settings (hours, services, deposit %, refund mode, notifications, branding; starts disabled),
Billing settings (payment methods, discount limit, stock on sale, VAT), Clinical settings (vaccine and
deworming protocols, reminders, client portal) and Exams → Report Design (branding and templates of lab reports).
See [SCHEDULING.md](modules/SCHEDULING.md), [BILLING.md](modules/BILLING.md), [CLINICAL.md](modules/CLINICAL.md)
and [EXAM_REPORTS.md](modules/EXAM_REPORTS.md).

Permissions are stored in the login token: after an update that adds permissions, users log out and
in again to see the new screens.

## Staff web and desktop app (`src/VetManagement.Staff.Web`, `src/VetManagement.Staff.Maui`)

`config.json` (template: `config.template.json`):

| Key | Notes |
|---|---|
| `ApiBaseUrl` | Public URL of the API (or `LocalApiBaseUrl` + `CloudApiBaseUrl`: the local one is used when it answers). |
| `Culture` | The clinic's language and date/number formats, e.g. `es-CL`. Absent or unknown = English. Texts are translated in `Staff.UI/Resources/Strings.{language}.resx` (English key → translation); a country file such as `Strings.es-CL.resx` holds only the terms that differ (RUT for the tax ID). A text without translation shows in English. Printed exam reports use the same texts. |

## Booking portal (`src/VetManagement.Booking.Web`)

Static Blazor WebAssembly app. Set `wwwroot/appsettings.json` → `ApiBaseUrl` to the API's public URL.
Branding comes from the API at runtime, so the same build serves any clinic.

## Clinic public site

Each clinic's branded website lives in the clinic's own repository, not here. Its booking buttons open the booking
portal with the site's address as `returnUrl`, so the portal can send clients back after booking (only to the origins in
`BookingPortal:AllowedReturnOrigins`). A Blazor site can read the link from `wwwroot/appsettings.json` and replace
`{returnUrl}` with its own address:

```json
{ "BookingUrl": "https://reservas.example.cl/?returnUrl={returnUrl}" }
```

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
   `Jwt__Key`, `AllowedOrigins__0`, `Modules__Enabled__0`, `Localization__Culture`, ...), `ASPNETCORE_ENVIRONMENT=Production` and
   `Proxy__ForwardLimit`. The first start creates the schema (migrations run on start-up) and the `SeedAdmin` user.
   The connection string uses the API's own database user ([below](#the-apis-database-user)), not the server admin.
3. GitHub → Settings → Environments → one per clinic, with the variables and secrets listed at the top of
   `.github/workflows/deploy-azure.yml`; then Actions → **Deploy to Azure** → Run workflow with that environment. It runs
   the tests, publishes the API and the staff web, and writes the staff web's `config.json` (`ApiBaseUrl`, and `Culture` from the optional `STAFF_CULTURE` variable).
   It also narrows the page's Content-Security-Policy so the browser connects only to that API (`connect-src`); the
   repository's `index.html` allows any HTTPS site and localhost, for development.
   The run logs show the clinic's resource names and URLs, and this repository is public: a clinic's private repository
   can hold the environment and call this workflow instead (`workflow_call`, see the comment at its top), so its logs
   and secrets stay there.
4. Custom domains, DNS at Cloudflare: a `CNAME` per subdomain to the Azure default host name, plus the `TXT asuid.<sub>`
   record App Service asks for. Keep the records **DNS only (grey cloud)** until Azure has validated the domain and issued
   its certificate; with the proxy on afterwards, use SSL mode **Full (strict)** and set `Proxy:ForwardLimit` to 2.
5. Existing data: Azure SQL can't restore a `.bak`. Export the database as a `.bacpac` (SSMS → Tasks → Export Data-tier
   Application, or `SqlPackage /a:Export`) and import it into Azure SQL **before** the API's first start. Rehearse on a
   copy while the clinic keeps working on its current server.

A hosted system needs the internet: with no connection at the clinic there's no system, unlike a server on its network.

### The API's database user

The server admin can do anything on the server; the API only needs its own database. Connected to the clinic's
database as the admin (e.g. the portal's Query editor), create a user for the API. Migrations run when the API starts,
so besides reading and writing rows it changes the schema (`db_ddladmin`); it can't manage users or permissions.

```sql
CREATE USER [vetmanagement_api] WITH PASSWORD = '<long random password>', DEFAULT_SCHEMA = dbo;
ALTER ROLE db_datareader ADD MEMBER [vetmanagement_api];
ALTER ROLE db_datawriter ADD MEMBER [vetmanagement_api];
ALTER ROLE db_ddladmin ADD MEMBER [vetmanagement_api];
```

Then `ConnectionStrings__DefaultConnection` uses `User ID=vetmanagement_api;Password=...`, and the admin's password
stays out of the app's settings. Every migration applies with these roles.

### Checks after the first deploy

- App Service → Configuration: **Always on** and **Web sockets** on, **HTTPS only** on, minimum TLS 1.2. FTP basic
  authentication can be off; SCM basic authentication stays on (the deploy's publish profile uses it).
- Azure SQL → Networking: delete the firewall rules of the PCs used to import the data. *Allow Azure services* lets any
  service hosted on Azure, of any customer, reach the server (it still needs a password); to narrow it, allow only the
  App Service's outbound IP addresses (App Service → Properties), which change if the plan's tier changes.
- Azure SQL → Metrics: *DTU percentage* over a busy week; Basic (5 DTU) staying near 100% means moving up to S0.
- Cost Management → Budgets: an alert on the clinic's resource group.

## Moving a clinic from another app

Its data is copied into a **new** database and the old one is left untouched. The copy runs once the schema exists and
**before the API's first start**: on start-up the API seeds roles, which would clash with the copied ones. The copy
script depends on the app the clinic comes from, so it lives with that clinic's deployment, in its private repository.
On Azure SQL, which can't restore a `.bak`, make the copy on another server and import it as a `.bacpac` (step 5 of
[Hosting on Azure](#hosting-on-azure)). Users with the roles `Manager`, `Employee` or `Laboratorio` get area roles with
the same access on the API's first start (`DataSeeder.LegacyRoles`); the admin then narrows each person in
Administration → Users.
