using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Configuration;

namespace TaskManager.ApiCommon;

/// <summary>
/// In AWS the JWT signing key lives in an SSM Parameter Store SecureString (free tier), named by
/// <c>Jwt:SigningKeyParameterName</c>. It is read once at startup and exposed as <c>Jwt:SigningKey</c>, so the key
/// never appears in Lambda environment variables or templates. A key that is already configured (user-secrets
/// locally) wins and SSM is not called. A failed read throws, so the API never starts without its key.
/// </summary>
public static class SsmSigningKey
{
    public const string ParameterNameKey = "Jwt:SigningKeyParameterName";
    private const string SigningKeyKey = "Jwt:SigningKey";

    public static IConfigurationManager AddJwtSigningKeyFromSsm(
        this IConfigurationManager configuration, Func<IAmazonSimpleSystemsManagement> clientFactory)
    {
        var parameterName = configuration[ParameterNameKey];
        if (string.IsNullOrWhiteSpace(parameterName) || !string.IsNullOrWhiteSpace(configuration[SigningKeyKey]))
        {
            return configuration;
        }

        using var client = clientFactory();

        // Startup code with no synchronization context: blocking once here is safe.
        var response = client
            .GetParameterAsync(new GetParameterRequest { Name = parameterName, WithDecryption = true })
            .GetAwaiter()
            .GetResult();

        configuration.AddInMemoryCollection([new KeyValuePair<string, string?>(SigningKeyKey, response.Parameter.Value)]);
        return configuration;
    }
}
