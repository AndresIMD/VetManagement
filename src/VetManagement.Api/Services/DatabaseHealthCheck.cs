using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Api.Services;

/// <summary>
/// Reports Unhealthy when the database is unreachable or its schema is behind this version, so /healthz reflects whether
/// the API can actually serve data. Start-up logs a failed migration and goes on (the API must come up even when SQL
/// Server is late), so without the second check a half-updated clinic would still answer "Healthy".
/// </summary>
public class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
            return HealthCheckResult.Unhealthy("Database is unreachable.");

        // Migrations only exist on a relational database (the in-memory one of the tests has none).
        if (db.Database.IsRelational())
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            if (pending > 0)
                return HealthCheckResult.Unhealthy($"The database is {pending} migration(s) behind this version.");
        }

        return HealthCheckResult.Healthy();
    }
}
