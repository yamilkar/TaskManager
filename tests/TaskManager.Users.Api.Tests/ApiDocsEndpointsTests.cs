using System.Net;

namespace TaskManager.Users.Api.Tests;

/// <summary>
/// The OpenAPI document and the Scalar UI are always on in Development. Elsewhere (Testing here, Production in AWS)
/// they appear only when <c>OpenApi:Enabled</c> is true, so reviewers can browse the deployed APIs.
/// </summary>
public sealed class ApiDocsEndpointsTests
{
    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task ApiDocs_enabledSetting_servedOutsideDevelopment(string path)
    {
        using var factory = new UsersApiFactory(settings: new Dictionary<string, string?> { ["OpenApi:Enabled"] = "true" });

        var response = await factory.CreateClient().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task ApiDocs_settingMissing_notServedOutsideDevelopment(string path)
    {
        using var factory = new UsersApiFactory();

        var response = await factory.CreateClient().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
