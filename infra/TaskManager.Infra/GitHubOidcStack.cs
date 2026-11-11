using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace TaskManager.Infra;

public sealed class GitHubOidcStackProps : StackProps
{
    public string Repository { get; init; } = "yamilkar/TaskManager";

    public string Environment { get; init; } = "production";

    /// <summary>An account holds at most one GitHub OIDC provider; pass its ARN to reuse it.</summary>
    public string? ExistingProviderArn { get; init; }
}

/// <summary>
/// Lets GitHub Actions deploy without stored AWS keys: the workflow exchanges its OIDC token for this role, which is
/// trusted only for the repository's production environment and may only assume the CDK bootstrap roles.
/// The account owner deploys it once, by hand.
/// </summary>
public sealed class GitHubOidcStack : Stack
{
    private const string GitHubIssuer = "token.actions.githubusercontent.com";

    public GitHubOidcStack(Construct scope, string id, GitHubOidcStackProps props) : base(scope, id, props)
    {
        var providerArn = props.ExistingProviderArn ?? new CfnOIDCProvider(this, "GitHubProvider", new CfnOIDCProviderProps
        {
            Url = $"https://{GitHubIssuer}",
            ClientIdList = ["sts.amazonaws.com"],
        }).AttrArn;

        var role = new Role(this, "DeployRole", new RoleProps
        {
            Description = $"GitHub Actions deploy role for {props.Repository} ({props.Environment})",
            MaxSessionDuration = Duration.Hours(1),
            AssumedBy = new WebIdentityPrincipal(providerArn, new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, object>
                {
                    [$"{GitHubIssuer}:aud"] = "sts.amazonaws.com",
                    [$"{GitHubIssuer}:sub"] = $"repo:{props.Repository}:environment:{props.Environment}",
                },
            }),
        });

        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Actions = ["sts:AssumeRole"],
            Resources = [$"arn:aws:iam::{Aws.ACCOUNT_ID}:role/cdk-hnb659fds-*-role-{Aws.ACCOUNT_ID}-{Aws.REGION}"],
        }));

        _ = new CfnOutput(this, "DeployRoleArn", new CfnOutputProps { Value = role.RoleArn });
    }
}
