using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VetManagement.Api.Authorization;
using VetManagement.Api.Controllers;
using VetManagement.Domain.Audit;
using VetManagement.Domain.Enums;
using VetManagement.Infrastructure.Data;

namespace VetManagement.Tests.Integration;

/// <summary>The audit log is filtered and paged by the API: the inventory audit page asks for one page at a time.</summary>
public class AuditApiTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);

    private async Task<string> SeedAsync(int entities, params AuditActionType[] actions)
    {
        var entity = $"Test{Guid.NewGuid():N}"[..20];
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var date = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        for (var id = 1; id <= entities; id++)
            foreach (var action in actions)
                db.AuditLogs.Add(new AuditLog { EntityName = entity, EntityId = id, Action = action, User = "test", Date = date = date.AddMinutes(1) });
        await db.SaveChangesAsync();
        return entity;
    }

    private async Task<JsonNode> PageAsync(string query) => (await _admin.GetFromJsonAsync<JsonNode>($"/api/audit/logs/paged?{query}"))!;

    [Fact]
    public async Task SeveralActionsAndEntities_AreMatched_OnThePageAskedFor()
    {
        var entity = await SeedAsync(3, AuditActionType.Add, AuditActionType.Edit, AuditActionType.Delete);

        var page = await PageAsync($"entityName={entity}&action=Add&action=Delete&entityId=1&entityId=3&page=0&pageSize=3");

        page["totalCount"]!.GetValue<int>().Should().Be(4, "Add and Delete of entities 1 and 3");
        var items = page["items"]!.AsArray();
        items.Should().HaveCount(3);
        items.Select(i => i!["entityId"]!.GetValue<int>()).Should().OnlyContain(id => id == 1 || id == 3);
        items.Select(i => i!["date"]!.GetValue<DateTime>()).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task PageSize_IsCapped_SoNoRequestReadsAWholeTable()
    {
        var entity = await SeedAsync(1, AuditActionType.Edit);

        var page = await PageAsync($"entityName={entity}&page=-1&pageSize=1000000");

        page["pageSize"]!.GetValue<int>().Should().Be(ApiControllerBase.MAX_PAGE_SIZE);
        page["page"]!.GetValue<int>().Should().Be(0);
        page["items"]!.AsArray().Should().ContainSingle();
    }
}
