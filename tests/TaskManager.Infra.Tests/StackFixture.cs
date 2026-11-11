using System.Text.Json;
using Amazon.CDK;
using Amazon.CDK.Assertions;
using TaskManager.Infra;

namespace TaskManager.Infra.Tests;

/// <summary>Synthesizes the stacks once, with placeholder asset folders instead of real build output.</summary>
public static class StackFixture
{
    private static readonly Lazy<(Template Template, string AssemblyDirectory)> App = new(() =>
    {
        var app = new App();
        var stack = new TaskManagerStack(app, "TaskManager", new TaskManagerStackProps
        {
            UsersApiCodePath = AssetFolder("users-api", "TaskManager.Users.Api.dll"),
            TasksApiCodePath = AssetFolder("tasks-api", "TaskManager.Tasks.Api.dll"),
            WebDistPath = AssetFolder("web", "index.html"),
        });
        return (Template.FromStack(stack), app.Synth().Directory);
    });

    public static Template Template => App.Value.Template;

    /// <summary>The config.json the stack uploads, as synthesized into the cloud assembly (deploy-time values are markers).</summary>
    public static string DeployedConfigJson =>
        File.ReadAllText(Directory.EnumerateFiles(App.Value.AssemblyDirectory, "config.json", SearchOption.AllDirectories).Single());

    public static Template GitHubOidc(string? existingProviderArn = null)
    {
        var app = new App();
        var stack = new GitHubOidcStack(app, "TaskManager-GitHubOidc", new GitHubOidcStackProps
        {
            ExistingProviderArn = existingProviderArn,
        });
        return Template.FromStack(stack);
    }

    /// <summary>The resources of one type, as JSON text, for assertions on nested intrinsic functions.</summary>
    public static string ResourcesJson(this Template template, string type) =>
        JsonSerializer.Serialize(template.FindResources(type));

    private static string AssetFolder(string name, string file)
    {
        var path = Path.Combine(Path.GetTempPath(), "taskmanager-infra-tests", name);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, file), name);
        return path;
    }
}
