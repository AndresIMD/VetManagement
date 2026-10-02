using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>
/// Fails when the EF model drifts from the migrations snapshot: a schema change without a
/// migration, or a refactor (e.g. moving entities between namespaces) that would alter the schema.
/// Compares model vs snapshot only; no database connection is opened.
/// </summary>
public class MigrationsTests
{
    [Fact]
    public void Model_HasNoPendingChanges_ComparedToMigrationsSnapshot()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused", sql => sql.MigrationsAssembly("VetManagement.Api"))
            .Options;

        using var context = new AppDbContext(options);

        // Joined so the failure message lists every pending operation, not just the first.
        string.Join(Environment.NewLine, PendingOperations(context)).Should().BeEmpty(
            "the EF model must match the latest migration; run 'dotnet ef migrations add <Name> --project src/VetManagement.Api'");
    }

    // Same comparison EF does for 'migrations add', listed so a failure shows what changed.
#pragma warning disable EF1001 // Internal EF Core API: acceptable in a test-only diagnostic
    private static IEnumerable<string> PendingOperations(AppDbContext context)
    {
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        if (snapshot is IMutableModel mutable)
            snapshot = mutable.FinalizeModel();
        snapshot = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot);

        var current = context.GetService<IDesignTimeModel>().Model;

        return context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(snapshot.GetRelationalModel(), current.GetRelationalModel())
            .Select(Describe)
            .ToList();
    }
#pragma warning restore EF1001

    private static string Describe(MigrationOperation op) => op switch
    {
        AddColumnOperation o => $"AddColumn {o.Table}.{o.Name}",
        DropColumnOperation o => $"DropColumn {o.Table}.{o.Name}",
        AlterColumnOperation o =>
            $"AlterColumn {o.Table}.{o.Name}: {o.OldColumn.ColumnType ?? o.OldColumn.ClrType.Name} null={o.OldColumn.IsNullable} maxLen={o.OldColumn.MaxLength} default={o.OldColumn.DefaultValue}"
            + $" -> {o.ColumnType ?? o.ClrType.Name} null={o.IsNullable} maxLen={o.MaxLength} default={o.DefaultValue}",
        CreateIndexOperation o => $"CreateIndex {o.Table}.{o.Name} ({string.Join(",", o.Columns)}) unique={o.IsUnique}",
        DropIndexOperation o => $"DropIndex {o.Table}.{o.Name}",
        AddForeignKeyOperation o => $"AddForeignKey {o.Table}.{o.Name} -> {o.PrincipalTable} onDelete={o.OnDelete}",
        DropForeignKeyOperation o => $"DropForeignKey {o.Table}.{o.Name}",
        CreateTableOperation o => $"CreateTable {o.Name}",
        DropTableOperation o => $"DropTable {o.Name}",
        _ => op.GetType().Name,
    };
}
