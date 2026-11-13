using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using VetManagement.Staff.UI.Layout;
using VetManagement.Staff.UI.Models.Configuration;

namespace VetManagement.Tests;

public class NavigationTests
{
    private static IEnumerable<MenuItem> Pages => AppNavigation.All.SelectMany(item => item.Children ?? [item]);

    private static Func<string, bool> Has(params string[] perms) => perms.Contains;

    private static MenuItem Area(string title) => AppNavigation.Areas.Single(area => area.Title == title);

    [Fact]
    public void EveryLink_GoesToARealPage()
    {
        var routes = typeof(AppNavigation).Assembly.GetTypes()
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>())
            .Select(route => route.Template.TrimStart('/'))
            .ToHashSet();

        Pages.Select(page => page.Href).Should().OnlyContain(href => routes.Contains(href!));
    }

    [Fact]
    public void EveryPage_NeedsAPermission()
        => Pages.Should().OnlyContain(page => page.RequiredPerms.Length > 0);

    [Fact]
    public void TheMostUsedAreas_ComeFirst()
        => AppNavigation.Areas.Take(4).Select(area => area.Title)
            .Should().Equal("Medical Visits", "Billing", "Exams", "Inventory");

    [Fact]
    public void AGroup_ShowsOnlyWhenOneOfItsPagesDoes()
    {
        Area("Billing").Visible(Has("billing.read")).Should().BeTrue();
        Area("Billing").Visible(Has("inventory.read")).Should().BeFalse();
        AppNavigation.Settings.Visible(Has("inventory.read")).Should().BeFalse();
    }

    [Fact]
    public void AnArea_LeadsToTheFirstPageTheUserCanOpen()
    {
        Area("Inventory").MainHref(Has("inventory.read", "consumption.read")).Should().Be("inventory");
        Area("Inventory").MainHref(Has("consumption.read")).Should().Be("consumption");
        Area("Agenda").MainHref(Has()).Should().Be("agenda");
    }

    [Fact]
    public void TheHomePage_HasACardPerArea_WithPatientConsumptionApart()
    {
        var cards = HomePage.AreasFor(Has("inventory.read", "inventory.metrics", "audit.read", "consumption.read"));

        cards.Select(card => (card.Title, card.Href, card.Data)).Should().Equal(
            ("Inventory", "inventory", HomeData.Stock),
            ("Patient consumption", "consumption", HomeData.PatientConsumption));
        cards[0].Links.Select(page => page.Href).Should().Equal("inventory-items", "inventory-audit", "inventory-metrics");
    }

    [Fact]
    public void WhoOnlyRecordsConsumption_GetsItsCard_NotAnEmptyInventoryOne()
        => HomePage.AreasFor(Has("consumption.read")).Select(card => (card.Title, card.Data))
            .Should().Equal(("Patient consumption", HomeData.PatientConsumption));

    [Fact]
    public void ACard_LinksToItsOtherPages_AtMostThree()
        => HomePage.AreasFor(_ => true).Should().OnlyContain(card =>
            card.Links.Count <= HomePage.MaxLinks && card.Links.All(page => page.Href != card.Href));

    [Fact]
    public void TheStockFigures_NeedTheInventoryItself()
        => HomePage.AreasFor(Has("audit.read")).Should().ContainSingle()
            .Which.Data.Should().Be(HomeData.None, "the audit alone doesn't show stock");

    [Theory]
    [InlineData("images/logo.png", true)]
    [InlineData("https://clinic.example/logo.png", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("<LOGO_URL; empty = the product name as text>", false)]
    public void TheHeader_ShowsALogoOnlyWhenThereIsARealOne(string? logoUrl, bool hasLogo)
        => new AppBranding(logoUrl).HasLogo.Should().Be(hasLogo);
}
