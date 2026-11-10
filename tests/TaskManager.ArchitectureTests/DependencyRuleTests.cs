using System.Reflection;
using NetArchTest.Rules;

namespace TaskManager.ArchitectureTests;

public class DependencyRuleTests
{
    // One well-known public type per assembly is enough to load it; no marker classes needed.
    private static readonly Dictionary<string, Assembly> Assemblies = new()
    {
        ["TaskManager.SharedKernel"] = typeof(SharedKernel.Error).Assembly,
        ["TaskManager.Infrastructure.DynamoDb"] = typeof(Infrastructure.DynamoDb.DynamoDbOptions).Assembly,
        ["TaskManager.ApiCommon"] = typeof(ApiCommon.ApiControllerBase).Assembly,
        ["TaskManager.Users.Domain"] = typeof(Users.Domain.User).Assembly,
        ["TaskManager.Users.Application"] = typeof(Users.Application.UserErrors).Assembly,
        ["TaskManager.Users.Infrastructure"] = typeof(Users.Infrastructure.UsersDatabaseBootstrapper).Assembly,
        ["TaskManager.Users.Api"] = typeof(Users.Api.Controllers.AuthController).Assembly,
        ["TaskManager.Tasks.Domain"] = typeof(Tasks.Domain.TaskItem).Assembly,
        ["TaskManager.Tasks.Application"] = typeof(Tasks.Application.TaskErrors).Assembly,
        ["TaskManager.Tasks.Infrastructure"] = typeof(Tasks.Infrastructure.TasksDatabaseBootstrapper).Assembly,
        ["TaskManager.Tasks.Api"] = typeof(Tasks.Api.Controllers.TasksController).Assembly,
    };

    private static readonly Type[] HandlerInterfaces = [typeof(SharedKernel.ICommandHandler<,>), typeof(SharedKernel.IQueryHandler<,>)];

    private static bool IsHandler(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType && HandlerInterfaces.Contains(i.GetGenericTypeDefinition()));

    [Theory]
    [InlineData("Users")]
    [InlineData("Tasks")]
    public void Application_use_cases_are_sealed_command_or_query_handlers(string ctx)
    {
        var types = Load($"TaskManager.{ctx}.Application").GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }).ToList();

        var handlersWithoutInterface = types
            .Where(t => t.Name.EndsWith("Handler", StringComparison.Ordinal) && !IsHandler(t))
            .Select(t => t.FullName);
        handlersWithoutInterface.ShouldBeEmpty("every *Handler must implement ICommandHandler or IQueryHandler");

        types.Where(IsHandler).Where(t => !t.IsSealed).Select(t => t.FullName).ShouldBeEmpty("handlers must be sealed");

        types.Where(t => t.Name.EndsWith("Service", StringComparison.Ordinal)).Select(t => t.FullName)
            .ShouldBeEmpty("use cases are handlers, not services");
    }

    [Theory]
    [InlineData("Users")]
    [InlineData("Tasks")]
    public void Api_request_bodies_are_api_dtos_not_application_commands(string ctx)
    {
        var api = Load($"TaskManager.{ctx}.Api");
        var bodyTypes = api.GetTypes()
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetParameters())
            .Where(p => p.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromBodyAttribute>() is not null)
            .Select(p => p.ParameterType)
            .ToList();

        bodyTypes.ShouldNotBeEmpty();
        bodyTypes.Where(t => t.Assembly != api).Select(t => t.FullName)
            .ShouldBeEmpty("controllers bind an Api request DTO and build the command themselves");
    }

    [Theory]
    [InlineData("Users")]
    [InlineData("Tasks")]
    public void Api_controllers_depend_on_handler_interfaces_not_implementations(string ctx) =>
        Types.InAssembly(Load($"TaskManager.{ctx}.Api")).That().HaveNameEndingWith("Controller").ShouldNot()
            .HaveDependencyOnAny(Load($"TaskManager.{ctx}.Application").GetTypes().Where(IsHandler).Select(t => t.FullName!).ToArray())
            .GetResult().ShouldBeSuccessful("controllers inject ICommandHandler/IQueryHandler, not concrete handlers");

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
            .GetResult().ShouldBeSuccessful();

    [Theory]
    [InlineData("Users")]
    [InlineData("Tasks")]
    public void Domain_does_not_depend_on_application_or_infrastructure(string ctx) =>
        Types.InAssembly(Load($"TaskManager.{ctx}.Domain")).ShouldNot()
            .HaveDependencyOnAny($"TaskManager.{ctx}.Application", $"TaskManager.{ctx}.Infrastructure", $"TaskManager.{ctx}.Api")
            .GetResult().ShouldBeSuccessful();

    [Theory]
    [InlineData("Users")]
    [InlineData("Tasks")]
    public void Application_does_not_depend_on_infrastructure_or_api(string ctx) =>
        Types.InAssembly(Load($"TaskManager.{ctx}.Application")).ShouldNot()
            .HaveDependencyOnAny($"TaskManager.{ctx}.Infrastructure", $"TaskManager.{ctx}.Api")
            .GetResult().ShouldBeSuccessful();

    [Theory]
    [InlineData("Users", "Tasks")]
    [InlineData("Tasks", "Users")]
    public void Bounded_contexts_do_not_reference_each_other(string ctx, string other)
    {
        foreach (var layer in new[] { "Domain", "Application", "Infrastructure", "Api" })
        {
            Types.InAssembly(Load($"TaskManager.{ctx}.{layer}")).ShouldNot()
                .HaveDependencyOnAny($"TaskManager.{other}")
                .GetResult().ShouldBeSuccessful($"{ctx}.{layer} must not depend on {other}");
        }
    }

    [Theory]
    [InlineData("TaskManager.Users.Infrastructure")]
    [InlineData("TaskManager.Tasks.Infrastructure")]
    [InlineData("TaskManager.ApiCommon")]
    [InlineData("TaskManager.Infrastructure.DynamoDb")]
    public void Infrastructure_and_building_blocks_do_not_depend_on_api_assemblies(string assembly) =>
        Types.InAssembly(Load(assembly)).ShouldNot()
            .HaveDependencyOnAny("TaskManager.Users.Api", "TaskManager.Tasks.Api")
            .GetResult().ShouldBeSuccessful($"{assembly} must not depend on an Api assembly");
}

internal static class ArchitectureResultExtensions
{
    public static void ShouldBeSuccessful(this TestResult result, string? because = null)
    {
        var failing = string.Join(", ", result.FailingTypeNames ?? []);
        result.IsSuccessful.ShouldBeTrue($"{because ?? "Dependency rule violated"}. Failing types: [{failing}]");
    }
}
