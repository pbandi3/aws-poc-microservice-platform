# aws-poc-microservice-platform

A proof-of-concept **multi-microservice** platform with a full CI/CD pipeline, demonstrating:

- **Two microservice REST APIs** — `greeting` and `orders`, each a .NET 8 Minimal API on its own **AWS Lambda + API Gateway** stack.
- **Infrastructure as Code** — **AWS CDK written in C#**; one reusable `MicroserviceStack` instantiated per service, parameterized per environment.
- **CI/CD via GitHub Actions** — PR builds with unit, integration, and infrastructure tests.
- **Selective deployment** — path-based change detection deploys **only the microservice(s) that changed**.
- **Ephemeral environments** — every PR deploys isolated stacks, runs live E2E smoke tests, and is torn down on close.
- **Two credential sets** — separate non-prod and prod IAM roles routed via GitHub Environments (prod is reviewer-gated and OIDC-restricted).
- **Branching strategy** — GitFlow-style (`feature/*`, `dev`, `hotfix/*`, `main`) with distinct CI/CD behavior each.
- **Semantic Versioning** — automatic per-branch SemVer via GitVersion; `main` publishes tagged GitHub Releases.

> New here? Read `docs/AWS_SETUP.md` (setup), `docs/BRANCHING.md` (strategy + SemVer), `docs/CICD_PIPELINE.md` (pipeline + SIT + selective deploy), `docs/CONCURRENT_DEVELOPMENT.md`, `docs/MULTI_ENVIRONMENT.md`, and `docs/DEMO_SCRIPT.md` (walkthrough).

## Architecture

```
GitHub push/PR ──> GitHub Actions ──(OIDC assume role: non-prod | prod)──> AWS
                        │
      ┌─────────────────┼───────────────────────────┐
      ▼                 ▼                             ▼
  Build & Test    Detect changed services     Live smoke tests
 (unit/integ/infra)  + CDK (C#) deploy          against deployed URLs
                        │
        ┌───────────────┴────────────────┐
        ▼                                 ▼
  PocApiStack-<env>                PocOrdersStack-<env>
  (greeting: Lambda+API GW)        (orders: Lambda+API GW)
        (dev | prod | pr-<number>, deployed selectively)
```

Requests flow: **API Gateway (REST, proxy)** → **Lambda (.NET 8)** → Minimal API endpoints.

## API endpoints

**greeting service** (`PocApiStack-<env>`, handler `Api`):

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | Service info (name, version, environment) |
| GET | `/health` | Health probe |
| GET | `/api/version` | Deployed SemVer + environment |
| GET | `/api/greeting?name=` | Greeting (defaults to `World`) |

**orders service** (`PocOrdersStack-<env>`, handler `OrdersApi`):

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | Service info |
| GET | `/health` | Health probe |
| GET | `/api/version` | Deployed SemVer + environment |
| GET | `/api/orders` | List orders |
| GET | `/api/orders/{id}` | Get one order (404 if unknown) |

## Repository layout

```
├── src/Api/                     # greeting service — .NET 8 Minimal API (Lambda-hosted)
├── src/OrdersApi/               # orders service — .NET 8 Minimal API (Lambda-hosted)
├── infra/                       # AWS CDK app (C#) — Lambda + API Gateway
│   ├── Program.cs               # env/version/service resolution + stack instantiation
│   └── Stacks/MicroserviceStack.cs  # reusable per-service stack (greeting + orders)
├── tests/
│   ├── Api.UnitTests/               # greeting unit tests
│   ├── Api.IntegrationTests/        # greeting in-memory API tests
│   ├── OrdersApi.UnitTests/         # orders unit tests
│   ├── OrdersApi.IntegrationTests/  # orders in-memory API tests
│   ├── Api.SmokeTests/              # live E2E tests (greeting + orders) against deployed URLs
│   └── Infra.Tests/                 # CDK template assertions (both services)
├── .github/
│   ├── actions/deploy/          # composite action: publish + CDK deploy ONE service + resolve URL
│   └── workflows/
│       ├── _build-test.yml      # reusable build/test gate
│       ├── _changes.yml         # reusable path-filter change detection
│       ├── _version.yml         # reusable SemVer computation
│       ├── pr-validation.yml    # PR: test + ephemeral env (selective) + smoke + comment
│       ├── pr-cleanup.yml       # PR closed: cdk destroy ephemeral env
│       ├── deploy-dev.yml       # push to dev: deploy persistent dev (selective)
│       └── release.yml          # push to main: prod deploy (selective) + tag + GitHub Release
├── iam/                         # OIDC trust (non-prod/prod) + permissions policy templates
├── scripts/
│   ├── aws-bootstrap.sh             # one-time AWS OIDC/IAM/CDK bootstrap
│   └── aws-setup-multienv-roles.sh  # create non-prod + prod deploy roles
├── GitVersion.yml               # SemVer configuration
└── docs/                        # setup, branching, pipeline, concurrency, multi-env, demo
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
# Run a service locally (http://localhost:5xxx)
dotnet run --project src/Api/Api.csproj          # greeting
dotnet run --project src/OrdersApi/OrdersApi.csproj  # orders

# Everything at once (all unit + integration + infra tests)
dotnet test PocMicroservice.sln
```

### 3. Deploy manually (optional)

```bash
# Publish both services, then deploy one (or all) selectively
dotnet publish src/Api/Api.csproj        -c Release -o ./publish/greeting
dotnet publish src/OrdersApi/OrdersApi.csproj -c Release -o ./publish/orders
cd infra
npx cdk deploy PocApiStack-dev    -c environment=dev -c appVersion=0.0.0-local -c service=greeting
npx cdk deploy PocOrdersStack-dev -c environment=dev -c appVersion=0.0.0-local -c service=orders
# ...or both: npx cdk deploy --all -c environment=dev -c appVersion=0.0.0-local -c service=all
```

## CI/CD at a glance

| Trigger | Workflow | Result |
|---------|----------|--------|
| PR opened/updated | `pr-validation.yml` | Tests → ephemeral `pr-<N>` env (changed services only) → smoke → PR comment with URL(s) |
| PR closed | `pr-cleanup.yml` | `cdk destroy` of the ephemeral env |
| Push to `dev` | `deploy-dev.yml` | Deploy changed services to persistent `dev` (pre-release SemVer) |
| Push to `main` | `release.yml` | Deploy changed services to `prod` + tag `vX.Y.Z` + GitHub Release |

## Design notes

- **OIDC over static keys** — no AWS secrets stored in GitHub; the pipeline assumes an IAM role via short-lived OIDC tokens.
- **Environment isolation** — every resource is suffixed with the environment name, so unlimited ephemeral PR stacks coexist with `dev`/`prod`.
- **DRY pipelines** — a reusable workflow (`_build-test.yml`) and composite action (`actions/deploy`) keep the four workflows small.
- **Cost control** — Lambda + API Gateway stay within free tier for a demo; ephemeral stacks are destroyed on PR close.
- **API Gateway CloudWatch role disabled** (`cdk.json`) to keep the IAM footprint minimal for the free-tier account.
