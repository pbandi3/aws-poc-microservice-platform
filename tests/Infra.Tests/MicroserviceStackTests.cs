using Amazon.CDK;
using Amazon.CDK.Assertions;
using Infra.Stacks;
using Xunit;

namespace Infra.Tests;

public class MicroserviceStackTests
{
    private static Template SynthesizeGreeting(string environmentName, string appVersion) =>
        Synthesize(environmentName, appVersion, "greeting", "poc-api", "Api", "LAMBDA_ASSET_PATH_GREETING");

    private static Template SynthesizeOrders(string environmentName, string appVersion) =>
        Synthesize(environmentName, appVersion, "orders", "poc-orders", "OrdersApi", "LAMBDA_ASSET_PATH_ORDERS");

    private static Template Synthesize(
        string environmentName,
        string appVersion,
        string serviceName,
        string resourcePrefix,
        string handlerAssembly,
        string assetPathEnvVar)
    {
        // Code.FromAsset requires an existing path at synth time; point it at this test's
        // output directory so synthesis succeeds without a real Lambda publish artifact.
        System.Environment.SetEnvironmentVariable(assetPathEnvVar, AppContext.BaseDirectory);

        var app = new App();
        var stack = new MicroserviceStack(app, $"Poc{serviceName}Stack-{environmentName}", new MicroserviceStackProps
        {
            EnvironmentName = environmentName,
            AppVersion = appVersion,
            ServiceName = serviceName,
            ResourcePrefix = resourcePrefix,
            HandlerAssembly = handlerAssembly,
            AssetPathEnvVar = assetPathEnvVar,
            DefaultAssetPath = AppContext.BaseDirectory
        });

        return Template.FromStack(stack);
    }

    [Fact]
    public void GreetingStack_CreatesDotnet8LambdaWithVersionEnvironment()
    {
        var template = SynthesizeGreeting("dev", "1.2.3");

        template.HasResourceProperties("AWS::Lambda::Function", new Dictionary<string, object>
        {
            ["Runtime"] = "dotnet8",
            ["Handler"] = "Api",
            ["FunctionName"] = "poc-api-dev",
            ["Environment"] = new Dictionary<string, object>
            {
                ["Variables"] = new Dictionary<string, object>
                {
                    ["APP_VERSION"] = "1.2.3",
                    ["ENVIRONMENT_NAME"] = "dev",
                    ["SERVICE_NAME"] = "greeting"
                }
            }
        });
    }

    [Fact]
    public void OrdersStack_CreatesDotnet8LambdaWithOwnHandlerAndName()
    {
        var template = SynthesizeOrders("dev", "1.2.3");

        template.HasResourceProperties("AWS::Lambda::Function", new Dictionary<string, object>
        {
            ["Runtime"] = "dotnet8",
            ["Handler"] = "OrdersApi",
            ["FunctionName"] = "poc-orders-dev",
            ["Environment"] = new Dictionary<string, object>
            {
                ["Variables"] = new Dictionary<string, object>
                {
                    ["APP_VERSION"] = "1.2.3",
                    ["ENVIRONMENT_NAME"] = "dev",
                    ["SERVICE_NAME"] = "orders"
                }
            }
        });
    }

    [Fact]
    public void GreetingStack_ExposesApiGatewayRestApi()
    {
        var template = SynthesizeGreeting("prod", "2.0.0");

        template.ResourceCountIs("AWS::ApiGateway::RestApi", 1);
        template.HasResourceProperties("AWS::ApiGateway::RestApi", new Dictionary<string, object>
        {
            ["Name"] = "poc-api-prod"
        });
    }

    [Fact]
    public void OrdersStack_ExposesApiGatewayRestApi()
    {
        var template = SynthesizeOrders("prod", "2.0.0");

        template.ResourceCountIs("AWS::ApiGateway::RestApi", 1);
        template.HasResourceProperties("AWS::ApiGateway::RestApi", new Dictionary<string, object>
        {
            ["Name"] = "poc-orders-prod"
        });
    }

    [Fact]
    public void Stack_SanitizesEphemeralStageName()
    {
        var template = SynthesizeGreeting("pr-42", "0.0.0-pr.42");

        // Hyphens are replaced with underscores for the API Gateway stage name.
        template.HasResourceProperties("AWS::ApiGateway::Stage", new Dictionary<string, object>
        {
            ["StageName"] = "pr_42"
        });
    }
}
