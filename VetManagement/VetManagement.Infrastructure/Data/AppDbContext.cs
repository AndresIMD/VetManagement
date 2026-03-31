using System.Text.Json;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using VetManagement.Shared.Enums;
using VetManagement.Shared.Models.Audit;
using VetManagement.Shared.Models.Core;
using VetManagement.Shared.Models.Exams;
using VetManagement.Shared.Models.Inventory;
using VetManagement.Shared.Models.Medical;

namespace VetManagement.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Item> Items { get; set; }
    public DbSet<InventoryMovement> InventoryMovements { get; set; }
    public DbSet<Exam> Exams { get; set; }
    public DbSet<ExamPerformed> ExamsPerformed { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<Pet> Pets { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    // New exam-related entities
    public DbSet<ExternalLab> ExternalLabs { get; set; }
    public DbSet<ExamRequestItem> ExamRequestItems { get; set; }

    // New medical visits
    public DbSet<MedicalVisit> MedicalVisits { get; set; }
    public DbSet<VisitProcedure> VisitProcedures { get; set; }

    private static readonly JsonSerializerOptions _jsonOptions = new();
    private static string SerializeDict(Dictionary<string, string>? v)
    => JsonSerializer.Serialize(v ?? new Dictionary<string, string>(), _jsonOptions);
    private static Dictionary<string, string> DeserializeDict(string? v)
    => string.IsNullOrWhiteSpace(v)
    ? new Dictionary<string, string>()
    : (JsonSerializer.Deserialize<Dictionary<string, string>>(v!, _jsonOptions) ?? new Dictionary<string, string>());

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Item>()
        .HasDiscriminator<ItemType>("Type")
        .HasValue<Item>(ItemType.Material)
        .HasValue<Drug>(ItemType.Drug);

        builder.Entity<Item>()
        .Property(i => i.LowStockThreshold)
        .HasDefaultValue(5);

        // Configure DosageRange as owned type for Drug
        builder.Entity<Drug>().OwnsOne(d => d.DosageDog);
        builder.Entity<Drug>().OwnsOne(d => d.DosageCat);

        builder.Entity<Client>()
        .HasMany(c => c.Pets)
        .WithOne()
        .HasForeignKey(p => p.OwnerId)
        .IsRequired()
        .OnDelete(DeleteBehavior.Cascade);

        // ExamsPerformed relationships
        builder.Entity<ExamPerformed>()
        .HasMany(e => e.Items)
        .WithOne()
        .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamRequestItem>()
        .HasOne(i => i.ExternalLab)
        .WithMany()
        .HasForeignKey(i => i.ExternalLabId)
        .OnDelete(DeleteBehavior.SetNull);

        // Map Attributes (Dictionary<string,string>) as JSON with a value comparer
        var dictComparer = new ValueComparer<Dictionary<string, string>>(
        (l, r) => SerializeDict(l) == SerializeDict(r),
        v => SerializeDict(v).GetHashCode(),
        v => v == null ? new Dictionary<string, string>() : new Dictionary<string, string>(v)
        );

        builder.Entity<ExamRequestItem>()
        .Property(i => i.Attributes)
        .HasConversion(v => SerializeDict(v), v => DeserializeDict(v))
        .Metadata.SetValueComparer(dictComparer);

        // MedicalVisit relationships
        builder.Entity<MedicalVisit>()
        .HasMany(v => v.Procedures)
        .WithOne()
        .OnDelete(DeleteBehavior.Cascade);
    }
}
