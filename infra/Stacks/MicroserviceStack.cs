using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Lambda;
using Constructs;

namespace Infra.Stacks;

public sealed class MicroserviceStackProps : StackProps
{
    /// <summary>Logical environment (dev, prod, pr-42, ...). Drives naming and tagging.</summary>
    public required string EnvironmentName { get; init; }

    /// <summary>Semantic version injected into the running Lambda for the /api/version endpoint.</summary>
    public required string AppVersion { get; init; }

    /// <summary>Short service identifier (greeting, orders, ...). Used for naming and tagging.</summary>
    public required string ServiceName { get; init; }

    /// <summary>Prefix for physical resource names, e.g. "poc-api" or "poc-orders".</summary>
    public required string ResourcePrefix { get; init; }

    /// <summary>Lambda handler assembly name, matching &lt;AssemblyName&gt; of the service project.</summary>
    public required string HandlerAssembly { get; init; }

    /// <summary>Name of the env var that overrides the published Lambda asset path for this service.</summary>
    public required string AssetPathEnvVar { get; init; }

    /// <summary>Default Lambda asset path (relative to the CDK working directory, infra/).</summary>
    public required string DefaultAssetPath { get; init; }
}

/// <summary>
/// Provisions one microservice: a .NET 8 Lambda fronted by an API Gateway REST API. The construct
/// is service-agnostic so every microservice (greeting, orders, ...) is defined once and instantiated
/// per service. Each service is an independent CloudFormation stack, which is what lets the pipeline
/// deploy them selectively.
/// </summary>
public sealed class MicroserviceStack : Stack
{
    public MicroserviceStack(Construct scope, string id, MicroserviceStackProps props)
        : base(scope, id, props)
    {
        var assetPath =
            System.Environment.GetEnvironmentVariable(props.AssetPathEnvVar) ?? props.DefaultAssetPath;

        var function = new Function(this, "Function", new FunctionProps
        {
            FunctionName = $"{props.ResourcePrefix}-{props.EnvironmentName}",
            Runtime = Runtime.DOTNET_8,
            Handler = props.HandlerAssembly,
            Code = Code.FromAsset(assetPath),
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            Environment = new Dictionary<string, string>
            {
                ["APP_VERSION"] = props.AppVersion,
                ["ENVIRONMENT_NAME"] = props.EnvironmentName,
                ["SERVICE_NAME"] = props.ServiceName
            }
        });

        // API Gateway stage names allow [a-zA-Z0-9_-]; keep it readable and collision-free.
        var stageName = props.EnvironmentName.Replace("-", "_");

        var api = new LambdaRestApi(this, "ApiGateway", new LambdaRestApiProps
        {
            Handler = function,
            RestApiName = $"{props.ResourcePrefix}-{props.EnvironmentName}",
            Description = $"POC {props.ServiceName} service for {props.EnvironmentName}",
            Proxy = true,
            DeployOptions = new StageOptions
            {
                StageName = stageName,
                LoggingLevel = MethodLoggingLevel.OFF
            }
        });

        Amazon.CDK.Tags.Of(this).Add("Project", "aws-poc-microservice-platform");
        Amazon.CDK.Tags.Of(this).Add("Service", props.ServiceName);
        Amazon.CDK.Tags.Of(this).Add("Environment", props.EnvironmentName);
        Amazon.CDK.Tags.Of(this).Add("ManagedBy", "cdk");

        _ = new CfnOutput(this, "ApiUrl", new CfnOutputProps
        {
            Value = api.Url,
            Description = $"Base URL of the {props.ServiceName} API",
            ExportName = $"PocApiUrl-{props.ServiceName}-{props.EnvironmentName}"
        });

        _ = new CfnOutput(this, "FunctionName", new CfnOutputProps
        {
            Value = function.FunctionName,
            Description = "Lambda function name"
        });
    }
}
