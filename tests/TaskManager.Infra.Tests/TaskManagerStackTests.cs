using Amazon.CDK.Assertions;

namespace TaskManager.Infra.Tests;

using Props = Dictionary<string, object>;

public sealed class TaskManagerStackTests
{
    private static readonly Template T = StackFixture.Template;

    // ---- Data -------------------------------------------------------------------------------------------------

    [Fact]
    public void Stack_usersTable_hasPkKeyFreeTierCapacityAndIsDeletedWithTheStack()
    {
        T.HasResource("AWS::DynamoDB::Table", new Props
        {
            ["DeletionPolicy"] = "Delete",
            ["Properties"] = new Props
            {
                ["TableName"] = "TaskManager-Users",
                ["KeySchema"] = new object[] { new Props { ["AttributeName"] = "pk", ["KeyType"] = "HASH" } },
                ["AttributeDefinitions"] = new object[] { new Props { ["AttributeName"] = "pk", ["AttributeType"] = "S" } },
                ["ProvisionedThroughput"] = new Props { ["ReadCapacityUnits"] = 5, ["WriteCapacityUnits"] = 5 },
            },
        });
    }

    [Fact]
    public void Stack_tasksTable_hasUserIdAndIdKeysAndFreeTierCapacity()
    {
        T.HasResourceProperties("AWS::DynamoDB::Table", new Props
        {
            ["TableName"] = "TaskManager-Tasks",
            ["KeySchema"] = new object[]
            {
                new Props { ["AttributeName"] = "userId", ["KeyType"] = "HASH" },
                new Props { ["AttributeName"] = "_id", ["KeyType"] = "RANGE" },
            },
            ["ProvisionedThroughput"] = new Props { ["ReadCapacityUnits"] = 5, ["WriteCapacityUnits"] = 5 },
        });
    }

    [Fact]
    public void Stack_tablesTogether_stayWithinTheAlwaysFreeCapacity()
    {
        T.ResourceCountIs("AWS::DynamoDB::Table", 2);
        T.ResourceCountIs("AWS::DynamoDB::GlobalTable", 0);
    }

    // ---- APIs -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("TaskManager.Users.Api")]
    [InlineData("TaskManager.Tasks.Api")]
    public void Stack_apiFunction_usesDotnet10Arm64AndAssemblyHandler(string handler)
    {
        T.HasResourceProperties("AWS::Lambda::Function", new Props
        {
            ["Handler"] = handler,
            ["Runtime"] = "dotnet10",
            ["Architectures"] = new[] { "arm64" },
            ["MemorySize"] = 1024,
            ["Timeout"] = 15,
        });
    }

    [Theory]
    [InlineData("TaskManager.Users.Api", "TaskManager-Users")]
    [InlineData("TaskManager.Tasks.Api", "TaskManager-Tasks")]
    public void Stack_apiFunction_environmentPointsAtItsTableTheSsmKeyAndTheSite(string handler, string table)
    {
        T.HasResourceProperties("AWS::Lambda::Function", new Props
        {
            ["Handler"] = handler,
            ["Environment"] = new Props
            {
                ["Variables"] = Match.ObjectLike(new Props
                {
                    ["Dynamo__TableName"] = new Props { ["Ref"] = Match.StringLikeRegexp($"^Data{table["TaskManager-".Length..]}") },
                    ["Dynamo__Region"] = Match.AnyValue(),
                    ["Jwt__SigningKeyParameterName"] = "/taskmanager/jwt-signing-key",
                    ["Cors__AllowedOrigins__0"] = Match.AnyValue(),
                    ["AWS_LAMBDA_HANDLER_LOG_FORMAT"] = "Unformatted",
                }),
            },
        });
    }

    [Fact]
    public void Stack_apiFunctions_allowOnlyTheSiteOrigin()
    {
        var functions = T.ResourcesJson("AWS::Lambda::Function");

        // Cors__AllowedOrigins__0 = "https://" + the CloudFront distribution's domain name.
        functions.ShouldContain("\"Cors__AllowedOrigins__0\":{\"Fn::Join\":[\"\",[\"https://\",{\"Fn::GetAtt\":[\"WebHostingDistribution");
    }

    [Theory]
    [InlineData("UsersApi", "TaskManager-Users", "TaskManager-Tasks")]
    [InlineData("TasksApi", "TaskManager-Tasks", "TaskManager-Users")]
    public void Stack_apiFunction_canReadTheSsmKeyAndUseOnlyItsOwnTable(string api, string ownTable, string otherTable)
    {
        var policies = T.FindResources("AWS::IAM::Policy");
        var policy = policies.Single(p => p.Key.StartsWith($"{api}FunctionServiceRoleDefaultPolicy", StringComparison.Ordinal));
        var json = System.Text.Json.JsonSerializer.Serialize(policy.Value);
        var tables = T.FindResources("AWS::DynamoDB::Table");
        string LogicalId(string name) => tables.Single(t =>
            System.Text.Json.JsonSerializer.Serialize(t.Value).Contains($"\"TableName\":\"{name}\"", StringComparison.Ordinal)).Key;

        json.ShouldContain("ssm:GetParameter");
        json.ShouldContain(":parameter/taskmanager/jwt-signing-key");
        json.ShouldContain("dynamodb:PutItem");
        json.ShouldContain("dynamodb:DescribeTable");
        json.ShouldContain(LogicalId(ownTable));
        json.ShouldNotContain(LogicalId(otherTable));
    }

