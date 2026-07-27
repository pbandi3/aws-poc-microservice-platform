using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Lambda;
using Constructs;

namespace Infra.Stacks;

public sealed class PocApiStackProps : StackProps
{
    /// <summary>Logical environment (dev, prod, pr-42, ...). Drives naming and tagging.</summary>
    public required string EnvironmentName { get; init; }

    /// <summary>Semantic version injected into the running Lambda for the /api/version endpoint.</summary>
    public required string AppVersion { get; init; }
}

/// <summary>
/// Provisions a single microservice: a .NET 8 Lambda fronted by an API Gateway REST API.
/// Every resource is namespaced by environment so ephemeral PR stacks never collide with dev/prod.
/// </summary>
public sealed class PocApiStack : Stack
{
    // Path to the `dotnet publish` output of src/Api, relative to the CDK working directory (infra/).
    // Overridable so CI can point at a build-specific artifact location.
    private const string DefaultLambdaAssetPath = "../publish";

    public PocApiStack(Construct scope, string id, PocApiStackProps props)
        : base(scope, id, props)
    {
        var assetPath =
            System.Environment.GetEnvironmentVariable("LAMBDA_ASSET_PATH") ?? DefaultLambdaAssetPath;

        var function = new Function(this, "ApiFunction", new FunctionProps
        {
            FunctionName = $"poc-api-{props.EnvironmentName}",
            Runtime = Runtime.DOTNET_8,
            // Matches <AssemblyName>Api</AssemblyName> in src/Api/Api.csproj.
            Handler = "Api",
            Code = Code.FromAsset(assetPath),
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>
            {
                ["APP_VERSION"] = props.AppVersion,
                ["ENVIRONMENT_NAME"] = props.EnvironmentName
            }
        });

        // API Gateway stage names allow [a-zA-Z0-9_-]; keep it readable and collision-free.
        var stageName = props.EnvironmentName.Replace("-", "_");

        var api = new LambdaRestApi(this, "ApiGateway", new LambdaRestApiProps
        {
            Handler = function,
            RestApiName = $"poc-api-{props.EnvironmentName}",
            Description = $"POC microservice API for {props.EnvironmentName}",
            Proxy = true,
            DeployOptions = new StageOptions
            {
                StageName = stageName,
                // Access logging kept off to avoid the account-level CloudWatch role dependency,
                // which simplifies the free-tier IAM footprint for this POC.
                LoggingLevel = MethodLoggingLevel.OFF
            }
        });

        Amazon.CDK.Tags.Of(this).Add("Project", "aws-poc-microservice-platform");
        Amazon.CDK.Tags.Of(this).Add("Environment", props.EnvironmentName);
        Amazon.CDK.Tags.Of(this).Add("ManagedBy", "cdk");

        _ = new CfnOutput(this, "ApiUrl", new CfnOutputProps
        {
            Value = api.Url,
            Description = "Base URL of the deployed API",
            ExportName = $"PocApiUrl-{props.EnvironmentName}"
        });

        _ = new CfnOutput(this, "FunctionName", new CfnOutputProps
        {
            Value = function.FunctionName,
            Description = "Lambda function name"
        });
    }
}
