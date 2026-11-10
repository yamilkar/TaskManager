using System.Text.Json;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace TaskManager.ApiCommon.Tests;

public sealed class EmfAppMetricsTests
{
    [Fact]
    public void EmfAppMetrics_writes_valid_emf_json_with_metric_name()
    {
        var env = Substitute.For<IHostEnvironment>();
        env.ApplicationName.Returns("TaskManager.Tasks.Api");
        var writer = new StringWriter();

        new EmfAppMetrics(env, writer).Increment("TaskCreated");

        using var doc = JsonDocument.Parse(writer.ToString().Trim());
        var root = doc.RootElement;
        var directive = root.GetProperty("_aws").GetProperty("CloudWatchMetrics")[0];
        directive.GetProperty("Namespace").GetString().ShouldBe("TaskManager");
        directive.GetProperty("Dimensions")[0][0].GetString().ShouldBe("Service");
        var metric = directive.GetProperty("Metrics")[0];
        metric.GetProperty("Name").GetString().ShouldBe("TaskCreated");
        metric.GetProperty("Unit").GetString().ShouldBe("Count");
        root.GetProperty("TaskCreated").GetInt32().ShouldBe(1);
        root.GetProperty("Service").GetString().ShouldBe("TaskManager.Tasks.Api");
        root.GetProperty("_aws").GetProperty("Timestamp").GetInt64().ShouldBeGreaterThan(0);
    }
}
