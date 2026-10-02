using System.Net;
using System.Reflection;
using FluentAssertions;
using VetManagement.Api.Authorization;
using VetManagement.Staff.UI.Constants;

namespace VetManagement.Tests.Integration;

/// <summary>
/// Every API route the staff UI calls must exist. A drift (e.g. UI "api/external-labs" vs API
/// "api/ExternalLabs") otherwise only shows up as a 404 in the browser.
/// </summary>
public class UiRoutesTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _admin;

    public UiRoutesTests(CustomWebAppFactory factory) => _admin = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);

    public static IEnumerable<object[]> UiRoutes() => typeof(ApiRouteConstants)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .Where(r => r.StartsWith("api/"))
        .Select(r => r.Replace("{0}", "1"))
        .Distinct()
        .Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(UiRoutes))]
    public async Task RouteUsedByTheUi_ExistsInTheApi(string route)
    {
        // GET only, so no endpoint ever writes during the test: a POST-only route still matches and
        // answers 405 Method Not Allowed. A controller's own 404 (e.g. no item with id 1) carries a
        // ProblemDetails body; only an unmatched route returns an empty 404.
        var response = await _admin.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();

        (response.StatusCode == HttpStatusCode.NotFound && body.Length == 0).Should().BeFalse(
            $"the UI calls '{route}' but the API has no endpoint there");
    }
}
