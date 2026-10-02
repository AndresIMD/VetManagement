using Microsoft.Extensions.Diagnostics.HealthChecks;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Api.Services;

/// <summary>
/// Reports Unhealthy when the database is unreachable, so /healthz reflects whether the API can actually serve data.
/// </summary>
public class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Database is unreachable.");
}
