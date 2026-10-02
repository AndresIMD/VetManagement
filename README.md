# VetManagement

A veterinary clinic management system built with .NET 10, supporting both web and desktop platforms.

## About This Project

This is a full-stack CRM I built for veterinary clinics. It handles inventory management, client/pet records, medical visits, and lab work. The system works both online and offline with automatic sync.

I built this to learn Clean Architecture, explore Blazor WebAssembly, and work with .NET MAUI for cross-platform apps.

## Tech Stack

**Backend:**
- .NET 10 with C# 14
- ASP.NET Core Web API
- Entity Framework Core + SQL Server
- JWT authentication
- Clean Architecture pattern

**Frontend:**
- Blazor WebAssembly (web client)
- .NET MAUI (desktop app - Windows/macOS/Linux)
- MudBlazor components

## Architecture

The solution is organized using Clean Architecture:

```
VetManagement.sln
├── src/
│   ├── VetManagement.Domain/         # Domain entities & enums (no dependencies)
│   ├── VetManagement.Contracts/      # API transport contracts (DTOs, requests)
│   ├── VetManagement.Application/    # Business logic & use cases
│   ├── VetManagement.Infrastructure/ # Data access (EF Core, repositories, UoW)
│   ├── VetManagement.Api/            # REST API endpoints, auth, migrations
│   ├── VetManagement.Shared/         # Blazor UI: pages, components, API clients
│   ├── VetManagement.Staff.Web/      # Staff web client (Blazor WebAssembly)
│   ├── VetManagement.Staff.Maui/     # Staff desktop/mobile client (.NET MAUI)
│   └── SPVetClinic/                  # Public website of San Pablo Vet Clinic (clinic-specific)
├── tests/
│   └── VetManagement.Tests/          # Unit + integration tests
└── docs/                             # Architecture & coding guidelines
```

**Why this structure?**  
Separating business logic (Application) from data access (Infrastructure) makes the code easier to test and maintain. The API layer only handles HTTP concerns. `Shared` is the UI layer only — it must never be referenced by Application or Infrastructure.

## Key Features

- **Inventory Management**: Track stock with automated low-stock alerts
- **Client & Pet Records**: Complete medical history and visit tracking
- **Medical Visits**: Document consultations with procedure tracking
- **Lab Integration**: Internal and external lab request management
- **Email Alerts**: Scheduled inventory alerts
- **Real-time Updates**: SignalR-powered live inventory and data sync
- **Role-Based Access**: Admin, Manager, and Employee roles with claim-based permissions
- **Audit Logging**: Track all changes for compliance

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (LocalDB works fine)
- Visual Studio 2022+ or VS Code

### Setup

1. **Clone the repo**
   ```bash
   git clone https://github.com/[your-username]/VetManagement.git
   cd VetManagement
   ```

2. **Configure API secrets**
   
   Navigate to the API project and set user secrets:
```bash
   cd src/VetManagement.Api
   
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=VetManagement;Trusted_Connection=True;"
   dotnet user-secrets set "Jwt:Key" "your-secret-key-at-least-32-characters-long"
   dotnet user-secrets set "Jwt:Issuer" "VetManagementApi"
   dotnet user-secrets set "Jwt:Audience" "VetManagementClient"
   dotnet user-secrets set "AllowedOrigins:0" "https://localhost:7001"
   ```

3. **Run database migrations**
   ```bash
   dotnet ef database update --project src/VetManagement.Api
   ```
   
   In development mode, migrations run automatically when you start the API.

4. **Start the API**
   ```bash
   dotnet run --project src/VetManagement.Api
```
   
   API runs at `https://localhost:7213` (check console for exact port)

5. **Configure the web client**
   
   Edit `src/VetManagement.Staff.Web/wwwroot/config.json`:
   ```json
   {
     "LocalApiBaseUrl": "https://localhost:7213"
   }
   ```

6. **Start the web client**
   ```bash
   dotnet run --project src/VetManagement.Staff.Web
   ```
   
   Web app runs at `https://localhost:7237`

**Optional:** Seed an admin user by setting these secrets:
```bash
dotnet user-secrets set "SeedAdmin:Enabled" "true"
dotnet user-secrets set "SeedAdmin:UserName" "admin"
dotnet user-secrets set "SeedAdmin:Email" "admin@example.com"
dotnet user-secrets set "SeedAdmin:Password" "Admin123!"
```

## Development

### Code Formatting

The project uses `.editorconfig` for consistent formatting. Run this before committing:

```bash
dotnet format
```

### Database Migrations

```bash
# Create a new migration
dotnet ef migrations add MigrationName --project src/VetManagement.Api

# Apply migrations
dotnet ef database update --project src/VetManagement.Api
```

### Project Patterns

- **Repository Pattern** for data access abstraction
- **Unit of Work** for transaction coordination
- **Command Pattern** for undo-able operations (inventory adjustments)
- **Dependency Injection** throughout

## What I Learned

Building this project taught me:

- How to structure a Clean Architecture solution
- Working with Blazor WebAssembly and component lifecycle
- EF Core relationship configuration and migrations
- JWT authentication flow in ASP.NET Core
- Cross-platform development with .NET MAUI
- Repository and Unit of Work patterns in practice

## Challenges & Solutions

**Challenge:** Managing polymorphic types (Item/Drug) in EF Core  
**Solution:** Used discriminator column with JSON polymorphism for API serialization

**Challenge:** Offline support for desktop app  
**Solution:** Planning to use SQLite for local cache with sync queue (not yet implemented)

**Challenge:** Keeping UI and API in sync  
**Solution:** Shared project with common models and DTOs

## Future Improvements

- [ ] Unit and integration tests
- [ ] Better offline sync strategy
- [ ] Reporting dashboard with charts
- [ ] Mobile apps (iOS/Android via MAUI)
- [ ] Integration with veterinary lab APIs

## API Documentation

When running in development mode, Swagger UI is available at:
```
https://localhost:7213/swagger
```

## License

Licensed under PolyForm Noncommercial 1.0.0 - see [LICENSE](LICENSE) for details.

Commercial use requires permission. Contact me for licensing inquiries.

---

**Note:** This is a portfolio project demonstrating full-stack .NET development. While fully functional, it's designed primarily for learning and showcase purposes.
