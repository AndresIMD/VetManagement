using System.Text.Json;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Inventory;
using VetManagement.Domain.Audit;
using VetManagement.Domain.Exams;
using VetManagement.Domain.Medical;
using VetManagement.Domain.Configuration;
using VetManagement.Domain.Scheduling;
using Client = VetManagement.Domain.Clients.Client;
using Pet = VetManagement.Domain.Clients.Pet;

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
    public DbSet<VetManagement.Domain.Clinical.PreventiveDose> PreventiveDoses { get; set; }
    public DbSet<VetManagement.Domain.Clinical.VisitSupply> VisitSupplies { get; set; }

    public DbSet<ClinicSetting> ClinicSettings { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<VetManagement.Domain.Billing.Sale> Sales { get; set; }
    public DbSet<VetManagement.Domain.Billing.SaleLine> SaleLines { get; set; }
    public DbSet<VetManagement.Domain.Billing.SalePayment> SalePayments { get; set; }
    public DbSet<VetManagement.Domain.Billing.CashClose> CashCloses { get; set; }

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

        // Column lengths carried over from the legacy Shared models' [MaxLength] attributes
        // (Domain types are persistence-ignorant, so the schema lives here).
        builder.Entity<Item>().Property(i => i.Barcode).HasMaxLength(100);
        builder.Entity<Item>().Property(i => i.BrandBarcode).HasMaxLength(100);
        builder.Entity<InventoryMovement>().Property(m => m.Reason).HasMaxLength(200);
        builder.Entity<InventoryMovement>().Property(m => m.Responsible).HasMaxLength(100);

        // One row per configuration document; Version rejects a save based on a stale read.
        builder.Entity<ClinicSetting>().HasIndex(s => s.Key).IsUnique();
        builder.Entity<ClinicSetting>().Property(s => s.Key).HasMaxLength(100);
        builder.Entity<ClinicSetting>().Property(s => s.UpdatedBy).HasMaxLength(256);
        builder.Entity<ClinicSetting>().Property(s => s.Version).IsConcurrencyToken();

        // Availability queries filter by resource and time; the index also narrows the serializable range locks taken while booking.
        builder.Entity<Appointment>().HasIndex(a => new { a.ResourceCode, a.StartUtc });
        builder.Entity<Appointment>().HasIndex(a => new { a.Status, a.StartUtc });
        builder.Entity<Appointment>().Property(a => a.ServiceCode).HasMaxLength(100);
        builder.Entity<Appointment>().Property(a => a.ResourceCode).HasMaxLength(100);
        builder.Entity<Appointment>().Property(a => a.OwnerName).HasMaxLength(200);
        builder.Entity<Appointment>().Property(a => a.OwnerTaxId).HasMaxLength(20);
        builder.Entity<Appointment>().Property(a => a.OwnerEmail).HasMaxLength(256);
        builder.Entity<Appointment>().Property(a => a.OwnerPhone).HasMaxLength(30);
        builder.Entity<Appointment>().Property(a => a.PetName).HasMaxLength(100);
        builder.Entity<Appointment>().Property(a => a.Notes).HasMaxLength(1000);
        builder.Entity<Appointment>().Property(a => a.CreatedBy).HasMaxLength(256);
        builder.Entity<Appointment>().Property(a => a.CancelledBy).HasMaxLength(256);
        builder.Entity<Appointment>().Property(a => a.CancelReason).HasMaxLength(500);
        builder.Entity<Appointment>().Property(a => a.PublicToken).HasMaxLength(64);
        builder.Entity<Appointment>().HasIndex(a => a.PublicToken).IsUnique().HasFilter("[PublicToken] IS NOT NULL");
        builder.Entity<Appointment>().Property(a => a.PaymentProvider).HasMaxLength(50);
        builder.Entity<Appointment>().Property(a => a.PaymentToken).HasMaxLength(100);
        builder.Entity<Appointment>().HasIndex(a => a.PaymentToken);

        // Billing: computed totals (Total, Balance, Gross...) are never stored.
        builder.Entity<VetManagement.Domain.Billing.Sale>(e =>
        {
            e.Ignore(s => s.Total); e.Ignore(s => s.PaidAmount); e.Ignore(s => s.Balance);
            e.HasIndex(s => new { s.BusinessDate, s.Status });
            // One live sale per appointment (2 = SaleStatus.Voided: a voided sale can be replaced).
            e.HasIndex(s => s.AppointmentId).IsUnique().HasFilter("[AppointmentId] IS NOT NULL AND [Status] <> 2");
            e.Property(s => s.CustomerName).HasMaxLength(200);
            e.Property(s => s.Notes).HasMaxLength(1000);
            e.Property(s => s.CreatedBy).HasMaxLength(256);
            e.Property(s => s.VoidedBy).HasMaxLength(256);
            e.Property(s => s.VoidReason).HasMaxLength(500);
            e.HasMany(s => s.Lines).WithOne().HasForeignKey(l => l.SaleId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Payments).WithOne().HasForeignKey(p => p.SaleId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<VetManagement.Domain.Billing.SaleLine>(e =>
        {
            e.Ignore(l => l.Gross); e.Ignore(l => l.Total);
            e.Property(l => l.Description).HasMaxLength(200);
            e.Property(l => l.ServiceCode).HasMaxLength(100);
        });
        builder.Entity<VetManagement.Domain.Billing.SalePayment>(e =>
        {
            e.HasIndex(p => p.BusinessDate);
            e.Property(p => p.Method).HasMaxLength(50);
            e.Property(p => p.Reference).HasMaxLength(100);
            e.Property(p => p.ReceivedBy).HasMaxLength(256);
        });
        builder.Entity<VetManagement.Domain.Billing.CashClose>(e =>
        {
            e.Ignore(c => c.Difference);
            e.HasIndex(c => c.BusinessDate).IsUnique();
            e.Property(c => c.Notes).HasMaxLength(1000);
            e.Property(c => c.ClosedBy).HasMaxLength(256);
        });

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

        // Clinical record (F9)
        builder.Entity<MedicalVisit>(e =>
        {
            e.HasIndex(v => v.PatientId);
            e.Property(v => v.Reason).HasMaxLength(500);
            e.Property(v => v.Anamnesis).HasMaxLength(4000);
            e.Property(v => v.Examination).HasMaxLength(4000);
            e.Property(v => v.Diagnosis).HasMaxLength(2000);
            e.Property(v => v.Treatment).HasMaxLength(4000);
            e.Property(v => v.WeightKg).HasPrecision(6, 2);
            e.Property(v => v.TemperatureC).HasPrecision(4, 1);
        });
        builder.Entity<VetManagement.Domain.Clinical.PreventiveDose>(e =>
        {
            e.Ignore(d => d.SeriesKey);
            e.HasIndex(d => d.PetId);
            e.HasIndex(d => d.NextDueOn);
            e.Property(d => d.ProtocolCode).HasMaxLength(100);
            e.Property(d => d.ProductName).HasMaxLength(200);
            e.Property(d => d.BatchNumber).HasMaxLength(100);
            e.Property(d => d.Notes).HasMaxLength(1000);
            e.Property(d => d.AppliedBy).HasMaxLength(256);
        });
        builder.Entity<VetManagement.Domain.Clinical.VisitSupply>(e =>
        {
            // Deleting a visit removes its supplies (MedicalVisitService puts their stock back first).
            e.HasOne<MedicalVisit>().WithMany().HasForeignKey(s => s.VisitId).OnDelete(DeleteBehavior.Cascade);
            e.Property(s => s.ItemName).HasMaxLength(200);
            e.Property(s => s.Notes).HasMaxLength(500);
            e.Property(s => s.CreatedBy).HasMaxLength(256);
        });
    }
}
