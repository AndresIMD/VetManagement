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
        // Any method: a matched route answers 2xx/4xx other than 404 (e.g. 405 for a POST-only endpoint).
        var get = await _admin.GetAsync(route);
        var post = await _admin.PostAsync(route, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        new[] { get.StatusCode, post.StatusCode }.Should().Contain(s => s != HttpStatusCode.NotFound,
            $"the UI calls '{route}' but the API has no endpoint there");
    }
}
