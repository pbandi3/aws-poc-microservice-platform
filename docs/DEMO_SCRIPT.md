# Demo Script (Wednesday walkthrough)

Audience: technical project manager. Goal: demonstrate CI/CD, all branching strategies,
ephemeral environments, and Semantic Versioning — live. Total time: ~15 minutes.

> Do the one-time setup in `docs/AWS_SETUP.md` and the branch/commit prep the evening before.

## 0. Pre-demo checklist (Tuesday night)

- [ ] `scripts/aws-bootstrap.sh` run; `AWS_ROLE_ARN` secret added to the repo.
- [ ] (Multi-env) `scripts/aws-setup-multienv-roles.sh` run; `prod` environment secret set to the
      prod role ARN and a Required reviewer added. See `docs/MULTI_ENVIRONMENT.md`.
- [ ] `main` branch has an initial green `release.yml` run and tag `v1.0.0` (deploys both services).
- [ ] `dev` branch created from `main` and pushed (triggers a green `deploy-dev.yml`).
- [ ] A prepared `feature/*` branch with a small, visible change ready to open as a PR.
- [ ] (Selective-deploy demo) a branch that changes **only** `src/OrdersApi` ready.
- [ ] Actions tab open, plus the AWS Console (CloudFormation + Lambda) in another tab.

## 1. The services & repo tour (2 min)

- Show `src/Api/Program.cs` (greeting) and `src/OrdersApi/Program.cs` (orders) — two .NET 8
  Minimal APIs, each an independently deployable microservice.
- Show `infra/Stacks/MicroserviceStack.cs` — one reusable **C# CDK** construct provisioning
  Lambda + API Gateway, instantiated per service in `infra/Program.cs` (greeting + orders).
- Show the test tiers: `Api.UnitTests`/`OrdersApi.UnitTests`, `*.IntegrationTests` (in-memory),
  `Infra.Tests` (CDK assertions), plus `Api.SmokeTests` (live, both services).

**Talking point:** a monorepo with two microservices, each its own CloudFormation stack —
`PocApiStack-<env>` and `PocOrdersStack-<env>`.

## 2. Feature branch → ephemeral environment (4 min)

1. Open a PR from `feature/<name>` into `dev`.
2. In **Actions**, show `PR Validation`:
   - `Build & Test` job (unit + integration + infra, both services).
   - `Detect changed services` job showing which service(s) the PR touched.
   - `Ephemeral Env + E2E Smoke` job spins up stack `PocApiStack-pr-<N>` and/or
     `PocOrdersStack-pr-<N>` — **only for the changed service(s)**.
3. Show the **bot comment** on the PR with the live ephemeral URL(s). Click `…/api/version` —
   note the version string `0.0.0-pr.<N>...`.
4. (Optional) Show the `Poc*Stack-pr-<N>` stack(s) in CloudFormation.

**Talking point:** every PR gets a real, isolated AWS environment; tests run against live infra.

## 2b. Selective deployment (2 min)

1. On a `feature/*` branch, change **only** `src/OrdersApi` (e.g. add an order to the catalog).
2. Open the PR and show the run: the **greeting deploy step is skipped**, only the orders stack
   updates. Do the reverse on another PR (change only `src/Api`) to show the mirror image.

**Talking point:** path-based change detection (`_changes.yml`) means the pipeline surgically
updates just the microservice that changed — the core of independent, low-blast-radius deploys.

## 3. Merge to dev → persistent dev deploy (2 min)

1. Merge the PR into `dev`.
2. Show `Deploy Dev` running: computes a SemVer like `1.1.0-dev.x`, deploys the persistent `dev` env, runs smoke tests.
3. Hit the dev `/api/version` endpoint — note the `-dev.` pre-release version.

## 4. Close the PR → ephemeral teardown (1 min)

- Show `PR Cleanup` running `cdk destroy` for `pr-<N>`.
- Refresh CloudFormation — the ephemeral stack is gone.

**Talking point:** cost control — ephemeral environments are automatically destroyed.

## 5. dev → main → production release + SemVer (3 min)

1. Open and merge a PR from `dev` into `main`.
2. Show `Release`:
   - Computes a **stable** version (e.g. `1.1.0`).
   - Deploys `prod`, runs smoke tests.
   - Creates tag `v1.1.0` and a **GitHub Release** (show the Releases page).
3. Hit prod `/api/version` — clean `1.1.0`, no pre-release label.

## 6. Hotfix branch → patch release (2 min)

1. Create `hotfix/<name>` from `main`, make a one-line fix, open a PR to `main`.
2. Show it get its own ephemeral env + smoke (same PR validation path).
3. Merge → `Release` publishes `v1.1.1` (patch bump).

**Talking point:** urgent fixes bypass `dev`, still fully tested, auto-versioned as a patch.

## 7. Two credential sets across environments (1 min)

- Show **Settings → Environments**: `ephemeral`/`dev` use the **non-prod** role; `prod` uses the
  **prod** role and has a **Required reviewer**.
- Show `iam/github-oidc-trust-prod.json`: the prod role only trusts
  `repo:ORG/REPO:environment:prod`, so PR/feature builds physically cannot assume prod creds.

**Talking point:** segregation of duties — non-prod and prod use different credentials, and prod
is both OIDC-restricted and human-gated. Portable to real multi-account with zero workflow changes.
See `docs/MULTI_ENVIRONMENT.md`.

## Closing summary (30 sec)

- One repo, **two microservices**, four branch types, distinct automated behaviors each.
- **Selective deploys**: only changed services ship.
- Every change is tested at three levels before it reaches an environment; SIT gates `dev → main`.
- Versioning is automatic and traceable to the branch that produced it (see `docs/BRANCHING.md`).
- Infrastructure is code (C# CDK), reproducible per environment, and ephemeral where it should be.
- **Two credential sets** isolate prod from non-prod (see `docs/MULTI_ENVIRONMENT.md`).

## If something fails live (backup plan)

- Keep a previously-successful run of each workflow open in a browser tab to show green history.
- The `docs/BRANCHING.md` diagram + a prior GitHub Release page tell the whole story without a live run.
