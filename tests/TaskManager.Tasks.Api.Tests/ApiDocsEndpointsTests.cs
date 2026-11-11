using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace TaskManager.Tasks.Api.Tests;

/// <summary>The Tasks API gets the same API docs switch from ApiCommon as the Users API.</summary>
public sealed class ApiDocsEndpointsTests
{
    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task ApiDocs_enabledSetting_servedOutsideDevelopment(string path)
    {
        using var factory = new TasksApiFactory();
        using var enabled = factory.WithWebHostBuilder(b => b.UseSetting("OpenApi:Enabled", "true"));

        var response = await enabled.CreateClient().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task ApiDocs_settingMissing_notServedOutsideDevelopment(string path)
    {
        using var factory = new TasksApiFactory();

        var response = await factory.CreateClient().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
