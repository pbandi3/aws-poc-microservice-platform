using Amazon.CDK;
using Amazon.CDK.Assertions;
using Infra.Stacks;
using Xunit;

namespace Infra.Tests;

public class PocApiStackTests
{
    private static Template Synthesize(string environmentName, string appVersion)
    {
        // Code.FromAsset requires an existing path at synth time; point it at this test's
        // output directory so synthesis succeeds without a real Lambda publish artifact.
        Environment.SetEnvironmentVariable("LAMBDA_ASSET_PATH", AppContext.BaseDirectory);

        var app = new App();
        var stack = new PocApiStack(app, $"PocApiStack-{environmentName}", new PocApiStackProps
        {
            EnvironmentName = environmentName,
            AppVersion = appVersion
        });

        return Template.FromStack(stack);
    }

    [Fact]
    public void Stack_CreatesDotnet8LambdaWithVersionEnvironment()
    {
        var template = Synthesize("dev", "1.2.3");

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
                    ["ENVIRONMENT_NAME"] = "dev"
                }
            }
        });
    }

    [Fact]
    public void Stack_ExposesApiGatewayRestApi()
    {
        var template = Synthesize("prod", "2.0.0");

        template.ResourceCountIs("AWS::ApiGateway::RestApi", 1);
        template.HasResourceProperties("AWS::ApiGateway::RestApi", new Dictionary<string, object>
        {
            ["Name"] = "poc-api-prod"
        });
    }

    [Fact]
    public void Stack_SanitizesEphemeralStageName()
    {
        var template = Synthesize("pr-42", "0.0.0-pr.42");

        // Hyphens are replaced with underscores for the API Gateway stage name.
        template.HasResourceProperties("AWS::ApiGateway::Stage", new Dictionary<string, object>
        {
            ["StageName"] = "pr_42"
        });
    }
}
