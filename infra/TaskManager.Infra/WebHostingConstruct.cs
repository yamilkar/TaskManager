using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace TaskManager.Infra;

/// <summary>
/// A private S3 bucket served only through CloudFront (origin access control), with HTTPS and SPA deep links:
/// S3 answers 403/404 for app routes such as /tasks, and CloudFront serves /index.html instead.
/// </summary>
public sealed class WebHostingConstruct : Construct
{
    public WebHostingConstruct(Construct scope, string id) : base(scope, id)
    {
        Bucket = new Bucket(this, "Bucket", new BucketProps
        {
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            EnforceSSL = true,
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,
        });

        Distribution = new Distribution(this, "Distribution", new DistributionProps
        {
            DefaultBehavior = new BehaviorOptions
            {
                Origin = S3BucketOrigin.WithOriginAccessControl(Bucket),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                ResponseHeadersPolicy = ResponseHeadersPolicy.SECURITY_HEADERS,
                CachePolicy = CachePolicy.CACHING_OPTIMIZED,
            },
            DefaultRootObject = "index.html",
            PriceClass = PriceClass.PRICE_CLASS_100,
            HttpVersion = HttpVersion.HTTP2_AND_3,
            ErrorResponses = [SpaFallback(403), SpaFallback(404)],
        });

        SiteUrl = "https://" + Distribution.DistributionDomainName;
    }

    public IBucket Bucket { get; }

    public IDistribution Distribution { get; }

    public string SiteUrl { get; }

    private static ErrorResponse SpaFallback(int status) => new()
    {
        HttpStatus = status,
        ResponseHttpStatus = 200,
        ResponsePagePath = "/index.html",
        Ttl = Duration.Seconds(0),
    };
}
