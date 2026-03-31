namespace VetManagement.Api.Seeding;

/// <summary>
/// Seed initial data such as roles and admin/test users.
/// </summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
