using FluentAssertions;
using VetManagement.Domain.Billing;
using VetManagement.Domain.Enums;

namespace VetManagement.Tests.Billing;

public class SaleTests
{
    private static Sale SaleOf(params (int Qty, int Price, int Discount)[] lines)
    {
        var sale = new Sale();
        var id = 1;
        foreach (var (qty, price, discount) in lines)
            sale.AddLine(new SaleLine { Id = id++, Kind = SaleLineKind.Other, Description = "x", Quantity = qty, UnitPrice = price, Discount = discount }).Should().BeNull();
        return sale;
    }

    private static SalePayment Pay(int amount) => new() { Method = "Cash", Amount = amount };

    [Fact]
    public void Totals_ApplyQuantityAndDiscount()
    {
        var sale = SaleOf((2, 5000, 1000), (1, 3000, 0));
        sale.Total.Should().Be(12000);
        sale.Balance.Should().Be(12000);
    }

    [Fact]
    public void PayingTheBalance_MarksPaid_AndOverpaymentIsRejected()
    {
        var sale = SaleOf((1, 10000, 0));

        sale.AddPayment(Pay(4000)).Should().BeNull();
        sale.Status.Should().Be(SaleStatus.Open);
        sale.AddPayment(Pay(7000)).Should().Contain("exceeds the balance (6000)");
        sale.AddPayment(Pay(6000)).Should().BeNull();

        sale.Status.Should().Be(SaleStatus.Paid);
        sale.AddPayment(Pay(1)).Should().NotBeNull();
        sale.AddLine(new SaleLine { Description = "late", Quantity = 1, UnitPrice = 100 }).Should().NotBeNull();
    }

    [Fact]
    public void RemovingALine_CannotLeaveTheTotalBelowWhatWasPaid()
    {
        var sale = SaleOf((1, 10000, 0), (1, 5000, 0));
        sale.AddPayment(Pay(12000)).Should().BeNull();

        sale.RemoveLine(2).Should().Contain("below what was already paid");
        sale.Total.Should().Be(15000);
    }

    [Theory]
    [InlineData(0, 1000, 0, "Quantity")]
    [InlineData(1, -1, 0, "negative")]
    [InlineData(1, 1000, 1001, "discount")]
    public void InvalidLines_AreRejected(int qty, int price, int discount, string error)
        => new Sale().AddLine(new SaleLine { Description = "x", Quantity = qty, UnitPrice = price, Discount = discount })
            .Should().Contain(error);

    [Fact]
    public void ProductLine_NeedsAnItem()
        => new Sale().AddLine(new SaleLine { Kind = SaleLineKind.Product, Description = "x", Quantity = 1, UnitPrice = 1 })
            .Should().Contain("inventory item");

    [Fact]
    public void Void_NeedsAReason_AndHappensOnce()
    {
        var sale = SaleOf((1, 1000, 0));
        sale.Void(" ", "admin", DateTime.UtcNow).Should().NotBeNull();
        sale.Void("Error de digitación", "admin", DateTime.UtcNow).Should().BeNull();
        sale.Status.Should().Be(SaleStatus.Voided);
        sale.Void("again", "admin", DateTime.UtcNow).Should().NotBeNull();
    }

    [Fact]
    public void DefaultBillingSettings_AreValid()
        => new BillingSettings().Validate().Should().BeEmpty();

    [Fact]
    public void BillingSettings_RejectReservedDuplicateAndNoEnabledMethods()
    {
        var settings = new BillingSettings
        {
            MaxDiscountPercent = 120,
            PaymentMethods =
            [
                new() { Code = "Cash", Name = "Efectivo", Enabled = false },
                new() { Code = "cash", Name = "Otro", Enabled = false },
                new() { Code = SalePayment.OnlineDepositMethod, Name = "x", Enabled = false }
            ]
        };

        settings.Validate().Should().Contain(e => e.Contains("MaxDiscountPercent"))
            .And.Contain(e => e.Contains("at least one"))
            .And.Contain(e => e.Contains("reserved"))
            .And.Contain(e => e.Contains("Duplicate"));
    }
}
