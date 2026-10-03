using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Contracts.Billing;
using VetManagement.Domain.Enums;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration.SqlServer;

public class ConcurrentPaymentTests
{
    [SqlServerFact]
    public async Task ParallelPayments_NeverOverpayASale()
    {
        await using var factory = new SqlServerAppFactory();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        try
        {
            var admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
            var saleId = await BillingApiTests.OpenSaleAsync(admin);
            await BillingApiTests.IdOf(await admin.PostAsJsonAsync($"/api/billing/sales/{saleId}/lines",
                new { Kind = SaleLineKind.Other, Description = "Cirugía", UnitPrice = 10000 }));

            // Five desks each try to pay 6000 of a 10000 balance: only one fits.
            var attempts = Enumerable.Range(0, 5).Select(_ => factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")])
                .PostAsJsonAsync($"/api/billing/sales/{saleId}/payments", new { Method = "Cash", Amount = 6000 }));
            var statuses = (await Task.WhenAll(attempts)).Select(r => r.StatusCode).ToList();

            statuses.Count(s => s == HttpStatusCode.OK).Should().Be(1, $"got [{string.Join(", ", statuses)}]");
            var sale = (await admin.GetFromJsonAsync<SaleDto>($"/api/billing/sales/{saleId}"))!;
            sale.PaidAmount.Should().Be(6000);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }
}
