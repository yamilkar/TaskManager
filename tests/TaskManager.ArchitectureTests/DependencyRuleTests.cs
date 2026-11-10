using System.Reflection;
using NetArchTest.Rules;

namespace TaskManager.ArchitectureTests;

public class DependencyRuleTests
{
    private static readonly Dictionary<string, Assembly> Assemblies = new()
    {
        ["TaskManager.SharedKernel"] = typeof(SharedKernel.AssemblyMarker).Assembly,
        ["TaskManager.Infrastructure.DynamoDb"] = typeof(Infrastructure.DynamoDb.AssemblyMarker).Assembly,
        ["TaskManager.ApiCommon"] = typeof(ApiCommon.AssemblyMarker).Assembly,
        ["TaskManager.Users.Domain"] = typeof(Users.Domain.AssemblyMarker).Assembly,
        ["TaskManager.Users.Application"] = typeof(Users.Application.AssemblyMarker).Assembly,
        ["TaskManager.Users.Infrastructure"] = typeof(Users.Infrastructure.AssemblyMarker).Assembly,
        ["TaskManager.Users.Api"] = typeof(Users.Api.AssemblyMarker).Assembly,
        ["TaskManager.Tasks.Domain"] = typeof(Tasks.Domain.AssemblyMarker).Assembly,
        ["TaskManager.Tasks.Application"] = typeof(Tasks.Application.AssemblyMarker).Assembly,
        ["TaskManager.Tasks.Infrastructure"] = typeof(Tasks.Infrastructure.AssemblyMarker).Assembly,
        ["TaskManager.Tasks.Api"] = typeof(Tasks.Api.AssemblyMarker).Assembly,
    };

    private static Assembly Load(string name) => Assemblies[name];

    [Theory]
    [InlineData("TaskManager.Users.Domain")]
    [InlineData("TaskManager.Users.Application")]
    [InlineData("TaskManager.Tasks.Domain")]
    [InlineData("TaskManager.Tasks.Application")]
    [InlineData("TaskManager.SharedKernel")]
    public void Core_layers_do_not_depend_on_aws_or_aspnet(string assembly) =>
        Types.InAssembly(Load(assembly)).ShouldNot()
            .HaveDependencyOnAny("Amazon", "Microsoft.AspNetCore", "TaskManager.ApiCommon", "TaskManager.Infrastructure.DynamoDb")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Theory]
    [InlineData("Users")]
    [InlineData("Tasks")]
    public void Domain_does_not_depend_on_application_or_infrastructure(string ctx) =>
        Types.InAssembly(Load($"TaskManager.{ctx}.Domain")).ShouldNot()
            .HaveDependencyOnAny($"TaskManager.{ctx}.Application", $"TaskManager.{ctx}.Infrastructure", $"TaskManager.{ctx}.Api")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Theory]
    [InlineData("Users")]
    [InlineData("Tasks")]
    public void Application_does_not_depend_on_infrastructure_or_api(string ctx) =>
        Types.InAssembly(Load($"TaskManager.{ctx}.Application")).ShouldNot()
            .HaveDependencyOnAny($"TaskManager.{ctx}.Infrastructure", $"TaskManager.{ctx}.Api")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Theory]
    [InlineData("Users", "Tasks")]
    [InlineData("Tasks", "Users")]
    public void Bounded_contexts_do_not_reference_each_other(string ctx, string other)
    {
        foreach (var layer in new[] { "Domain", "Application", "Infrastructure", "Api" })
        {
            Types.InAssembly(Load($"TaskManager.{ctx}.{layer}")).ShouldNot()
                .HaveDependencyOnAny($"TaskManager.{other}")
                .GetResult().IsSuccessful.ShouldBeTrue($"{ctx}.{layer} must not depend on {other}");
        }
    }
}
