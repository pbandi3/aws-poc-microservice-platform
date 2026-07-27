# Demo Script (Wednesday walkthrough)

Audience: technical project manager. Goal: demonstrate CI/CD, all branching strategies,
ephemeral environments, and Semantic Versioning — live. Total time: ~15 minutes.

> Do the one-time setup in `docs/AWS_SETUP.md` and the branch/commit prep the evening before.

## 0. Pre-demo checklist (Tuesday night)

- [ ] `scripts/aws-bootstrap.sh` run; `AWS_ROLE_ARN` secret added to the repo.
- [ ] `main` branch has an initial green `release.yml` run and tag `v1.0.0`.
- [ ] `dev` branch created from `main` and pushed (triggers a green `deploy-dev.yml`).
- [ ] A prepared `feature/*` branch with a small, visible change ready to open as a PR.
- [ ] Actions tab open, plus the AWS Console (CloudFormation + Lambda) in another tab.

## 1. The service & repo tour (2 min)

- Show `src/Api/Program.cs` — a .NET 8 Minimal API with `/health`, `/api/version`, `/api/greeting`.
- Show `infra/Stacks/PocApiStack.cs` — **C# CDK** provisioning Lambda + API Gateway, parameterized by environment.
- Show the three test tiers: `Api.UnitTests`, `Api.IntegrationTests` (in-memory), `Infra.Tests` (CDK assertions), plus `Api.SmokeTests` (live).

## 2. Feature branch → ephemeral environment (4 min)

1. Open a PR from `feature/<name>` into `dev`.
2. In **Actions**, show `PR Validation`:
   - `Build & Test` job (unit + integration + infra).
   - `Ephemeral Env + E2E Smoke` job spins up stack `PocApiStack-pr-<N>`.
3. Show the **bot comment** on the PR with the live ephemeral URL. Click `…/api/version` —
   note the version string `0.0.0-pr.<N>...`.
4. (Optional) Show the `PocApiStack-pr-<N>` stack in CloudFormation.

**Talking point:** every PR gets a real, isolated AWS environment; tests run against live infra.

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

## Closing summary (30 sec)

- One repo, four branch types, four distinct automated behaviors.
- Every change is tested at three levels before it reaches an environment.
- Versioning is automatic and traceable to the branch that produced it.
- Infrastructure is code (C# CDK), reproducible per environment, and ephemeral where it should be.

## If something fails live (backup plan)

- Keep a previously-successful run of each workflow open in a browser tab to show green history.
- The `docs/BRANCHING.md` diagram + a prior GitHub Release page tell the whole story without a live run.
