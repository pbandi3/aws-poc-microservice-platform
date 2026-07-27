using Amazon.CDK;
using Infra.Stacks;

var app = new App();

// Environment name drives resource naming and enables many isolated stacks in one account:
//   dev            -> persistent dev environment
//   prod           -> production (deployed from main)
//   pr-<number>    -> ephemeral per-PR environment (created/destroyed by CI)
// Resolution order: CDK context (-c environment=...) then env var, then a safe default.
var environmentName =
    app.Node.TryGetContext("environment") as string
    ?? System.Environment.GetEnvironmentVariable("ENVIRONMENT_NAME")
    ?? "dev";

// Application semantic version, surfaced by the API at runtime. Supplied by the CI pipeline.
var appVersion =
    app.Node.TryGetContext("appVersion") as string
    ?? System.Environment.GetEnvironmentVariable("APP_VERSION")
    ?? "0.0.0-local";

_ = new PocApiStack(app, $"PocApiStack-{environmentName}", new PocApiStackProps
{
    EnvironmentName = environmentName,
    AppVersion = appVersion,
    Description = $"aws-poc-microservice-platform ({environmentName}) - {appVersion}",
    // CDK CLI populates these from the ambient (assumed) credentials at synth time.
    Env = new Amazon.CDK.Environment
    {
        Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
        Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION")
    }
});

app.Synth();
