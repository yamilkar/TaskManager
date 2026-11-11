using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SSM;
using Constructs;

namespace TaskManager.Infra;

public sealed class ApiConstructProps
{
    public required string CodePath { get; init; }

    /// <summary>The API's assembly name, which is the Lambda handler for an ASP.NET Core app using AddAWSLambdaHosting.</summary>
    public required string AssemblyName { get; init; }

    public required ITable Table { get; init; }

    public required IStringParameter JwtSigningKey { get; init; }

    /// <summary>The site origin allowed by CORS, e.g. https://d123.cloudfront.net.</summary>
    public required string AllowedOrigin { get; init; }

    /// <summary>The demo user's private password (Users API only); the Tasks API seeds demo tasks without it.</summary>
    public IStringParameter? DemoPassword { get; init; }
}

/// <summary>
/// One ASP.NET Core API on Lambda, exposed through a public Function URL. Function URLs cost nothing beyond the
/// Lambda invocation, unlike API Gateway, and send the same payload format (2.0) as an HTTP API.
/// </summary>
public sealed class ApiConstruct : Construct
{
    public ApiConstruct(Construct scope, string id, ApiConstructProps props) : base(scope, id)
    {
        var logs = new LogGroup(this, "Logs", new LogGroupProps
        {
            Retention = RetentionDays.ONE_WEEK,
            RemovalPolicy = RemovalPolicy.DESTROY,
        });

        var environment = new Dictionary<string, string>
        {
            ["Dynamo__TableName"] = props.Table.TableName,
            ["Dynamo__Region"] = Stack.Of(this).Region,
            ["Jwt__SigningKeyParameterName"] = props.JwtSigningKey.ParameterName,
            ["Cors__AllowedOrigins__0"] = props.AllowedOrigin,
            // Idempotent demo data for reviewers: the demo user (Users API) and its tasks (Tasks API).
            ["Seed__Enabled"] = "true",
            // Reviewers can browse the API with Scalar at /scalar/v1 (OpenAPI document at /openapi/v1.json).
            ["OpenApi__Enabled"] = "true",
            // Spec section 11: CloudWatch extracts EMF metrics only from log lines that are pure JSON.
            ["AWS_LAMBDA_HANDLER_LOG_FORMAT"] = "Unformatted",
        };
        if (props.DemoPassword is not null)
        {
            environment["Seed__DemoPasswordParameterName"] = props.DemoPassword.ParameterName;
        }

        var function = new Function(this, "Function", new FunctionProps
        {
            Runtime = Runtime.DOTNET_10,
            Architecture = Architecture.ARM_64,
            Handler = props.AssemblyName,
            Code = Code.FromAsset(props.CodePath),
            MemorySize = 1024,
            Timeout = Duration.Seconds(15),
            LogGroup = logs,
            Environment = environment,
        });

        props.Table.GrantReadWriteData(function);
        props.JwtSigningKey.GrantRead(function);
        props.DemoPassword?.GrantRead(function);

        var url = function.AddFunctionUrl(new FunctionUrlOptions { AuthType = FunctionUrlAuthType.NONE });

        Function = function;

        // The Function URL ends with "/", and the client appends "/api/v1/...", so publish "https://" + host only.
        Url = Fn.Join(string.Empty, ["https://", Fn.Select(2, Fn.Split("/", url.Url))]);
    }

    public IFunction Function { get; }

    public string Url { get; }
}
