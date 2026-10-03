# VetManagement

A CRM for veterinary clinics built with .NET 10: one generic staff application sold to many clinics, plus an
online booking portal and a branded public website per clinic. Every business policy (agenda rules, deposits,
refunds, payment methods, taxes, vaccine protocols, reminders) is configured per clinic by its administrator.

## What it does

| Area | Highlights |
|---|---|
| **Agenda** | Vets and rooms with weekly hours, holidays and absences, overbooking, progressive release ("sobrecupo"), email confirmations and reminders |
| **Online booking** | Hosted portal per clinic (branding, RUT validation), deposits via WebPay Plus, cancellation link, refund policy per clinic |
| **Clinical record** | Visits with clinical notes, vaccines and deworming with next due dates and owner reminders, supplies used per visit |
| **Billing (caja)** | Sales from the agenda, the clinical record ("Charge visit") or the counter; split payments; VAT-exempt/taxed lines and split charges; voids; daily cash close |
| **Inventory** | Items and stock movements; stock leaves automatically with sales, visit supplies and vaccines; low-stock email alerts |
| **Exams** | Exam catalog, requests per patient, external labs |
| **Reports** | Agenda occupancy and no-shows, income by method and day, top sold, stock value, clinical activity |
| **Client portal** | "Mis mascotas": owners see vaccines, visits and upcoming appointments through an emailed link (no account) |
| **Platform** | Roles and claim-based permissions, audit log, real-time updates (SignalR), rate limits, health check |

## Tech stack

- .NET 10 / C# 14, ASP.NET Core Web API, EF Core + SQL Server, JWT authentication
- Blazor WebAssembly (staff web, booking portal), .NET MAUI (staff desktop), MudBlazor
- Clean Architecture modular monolith; xUnit + FluentAssertions; GitHub Actions CI (with a SQL Server container)

## Solution layout

```
src/
  VetManagement.Domain/          entities, enums, domain rules (no dependencies)
  VetManagement.Contracts/       API requests/DTOs shared by the API and every client
  VetManagement.Application/     services / use cases
  VetManagement.Infrastructure/  EF Core, repositories, payment gateways
  VetManagement.Api/             controllers, auth, migrations, background jobs, test data (dev only)
  VetManagement.Staff.UI/        staff pages and components, shared by Staff.Web and Staff.Maui
  VetManagement.Staff.Web/       staff web (Blazor WebAssembly)
  VetManagement.Staff.Maui/      staff desktop/mobile (.NET MAUI)
  VetManagement.Booking.Web/     public booking portal + client portal (Blazor WebAssembly)
sites/
  SPVetClinic/                   branded public website of one clinic
tests/
  VetManagement.Tests/           unit, integration and architecture tests
docs/                            documentation (index: docs/README.md)
```

## Running it locally

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), SQL Server LocalDB (comes with
Visual Studio), Visual Studio 2022+ or VS Code.

1. **API secrets** (Development only, never committed):

   ```bash
   cd src/VetManagement.Api
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\MSSQLLocalDB;Database=VetManagement;Trusted_Connection=True;TrustServerCertificate=True"
   dotnet user-secrets set "Jwt:Key" "a-development-key-of-at-least-32-characters"
   dotnet user-secrets set "Jwt:Issuer" "VetManagementApi"
   dotnet user-secrets set "Jwt:Audience" "VetManagementClient"
   dotnet user-secrets set "SeedAdmin:UserName" "admin"
   dotnet user-secrets set "SeedAdmin:Email" "admin@example.com"
   dotnet user-secrets set "SeedAdmin:Password" "<your password>"
   ```

2. **Start** — in Visual Studio pick the **Web** launch profile (API + staff web) and press F5; or from a terminal:

   ```bash
   dotnet run --project src/VetManagement.Api --launch-profile https          # https://localhost:44395 (Swagger at /swagger)
   dotnet run --project src/VetManagement.Staff.Web --launch-profile https    # https://localhost:7237
   dotnet run --project src/VetManagement.Booking.Web --launch-profile https  # https://localhost:7300 (optional)
   ```

   In Development the API applies migrations and seeds roles, the admin user and some sample data on startup.
   Without WebPay credentials, deposits use a simulated payment page.

3. **Sample data** — staff web → *Administration → Test Data* creates realistic records per kind, in dependency
   order. Guide (Spanish): [`docs/guides/DATOS_DE_PRUEBA.md`](docs/guides/DATOS_DE_PRUEBA.md).

4. **Tests**

   ```bash
   dotnet test tests/VetManagement.Tests/VetManagement.Tests.csproj
   ```

   Tests against a real SQL Server (concurrency, migrations) run when `VETMANAGEMENT_TEST_SQLSERVER` is set, e.g.
   `Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True`. CI always runs them.

## Documentation

Start at [`docs/README.md`](docs/README.md): architecture and rules, one design document per module, deployment
for a clinic, guides. Current status, open decisions and roadmap: [`PROJECT_STATE.md`](PROJECT_STATE.md).

## License

Licensed under PolyForm Noncommercial 1.0.0 — see [LICENSE](LICENSE). Commercial use requires permission.
