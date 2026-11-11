using Amazon.CDK.Assertions;

namespace TaskManager.Infra.Tests;

using Props = Dictionary<string, object>;

public sealed class GitHubOidcStackTests
{
    private const string ProviderArn = "arn:aws:iam::123456789012:oidc-provider/token.actions.githubusercontent.com";

    [Fact]
    public void Stack_noExistingProvider_createsTheGitHubOidcProvider()
    {
        StackFixture.GitHubOidc().HasResourceProperties("AWS::IAM::OIDCProvider", new Props
        {
            ["Url"] = "https://token.actions.githubusercontent.com",
            ["ClientIdList"] = new[] { "sts.amazonaws.com" },
        });
    }

    [Fact]
    public void Stack_existingProvider_createsNoProvider()
    {
        StackFixture.GitHubOidc(ProviderArn).ResourceCountIs("AWS::IAM::OIDCProvider", 0);
    }

    [Fact]
    public void Stack_deployRole_trustsOnlyTheProductionEnvironmentOfTheRepository()
    {
        StackFixture.GitHubOidc(ProviderArn).HasResourceProperties("AWS::IAM::Role", new Props
        {
            ["MaxSessionDuration"] = 3600,
            ["AssumeRolePolicyDocument"] = Match.ObjectLike(new Props
            {
                ["Statement"] = new object[]
                {
                    Match.ObjectLike(new Props
                    {
                        ["Action"] = "sts:AssumeRoleWithWebIdentity",
                        ["Principal"] = new Props { ["Federated"] = ProviderArn },
                        ["Condition"] = new Props
                        {
                            ["StringEquals"] = new Props
                            {
                                ["token.actions.githubusercontent.com:aud"] = "sts.amazonaws.com",
                                ["token.actions.githubusercontent.com:sub"] = "repo:yamilkar/TaskManager:environment:production",
                            },
                        },
                    }),
                },
            }),
        });
    }

    [Fact]
    public void Stack_deployRole_canOnlyAssumeTheCdkBootstrapRoles()
    {
        var policies = StackFixture.GitHubOidc(ProviderArn).FindResources("AWS::IAM::Policy");
        var json = System.Text.Json.JsonSerializer.Serialize(policies);

        policies.Count.ShouldBe(1);
        json.ShouldContain("\"Action\":\"sts:AssumeRole\"");
        json.ShouldContain(":role/cdk-hnb659fds-*-role-");
        json.ShouldNotContain("\"Action\":\"*\"");
    }

    [Fact]
    public void Stack_outputs_exposeTheDeployRoleArn()
    {
        StackFixture.GitHubOidc().HasOutput("DeployRoleArn", new Props { ["Value"] = Match.AnyValue() });
    }
}
