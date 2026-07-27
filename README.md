# aws-poc-microservice-platform

A proof-of-concept microservice with a full CI/CD platform, demonstrating:

- **Microservice REST API** — .NET 8 Minimal API running on **AWS Lambda + API Gateway**.
- **Infrastructure as Code** — **AWS CDK written in C#**, parameterized per environment.
- **CI/CD via GitHub Actions** — PR builds with unit, integration, and infrastructure tests.
- **Ephemeral environments** — every PR deploys an isolated stack, runs live E2E smoke tests, and is torn down on close.
- **Branching strategy** — GitFlow-style (`feature/*`, `dev`, `hotfix/*`, `main`) with distinct CI/CD behavior each.
- **Semantic Versioning** — automatic per-branch SemVer via GitVersion; `main` publishes tagged GitHub Releases.

> New here? Read `docs/AWS_SETUP.md` (setup), `docs/BRANCHING.md` (strategy), and `docs/DEMO_SCRIPT.md` (walkthrough).

## Architecture

```
GitHub push/PR ──> GitHub Actions ──(OIDC assume role)──> AWS
                        │
      ┌─────────────────┼──────────────────────┐
      ▼                 ▼                       ▼
  Build & Test     CDK (C#) synth+deploy     Live smoke tests
 (unit/integ/infra)   Lambda + API GW         against deployed URL
                        │
                 CloudFormation stack: PocApiStack-<env>
                   (dev | prod | pr-<number>)
```

Requests flow: **API Gateway (REST, proxy)** → **Lambda (.NET 8, `Api` handler)** → Minimal API endpoints.

## API endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | Service info (name, version, environment) |
| GET | `/health` | Health probe |
| GET | `/api/version` | Deployed SemVer + environment |
| GET | `/api/greeting?name=` | Greeting (defaults to `World`) |

## Repository layout

```
├── src/Api/                     # .NET 8 Minimal API (Lambda-hosted)
├── infra/                       # AWS CDK app (C#) — Lambda + API Gateway
│   ├── Program.cs               # env/version resolution + stack instantiation
│   └── Stacks/PocApiStack.cs    # the environment-scoped stack
├── tests/
│   ├── Api.UnitTests/           # xUnit unit tests (domain logic)
│   ├── Api.IntegrationTests/    # in-memory API tests (WebApplicationFactory)
│   ├── Api.SmokeTests/          # live E2E tests against a deployed URL
│   └── Infra.Tests/             # CDK template assertions
├── .github/
│   ├── actions/deploy/          # composite action: publish + CDK deploy + resolve URL
│   └── workflows/
│       ├── _build-test.yml      # reusable build/test gate
│       ├── pr-validation.yml    # PR: test + ephemeral env + smoke + comment
│       ├── pr-cleanup.yml       # PR closed: cdk destroy ephemeral env
│       ├── deploy-dev.yml       # push to dev: deploy persistent dev
│       └── release.yml          # push to main: prod deploy + tag + GitHub Release
├── iam/                         # OIDC trust + permissions policy templates
├── scripts/aws-bootstrap.sh     # one-time AWS OIDC/IAM/CDK bootstrap
├── GitVersion.yml               # SemVer configuration
└── docs/                        # setup, branching, demo script
```

## Quick start

### 1. AWS setup (once)

See `docs/AWS_SETUP.md`. TL;DR with admin credentials:

```bash
./scripts/aws-bootstrap.sh          # creates OIDC provider, role, and CDK bootstrap
```

Add the printed `AWS_ROLE_ARN` as a GitHub repository secret.

### 2. Local development

```bash
# Run the API locally (http://localhost:5xxx)
dotnet run --project src/Api/Api.csproj

# Run the fast test tiers
dotnet test tests/Api.UnitTests/Api.UnitTests.csproj
dotnet test tests/Api.IntegrationTests/Api.IntegrationTests.csproj
dotnet test tests/Infra.Tests/Infra.Tests.csproj

# Everything at once
dotnet test PocMicroservice.sln
```

### 3. Deploy manually (optional)

```bash
dotnet publish src/Api/Api.csproj -c Release -o ./publish
cd infra
LAMBDA_ASSET_PATH=../publish npx cdk deploy -c environment=dev -c appVersion=0.0.0-local
```

## CI/CD at a glance

| Trigger | Workflow | Result |
|---------|----------|--------|
| PR opened/updated | `pr-validation.yml` | Tests → ephemeral `pr-<N>` env → smoke → PR comment with URL |
| PR closed | `pr-cleanup.yml` | `cdk destroy` of the ephemeral env |
| Push to `dev` | `deploy-dev.yml` | Deploy persistent `dev` (pre-release SemVer) |
| Push to `main` | `release.yml` | Deploy `prod` + tag `vX.Y.Z` + GitHub Release |

## Design notes

- **OIDC over static keys** — no AWS secrets stored in GitHub; the pipeline assumes an IAM role via short-lived OIDC tokens.
- **Environment isolation** — every resource is suffixed with the environment name, so unlimited ephemeral PR stacks coexist with `dev`/`prod`.
- **DRY pipelines** — a reusable workflow (`_build-test.yml`) and composite action (`actions/deploy`) keep the four workflows small.
- **Cost control** — Lambda + API Gateway stay within free tier for a demo; ephemeral stacks are destroyed on PR close.
- **API Gateway CloudWatch role disabled** (`cdk.json`) to keep the IAM footprint minimal for the free-tier account.
