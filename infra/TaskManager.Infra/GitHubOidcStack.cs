using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace TaskManager.Infra;

public sealed class GitHubOidcStackProps : StackProps
{
    public string Repository { get; init; } = "yamilkar/TaskManager";

    /// <summary>
    /// The repository in GitHub's immutable subject format (owner@id/repo@id), which this repository uses
    /// (gh api repos/OWNER/REPO/actions/oidc/customization/sub shows it as sub_claim_prefix).
    /// </summary>
    public string ImmutableRepository { get; init; } = "yamilkar@97929231/TaskManager@1451505784";

    public string Environment { get; init; } = "production";

    public string Branch { get; init; } = "main";

    /// <summary>An account holds at most one GitHub OIDC provider; pass its ARN to reuse it.</summary>
    public string? ExistingProviderArn { get; init; }
}

/// <summary>
/// Lets GitHub Actions deploy without stored AWS keys: the workflow exchanges its OIDC token for this role, which may
/// only assume the CDK bootstrap roles. The token's subject must name the production environment AND the main branch,
/// so a feature branch cannot use the role even if it edits ci.yml. GitHub puts the branch in the subject only after
/// the repository's OIDC subject template includes "ref" (README, one-time setup); until then the role refuses every
/// token (fail closed). The account owner deploys this stack once, by hand.
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
            Description = $"GitHub Actions deploy role for {props.Repository} ({props.Environment}, {props.Branch} only)",
            MaxSessionDuration = Duration.Hours(1),
            AssumedBy = new WebIdentityPrincipal(providerArn, new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, object>
                {
                    [$"{GitHubIssuer}:aud"] = "sts.amazonaws.com",
                    [$"{GitHubIssuer}:sub"] = new[] { props.Repository, props.ImmutableRepository }
                        .Select(repo => $"repo:{repo}:environment:{props.Environment}:ref:refs/heads/{props.Branch}")
                        .ToArray(),
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
