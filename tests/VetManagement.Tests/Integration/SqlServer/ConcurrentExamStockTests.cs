using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Domain.Clients;
using VetManagement.Domain.Enums;
using VetManagement.Domain.Exams;
using VetManagement.Domain.Inventory;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration.SqlServer;

public class ConcurrentExamStockTests
{
    [SqlServerFact]
    public async Task ParallelDeducts_TakeTheExamsProductOnce()
    {
        await using var factory = new SqlServerAppFactory();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        try
        {
            var (itemId, reportId) = await ExamReportStockTests.WithDbAsync(factory, async db =>
            {
                var item = new Item("Kit T4", ItemType.Material, "T4-KIT-0001", null, 10, 9000);
                var owner = new Client("Ana", "Parker", "12345678-5", "Calle 1", 912345678, "ana@example.test");
                db.AddRange(item, owner);
                await db.SaveChangesAsync();
                var exam = new Exam { Name = "T4", Brand = "BioNote", Machine = "Vcheck V200", ItemId = item.Id, ItemQuantity = 1 };
                var pet = new Pet { OwnerId = owner.Id, Name = "Rocky", Breed = "Golden", Species = Species.Dog };
                db.AddRange(exam, pet);
                await db.SaveChangesAsync();
                var report = new ExamReport { PetId = pet.Id, ExamId = exam.Id, Title = "T4", ReceivedOn = new DateOnly(2026, 10, 20), CreatedBy = "test" };
                db.ExamReports.Add(report);
                await db.SaveChangesAsync();
                return (item.Id, report.Id);
            });

            // Five desks press "Deduct from inventory" on the same report at once: one takes the kit.
            var attempts = Enumerable.Range(0, 5).Select(_ => factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")])
                .PostAsync($"/api/exam-reports/{reportId}/stock", null));
            var statuses = (await Task.WhenAll(attempts)).Select(r => r.StatusCode).ToList();

            statuses.Count(s => s == HttpStatusCode.OK).Should().Be(1, $"got [{string.Join(", ", statuses)}]");
            var (stock, movements) = await ExamReportStockTests.WithDbAsync(factory, async db =>
                ((await db.Items.AsNoTracking().FirstAsync(i => i.Id == itemId)).Stock, await db.InventoryMovements.CountAsync(m => m.ItemId == itemId)));
            stock.Should().Be(9);
            movements.Should().Be(1);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }
}