    [Fact]
    public void Stack_apiFunctions_arePublicThroughFunctionUrlsWithoutApiGateway()
    {
        T.ResourceCountIs("AWS::Lambda::Url", 2);
        T.AllResourcesProperties("AWS::Lambda::Url", new Props { ["AuthType"] = "NONE" });
        T.HasResourceProperties("AWS::Lambda::Permission", new Props
        {
            ["Action"] = "lambda:InvokeFunctionUrl",
            ["FunctionUrlAuthType"] = "NONE",
            ["Principal"] = "*",
        });
        T.ResourceCountIs("AWS::ApiGatewayV2::Api", 0);
        T.ResourceCountIs("AWS::ApiGateway::RestApi", 0);
    }

    [Fact]
    public void Stack_logGroups_keepOneWeekAndAreDeletedWithTheStack()
    {
        T.HasResource("AWS::Logs::LogGroup", new Props
        {
            ["DeletionPolicy"] = "Delete",
            ["Properties"] = new Props { ["RetentionInDays"] = 7 },
        });
    }

    [Fact]
    public void Stack_noPaidSecretStore()
    {
        T.ResourceCountIs("AWS::SecretsManager::Secret", 0);
    }

    // ---- Web --------------------------------------------------------------------------------------------------

    [Fact]
    public void Stack_siteBucket_isPrivateEncryptedSslOnlyAndAutoDeleted()
    {
        T.HasResource("AWS::S3::Bucket", new Props
        {
            ["DeletionPolicy"] = "Delete",
            ["Properties"] = Match.ObjectLike(new Props
            {
                ["PublicAccessBlockConfiguration"] = new Props
                {
                    ["BlockPublicAcls"] = true,
                    ["BlockPublicPolicy"] = true,
                    ["IgnorePublicAcls"] = true,
                    ["RestrictPublicBuckets"] = true,
                },
                ["BucketEncryption"] = new Props
                {
                    ["ServerSideEncryptionConfiguration"] = new object[]
                    {
                        new Props { ["ServerSideEncryptionByDefault"] = new Props { ["SSEAlgorithm"] = "AES256" } },
                    },
                },
            }),
        });
        T.ResourcesJson("AWS::S3::BucketPolicy").ShouldContain("\"aws:SecureTransport\":\"false\"");
        T.ResourceCountIs("Custom::S3AutoDeleteObjects", 1);
    }

    [Fact]
    public void Stack_distribution_servesTheSpaOverHttpsThroughOac()
    {
        T.HasResourceProperties("AWS::CloudFront::Distribution", new Props
        {
            ["DistributionConfig"] = Match.ObjectLike(new Props
            {
                ["DefaultRootObject"] = "index.html",
                ["PriceClass"] = "PriceClass_100",
                ["DefaultCacheBehavior"] = Match.ObjectLike(new Props
                {
                    ["ViewerProtocolPolicy"] = "redirect-to-https",
                    ["ResponseHeadersPolicyId"] = Match.AnyValue(),
                }),
                ["CustomErrorResponses"] = new object[]
                {
                    new Props { ["ErrorCode"] = 403, ["ResponseCode"] = 200, ["ResponsePagePath"] = "/index.html", ["ErrorCachingMinTTL"] = 0 },
                    new Props { ["ErrorCode"] = 404, ["ResponseCode"] = 200, ["ResponsePagePath"] = "/index.html", ["ErrorCachingMinTTL"] = 0 },
                },
            }),
        });
        T.ResourceCountIs("AWS::CloudFront::OriginAccessControl", 1);
    }

    [Fact]
    public void Stack_webContent_isNotCachedByBrowsersAndInvalidatesEverything()
    {
        T.HasResourceProperties("Custom::CDKBucketDeployment", new Props
        {
            ["DistributionPaths"] = new[] { "/*" },
            ["SystemMetadata"] = Match.ObjectLike(new Props { ["cache-control"] = "no-cache" }),
        });
    }

    [Fact]
    public void Stack_webContent_writesConfigJsonWithBothApiUrlsAndNoDemoAccount()
    {
        var config = System.Text.Json.JsonDocument.Parse(StackFixture.DeployedConfigJson).RootElement;
        var deployment = T.ResourcesJson("Custom::CDKBucketDeployment");

        config.EnumerateObject().Select(p => p.Name).ShouldBe(["usersApiUrl", "tasksApiUrl"], ignoreOrder: true);
        deployment.ShouldContain("UsersApiFunctionFunctionUrl");
        deployment.ShouldContain("TasksApiFunctionFunctionUrl");
    }

    // ---- Outputs ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("SiteUrl")]
    [InlineData("UsersApiUrl")]
    [InlineData("TasksApiUrl")]
    public void Stack_outputs_exposeTheUrls(string output)
    {
        T.HasOutput(output, new Props { ["Value"] = Match.AnyValue() });
    }

    [Fact]
    public void Stack_apiUrlOutputs_dropTheFunctionUrlTrailingSlash()
    {
        // Function URLs end with "/"; the client appends "/api/v1/...", so outputs and config.json use "https://" + host.
        var outputs = System.Text.Json.JsonSerializer.Serialize(T.FindOutputs("UsersApiUrl"));

        outputs.ShouldContain("\"Fn::Select\":[2,{\"Fn::Split\":[\"/\"");
    }
}
