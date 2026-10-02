using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace VetManagement.Tests.Integration.SqlServer;

/// <summary>
/// Runs the API against a real, throwaway SQL Server database (created and dropped by the test), for
/// behavior the in-memory provider can't reproduce: transactions, isolation, retry strategy.
/// Opt-in: set VETMANAGEMENT_TEST_SQLSERVER to a server connection string without a database, e.g.
/// <c>Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True</c>.
/// </summary>
public class SqlServerAppFactory : CustomWebAppFactory
{
    public const string EnvironmentVariable = "VETMANAGEMENT_TEST_SQLSERVER";

    public string ConnectionString { get; } = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(EnvironmentVariable) ?? "")
    {
        InitialCatalog = $"VetManagement_Tests_{Guid.NewGuid():N}"
    }.ConnectionString;

    // Same provider setup as Program.cs, including the retrying execution strategy.
    protected override void ConfigureDatabase(DbContextOptionsBuilder options)
        => options.UseSqlServer(ConnectionString, sql => sql.MigrationsAssembly("VetManagement.Api").EnableRetryOnFailure(3, TimeSpan.FromSeconds(1), null));
}

/// <summary>Skipped unless <see cref="SqlServerAppFactory.EnvironmentVariable"/> is set.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerAppFactory.EnvironmentVariable)))
            Skip = $"Set {SqlServerAppFactory.EnvironmentVariable} to run tests against SQL Server.";
    }
}
