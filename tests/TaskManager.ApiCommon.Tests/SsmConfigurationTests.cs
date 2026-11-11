using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace TaskManager.ApiCommon.Tests;

public sealed class SsmConfigurationTests
{
    private const string ParameterName = "/taskmanager/jwt-signing-key";
    private const string NameKey = "Jwt:SigningKeyParameterName";
    private const string TargetKey = "Jwt:SigningKey";
    private static readonly string Key = new('k', 64);

    private static ConfigurationManager Configuration(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)));
        return configuration;
    }

    private static Func<IAmazonSimpleSystemsManagement> NeverCalled() =>
        () => throw new InvalidOperationException("The SSM client must not be created.");

    [Fact]
    public void AddFromSsm_parameterNameSet_setsTargetFromDecryptedParameter()
    {
        var client = Substitute.For<IAmazonSimpleSystemsManagement>();
        client.GetParameterAsync(Arg.Is<GetParameterRequest>(r => r.Name == ParameterName && r.WithDecryption == true), Arg.Any<CancellationToken>())
            .Returns(new GetParameterResponse { Parameter = new Parameter { Name = ParameterName, Value = Key } });
        var configuration = Configuration((NameKey, ParameterName));

        configuration.AddFromSsm(() => client, NameKey, TargetKey);

        configuration["Jwt:SigningKey"].ShouldBe(Key);
    }

    [Fact]
    public void AddFromSsm_noParameterName_neverCreatesClient()
    {
        var configuration = Configuration();

        configuration.AddFromSsm(NeverCalled(), NameKey, TargetKey);

        configuration["Jwt:SigningKey"].ShouldBeNull();
    }

    [Fact]
    public void AddFromSsm_targetAlreadySet_keepsItAndNeverCreatesClient()
    {
        var configuration = Configuration((NameKey, ParameterName), ("Jwt:SigningKey", Key));

        configuration.AddFromSsm(NeverCalled(), NameKey, TargetKey);

        configuration["Jwt:SigningKey"].ShouldBe(Key);
    }

    [Fact]
    public void AddFromSsm_demoPasswordKeys_setsTheDemoPassword()
    {
        var client = Substitute.For<IAmazonSimpleSystemsManagement>();
        client.GetParameterAsync(Arg.Is<GetParameterRequest>(r => r.Name == "/taskmanager/demo-password"), Arg.Any<CancellationToken>())
            .Returns(new GetParameterResponse { Parameter = new Parameter { Value = "a-Private-demo-passw0rd" } });
        var configuration = Configuration(("Seed:DemoPasswordParameterName", "/taskmanager/demo-password"));

        configuration.AddFromSsm(() => client, "Seed:DemoPasswordParameterName", "Seed:DemoPassword");

        configuration["Seed:DemoPassword"].ShouldBe("a-Private-demo-passw0rd");
    }

    [Fact]
    public void AddFromSsm_parameterFetchFails_throws()
    {
        var client = Substitute.For<IAmazonSimpleSystemsManagement>();
        client.GetParameterAsync(Arg.Any<GetParameterRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ParameterNotFoundException("not found"));
        var configuration = Configuration((NameKey, ParameterName));

        Should.Throw<ParameterNotFoundException>(() => configuration.AddFromSsm(() => client, NameKey, TargetKey));
    }
}
