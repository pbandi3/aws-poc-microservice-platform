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

// Which microservice(s) to synthesize (and therefore deploy). This is the switch the pipeline
// uses for selective, per-service deployments:
//   -c service=greeting  -> only the greeting stack
//   -c service=orders    -> only the orders stack
//   -c service=all        -> both (default; used for full/first-time deploys and teardown)
var service =
    (app.Node.TryGetContext("service") as string ?? "all").ToLowerInvariant();

var env = new Amazon.CDK.Environment
{
    // CDK CLI populates these from the ambient (assumed) credentials at synth time.
    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
    Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION")
};

if (service is "greeting" or "all")
{
    // Stack id kept as PocApiStack-<env> for backward compatibility with the already-deployed
    // greeting service, so CloudFormation updates it in place rather than orphaning it.
    _ = new MicroserviceStack(app, $"PocApiStack-{environmentName}", new MicroserviceStackProps
    {
        EnvironmentName = environmentName,
        AppVersion = appVersion,
        ServiceName = "greeting",
        ResourcePrefix = "poc-api",
        HandlerAssembly = "Api",
        AssetPathEnvVar = "LAMBDA_ASSET_PATH_GREETING",
        DefaultAssetPath = "../publish/greeting",
        Description = $"greeting service ({environmentName}) - {appVersion}",
        Env = env
    });
}

if (service is "orders" or "all")
{
    _ = new MicroserviceStack(app, $"PocOrdersStack-{environmentName}", new MicroserviceStackProps
    {
        EnvironmentName = environmentName,
        AppVersion = appVersion,
        ServiceName = "orders",
        ResourcePrefix = "poc-orders",
        HandlerAssembly = "OrdersApi",
        AssetPathEnvVar = "LAMBDA_ASSET_PATH_ORDERS",
        DefaultAssetPath = "../publish/orders",
        Description = $"orders service ({environmentName}) - {appVersion}",
        Env = env
    });
}

app.Synth();
