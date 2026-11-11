using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace TaskManager.ApiCommon.Tests;

public sealed class SsmSigningKeyTests
{
    private const string ParameterName = "/taskmanager/jwt-signing-key";
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
    public void AddJwtSigningKeyFromSsm_parameterNameSet_setsSigningKeyFromDecryptedParameter()
    {
        var client = Substitute.For<IAmazonSimpleSystemsManagement>();
        client.GetParameterAsync(Arg.Is<GetParameterRequest>(r => r.Name == ParameterName && r.WithDecryption == true), Arg.Any<CancellationToken>())
            .Returns(new GetParameterResponse { Parameter = new Parameter { Name = ParameterName, Value = Key } });
        var configuration = Configuration((SsmSigningKey.ParameterNameKey, ParameterName));

        configuration.AddJwtSigningKeyFromSsm(() => client);

        configuration["Jwt:SigningKey"].ShouldBe(Key);
    }

    [Fact]
    public void AddJwtSigningKeyFromSsm_noParameterName_neverCreatesClient()
    {
        var configuration = Configuration();

        configuration.AddJwtSigningKeyFromSsm(NeverCalled());

        configuration["Jwt:SigningKey"].ShouldBeNull();
    }

    [Fact]
    public void AddJwtSigningKeyFromSsm_signingKeyAlreadySet_keepsItAndNeverCreatesClient()
    {
        var configuration = Configuration((SsmSigningKey.ParameterNameKey, ParameterName), ("Jwt:SigningKey", Key));

        configuration.AddJwtSigningKeyFromSsm(NeverCalled());

        configuration["Jwt:SigningKey"].ShouldBe(Key);
    }

    [Fact]
    public void AddJwtSigningKeyFromSsm_parameterFetchFails_throws()
    {
        var client = Substitute.For<IAmazonSimpleSystemsManagement>();
        client.GetParameterAsync(Arg.Any<GetParameterRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ParameterNotFoundException("not found"));
        var configuration = Configuration((SsmSigningKey.ParameterNameKey, ParameterName));

        Should.Throw<ParameterNotFoundException>(() => configuration.AddJwtSigningKeyFromSsm(() => client));
    }
}
