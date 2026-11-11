using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Configuration;

namespace TaskManager.ApiCommon;

/// <summary>
/// In AWS, secrets live in SSM Parameter Store SecureStrings (free tier). <c>AddFromSsm</c> reads the parameter named
/// by <paramref name="parameterNameKey"/> once at startup and exposes its value as <paramref name="targetKey"/>, so
/// secrets never appear in Lambda environment variables or templates. Used for <c>Jwt:SigningKey</c> (both APIs)
/// and <c>Seed:DemoPassword</c> (Users API). A target value that is already configured (user-secrets locally) wins
/// and SSM is not called. A failed read throws, so the API never starts without its secret.
/// </summary>
public static class SsmConfiguration
{
    public const string JwtSigningKeyParameterNameKey = "Jwt:SigningKeyParameterName";

    public static IConfigurationManager AddFromSsm(
        this IConfigurationManager configuration,
        Func<IAmazonSimpleSystemsManagement> clientFactory,
        string parameterNameKey,
        string targetKey)
    {
        var parameterName = configuration[parameterNameKey];
        if (string.IsNullOrWhiteSpace(parameterName) || !string.IsNullOrWhiteSpace(configuration[targetKey]))
        {
            return configuration;
        }

        using var client = clientFactory();

        // Startup code with no synchronization context: blocking once here is safe.
        var response = client
            .GetParameterAsync(new GetParameterRequest { Name = parameterName, WithDecryption = true })
            .GetAwaiter()
            .GetResult();

        configuration.AddInMemoryCollection([new KeyValuePair<string, string?>(targetKey, response.Parameter.Value)]);
        return configuration;
    }
}
