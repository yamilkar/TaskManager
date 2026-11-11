using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Constructs;
using AssetOptions = Amazon.CDK.AWS.S3.Assets.AssetOptions;

namespace TaskManager.Infra;

public sealed class WebContentProps
{
    public required string DistPath { get; init; }

    public required IBucket Bucket { get; init; }

    public required IDistribution Distribution { get; init; }

    public required string UsersApiUrl { get; init; }

    public required string TasksApiUrl { get; init; }
}

/// <summary>
/// Uploads the Angular build and writes config.json with the deployed API URLs. The local config.json (which
/// enables the demo account) is left out. Browsers revalidate every file and CloudFront is invalidated, so a
/// redeploy never serves an old index.html or config.json. CloudFormation re-runs this only when the build changes.
/// </summary>
public sealed class WebContentConstruct : Construct
{
    public WebContentConstruct(Construct scope, string id, WebContentProps props) : base(scope, id)
    {
        _ = new BucketDeployment(this, "Deployment", new BucketDeploymentProps
        {
            Sources =
            [
                Source.Asset(props.DistPath, new AssetOptions { Exclude = ["config.json"] }),
                Source.JsonData("config.json", new Dictionary<string, object>
                {
                    ["usersApiUrl"] = props.UsersApiUrl,
                    ["tasksApiUrl"] = props.TasksApiUrl,
                }),
            ],
            DestinationBucket = props.Bucket,
            Distribution = props.Distribution,
            DistributionPaths = ["/*"],
            CacheControl = [CacheControl.NoCache()],
            MemoryLimit = 512,
        });
    }
}
