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
