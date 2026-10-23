using System.Net.Http.Headers;
using FluentAssertions;
using VetManagement.Api.Authorization;

namespace VetManagement.Tests.Integration;

/// <summary>JSON answers are compressed for clients that accept it (App Service on Linux doesn't compress them itself).</summary>
public class ResponseCompressionTests(CustomWebAppFactory factory) : IClassFixture<CustomWebAppFactory>
{
    [Theory]
    [InlineData("br")]
    [InlineData("gzip")]
    public async Task JsonAnswers_AreCompressed(string encoding)
    {
        var client = factory.CreateClientWithPermissions([.. Permissions.ForRole("Admin")]);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));

        var response = await client.SendAsync(request);

        response.Content.Headers.ContentEncoding.Should().Equal(encoding);
    }
}
