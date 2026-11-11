using Amazon.CDK;
using Amazon.CDK.AWS.SSM;
using Constructs;

namespace TaskManager.Infra;

public sealed class TaskManagerStackProps : StackProps
{
    public required string UsersApiCodePath { get; init; }

    public required string TasksApiCodePath { get; init; }

    public required string WebDistPath { get; init; }
}

/// <summary>
/// The whole application: DynamoDB tables, both APIs on Lambda Function URLs, and the Angular client on S3 +
/// CloudFront. Every resource is deleted with the stack (cdk destroy TaskManager).
/// </summary>
public sealed class TaskManagerStack : Stack
{
    /// <summary>A SecureString the owner creates once: CloudFormation cannot create SecureString parameters.</summary>
    public const string JwtSigningKeyParameterName = "/taskmanager/jwt-signing-key";

    public TaskManagerStack(Construct scope, string id, TaskManagerStackProps props) : base(scope, id, props)
    {
        var data = new DataConstruct(this, "Data");
        var hosting = new WebHostingConstruct(this, "WebHosting");
        var jwtSigningKey = StringParameter.FromSecureStringParameterAttributes(this, "JwtSigningKey",
            new SecureStringParameterAttributes { ParameterName = JwtSigningKeyParameterName });

        var usersApi = new ApiConstruct(this, "UsersApi", new ApiConstructProps
        {
            CodePath = props.UsersApiCodePath,
            AssemblyName = "TaskManager.Users.Api",
            Table = data.UsersTable,
            JwtSigningKey = jwtSigningKey,
            AllowedOrigin = hosting.SiteUrl,
        });
        var tasksApi = new ApiConstruct(this, "TasksApi", new ApiConstructProps
        {
            CodePath = props.TasksApiCodePath,
            AssemblyName = "TaskManager.Tasks.Api",
            Table = data.TasksTable,
            JwtSigningKey = jwtSigningKey,
            AllowedOrigin = hosting.SiteUrl,
        });

        _ = new WebContentConstruct(this, "WebContent", new WebContentProps
        {
            DistPath = props.WebDistPath,
            Bucket = hosting.Bucket,
            Distribution = hosting.Distribution,
            UsersApiUrl = usersApi.Url,
            TasksApiUrl = tasksApi.Url,
        });

        _ = new CfnOutput(this, "SiteUrl", new CfnOutputProps { Value = hosting.SiteUrl });
        _ = new CfnOutput(this, "UsersApiUrl", new CfnOutputProps { Value = usersApi.Url });
        _ = new CfnOutput(this, "TasksApiUrl", new CfnOutputProps { Value = tasksApi.Url });
    }
}
