using System.Text.Json;
using Microsoft.Extensions.Hosting;
using TaskManager.SharedKernel;

namespace TaskManager.ApiCommon;

/// <summary>
/// Writes one CloudWatch Embedded Metric Format document per call, as a single line on stdout.
/// </summary>
public sealed class EmfAppMetrics(IHostEnvironment environment, TextWriter? output = null) : IAppMetrics
{
    public const string Namespace = "TaskManager";
    private const string ServiceDimension = "Service";

    private readonly TextWriter _output = output ?? Console.Out;
    private readonly object _gate = new();

    public void Increment(string metricName)
    {
        var line = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["_aws"] = new Dictionary<string, object>
            {
                ["Timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["CloudWatchMetrics"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["Namespace"] = Namespace,
                        ["Dimensions"] = new[] { new[] { ServiceDimension } },
                        ["Metrics"] = new[]
                        {
                            new Dictionary<string, object> { ["Name"] = metricName, ["Unit"] = "Count" },
                        },
                    },
                },
            },
            [ServiceDimension] = environment.ApplicationName,
            [metricName] = 1,
        });

        lock (_gate)
        {
            _output.WriteLine(line);
            _output.Flush();
        }
    }
}
