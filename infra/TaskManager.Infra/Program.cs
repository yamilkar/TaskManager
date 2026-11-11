using Amazon.CDK;
using TaskManager.Infra;

var app = new App();

string Context(string key) => app.Node.TryGetContext(key) as string
    ?? throw new InvalidOperationException($"CDK context '{key}' is missing (see infra/cdk.json).");

var usersApiCode = Context("usersApiCode");
var tasksApiCode = Context("tasksApiCode");
var webDist = Context("webDist");

// The application stack needs the published APIs and the Angular build. The one-time GitHubOidc deploy does not,
// so the application stack is left out, with a note, until those folders exist.
string[] missing = [.. new[] { usersApiCode, tasksApiCode, webDist }.Where(path => !Directory.Exists(path))];
if (missing.Length == 0)
{
    _ = new TaskManagerStack(app, "TaskManager", new TaskManagerStackProps
    {
        UsersApiCodePath = usersApiCode,
        TasksApiCodePath = tasksApiCode,
        WebDistPath = webDist,
        Description = "TaskManager: Users and Tasks APIs on Lambda, DynamoDB tables, Angular client on S3 and CloudFront",
    });
}
else
{
    Console.Error.WriteLine($"Stack TaskManager skipped: build output not found ({string.Join(", ", missing)}).");
}

_ = new GitHubOidcStack(app, "TaskManager-GitHubOidc", new GitHubOidcStackProps
{
    ExistingProviderArn = app.Node.TryGetContext("githubOidcProviderArn") as string,
    Description = "TaskManager: GitHub Actions OIDC deploy role",
});

app.Synth();
