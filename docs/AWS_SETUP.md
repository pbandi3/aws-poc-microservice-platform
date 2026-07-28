# AWS & GitHub Setup

This is the one-time setup to make the pipeline deploy into your AWS account. It uses
**GitHub OIDC** so there are **no long-lived AWS access keys** stored in GitHub.

## Prerequisites

- An AWS account (free tier is fine) and admin/PowerUser credentials locally (`aws configure` or SSO).
- AWS CLI v2 and Node.js 18+ installed locally (the bootstrap script uses `npx aws-cdk`).
- The repo pushed to `github.com/pbandi3/aws-poc-microservice-platform`.

## Option A — Automated (recommended)

```bash
# From the repo root, with admin credentials active:
chmod +x scripts/aws-bootstrap.sh
./scripts/aws-bootstrap.sh
```

This creates the GitHub OIDC provider, the `GitHubActionsDeployRole` IAM role (trust scoped
to this repo), attaches permissions, and runs `cdk bootstrap`. It prints the `AWS_ROLE_ARN`
to add as a GitHub secret.

Least-privilege variant (requires CDK modern bootstrap, which the script also runs):

```bash
PERMISSIONS_MODE=scoped ./scripts/aws-bootstrap.sh
```

## Option B — Manual

1. **Create the OIDC provider**
   - Provider URL: `https://token.actions.githubusercontent.com`
   - Audience: `sts.amazonaws.com`

2. **Create the IAM role** (e.g. `GitHubActionsDeployRole`) using
   `iam/github-oidc-trust-policy.json` (replace `__ACCOUNT_ID__`). The `sub` condition
   restricts assumption to `repo:pbandi3/aws-poc-microservice-platform:*`.

3. **Attach permissions** — either:
   - **Broad (POC):** attach managed `PowerUserAccess` + `IAMFullAccess`, or
   - **Scoped:** attach `iam/github-actions-permissions-policy.json` (replace `__ACCOUNT_ID__`).

4. **Bootstrap CDK** once per account/region:
   ```bash
   cdk bootstrap aws://<ACCOUNT_ID>/us-east-1
   ```

## GitHub configuration

**Repository secret** (Settings → Secrets and variables → Actions):

| Secret | Value |
|--------|-------|
| `AWS_ROLE_ARN` | `arn:aws:iam::<ACCOUNT_ID>:role/GitHubActionsDeployRole` |

Region is pinned to `us-east-1` via `env.AWS_REGION` in each workflow — change it there if needed.

**Environments** (Settings → Environments) — optional but recommended. The workflows reference
`ephemeral`, `dev`, and `prod`. Create them to enable protection rules (e.g. require approval
before `prod`). They work without configuration if you skip this.

## What the pipeline needs at runtime

- `id-token: write` permission (already declared in each workflow) for OIDC.
- The assumed role must be able to assume the `cdk-*` bootstrap roles and read CloudFormation
  stack outputs. Both broad and scoped permission options above satisfy this.

## Teardown

- Ephemeral PR stacks are destroyed automatically when the PR closes.
- To remove `dev`/`prod` manually (both services):
  ```bash
  dotnet publish src/Api/Api.csproj        -c Release -o ./publish/greeting
  dotnet publish src/OrdersApi/OrdersApi.csproj -c Release -o ./publish/orders
  cd infra
  LAMBDA_ASSET_PATH_GREETING=../publish/greeting LAMBDA_ASSET_PATH_ORDERS=../publish/orders \
    npx cdk destroy --all -c environment=dev  -c service=all
  LAMBDA_ASSET_PATH_GREETING=../publish/greeting LAMBDA_ASSET_PATH_ORDERS=../publish/orders \
    npx cdk destroy --all -c environment=prod -c service=all
  ```
- To remove everything: also delete the `CDKToolkit` stack and the IAM role/OIDC provider.
