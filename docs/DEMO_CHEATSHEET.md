# Demo Cheat Sheet (live walkthrough)

Everything you need on the day, in order: what to say, what to change, exact commands, and what
to show. Target ~20 min. Each scenario is self-contained — drop any if you're short on time.

> Repo: `github.com/pbandi3/aws-poc-microservice-platform`
> Services: **greeting** (`PocApiStack-<env>`, `poc-api-<env>`) and **orders** (`PocOrdersStack-<env>`, `poc-orders-<env>`)
> Environments: ephemeral `pr-<N>` · persistent `dev` · `prod` (reviewer-gated)

---

## 0. Before the meeting (do this once, the morning of)

- [ ] `dev` is green: latest `Deploy Dev` run deployed both services (`PocApiStack-dev`, `PocOrdersStack-dev`).
- [ ] `main` has a green `Release` and a tag (e.g. `v1.0.0`).
- [ ] GitHub → Settings → Environments: `prod` has **Required reviewer** = you, and `AWS_ROLE_ARN` = prod role. `dev`/`ephemeral` = non-prod role. Repo secret `AWS_ROLE_ARN` = non-prod role.
- [ ] Open tabs: **Actions**, **Pull requests**, AWS Console → **CloudFormation** and **Lambda** (us-east-1), and `docs/CICD_PIPELINE.md` (diagrams).
- [ ] Sync local:
  ```bash
  cd /Users/pbandi/git/aws-poc-microservice-platform
  git checkout dev && git pull origin dev
  git checkout main && git pull origin main && git checkout dev
  ```

---

## 1. Story + architecture (2 min, talk only)

Open `README.md` and `docs/CICD_PIPELINE.md`. Say:
- "One repo, **two independently deployable microservices**, one reusable C# CDK stack."
- "Every change flows **feature → dev → main**; each hop triggers a different workflow."
- "Infra is code; prod uses **different credentials** than non-prod and is human-gated."
- Show the GitOps flow diagram and the **selective deploy** diagram in `docs/CICD_PIPELINE.md`.
- Show the branching + SemVer table in `docs/BRANCHING.md` (call out the **transition point**: pre-release labels until the merge to `main`).

---

## 2. Feature branch → ephemeral env → dev (5 min)

**Narrative:** a developer adds a greeting endpoint. PR spins up a real, isolated AWS env and tests it.

### 2.1 Create the branch + change
```bash
git checkout dev && git pull origin dev
git checkout -b feature/greeting-farewell
```

Edit **`src/Api/Program.cs`** — add this line immediately **above** `app.Run();`:
```csharp
app.MapGet("/api/farewell", (string? name) =>
    Results.Ok(new { message = $"Goodbye, {string.IsNullOrWhiteSpace(name) ? "World" : name.Trim()}!" }));
```

Create **`tests/Api.IntegrationTests/FarewellEndpointTests.cs`**:
```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PocApi.IntegrationTests;

public class FarewellEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public FarewellEndpointTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Farewell_ReturnsGoodbyeMessage()
    {
        var response = await _client.GetAsync("/api/farewell?name=ProServe");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FarewellDto>();
        Assert.Equal("Goodbye, ProServe!", body!.Message);
    }

    private sealed record FarewellDto(string Message);
}
```

### 2.2 Commit + push + open PR
```bash
git add -A
git commit -m "feat(greeting): add /api/farewell endpoint"
git push -u origin feature/greeting-farewell
```
Open PR to **dev**: `https://github.com/pbandi3/aws-poc-microservice-platform/compare/dev...feature/greeting-farewell?expand=1`

### 2.3 What to show (Actions → PR Validation)
- `Build & Test` → unit + integration + infra (both services).
- `Detect changed services` → **greeting = true, orders = false**.
- `Ephemeral Env + E2E Smoke` → deploys **only** `PocApiStack-pr-<N>` (orders step skipped).
- The **bot comment** on the PR with the live URL → click `…/api/farewell?name=ProServe`.
- (Optional) CloudFormation: show `PocApiStack-pr-<N>`.

**Talking point:** "Every PR gets a real, throwaway AWS environment; tests run against live infra; the version is `0.0.0-pr.<N>...`."

### 2.4 Merge → dev deploy
- Merge the PR. Show `Deploy Dev`: version like `1.1.0-dev.x`, deploys greeting to `dev`, smoke passes.
- Hit dev `…/api/version` → note the `-dev.` pre-release label.

---

## 3. Selective deployment — the headline (4 min)

**Narrative:** change **only** the orders service; the pipeline touches only that microservice.

```bash
git checkout dev && git pull origin dev
git checkout -b feature/orders-catalog
```

Edit **`src/OrdersApi/Services/OrderService.cs`** — add a 4th item to `Catalog`:
```csharp
        new("1003", "Gizmo", 7, "delivered"),
        new("1004", "Sprocket", 5, "processing")
```
(add the `Sprocket` line; add a comma after the `Gizmo` line as shown)

Update the two counts that assert the catalog size:
- **`tests/OrdersApi.UnitTests/OrderServiceTests.cs`**: `Assert.Equal(3, orders.Count);` → `Assert.Equal(4, orders.Count);`
- **`tests/OrdersApi.IntegrationTests/OrdersEndpointTests.cs`**: `Assert.Equal(3, body!.Orders.Count);` → `Assert.Equal(4, body!.Orders.Count);`

Commit + push + PR:
```bash
git add -A
git commit -m "feat(orders): add Sprocket to catalog"
git push -u origin feature/orders-catalog
```
PR to **dev**: `https://github.com/pbandi3/aws-poc-microservice-platform/compare/dev...feature/orders-catalog?expand=1`

### What to show
- `Detect changed services` → **orders = true, greeting = false**.
- Ephemeral job: **greeting deploy step is SKIPPED**, only `PocOrdersStack-pr-<N>` deploys.
- Bot comment shows only the **orders** URL → click `…/api/orders` (Sprocket now appears).

**Talking point:** "Path-based change detection means the pipeline surgically updates just the microservice that changed — independent, low-blast-radius deploys." Merge it to `dev` to keep dev current.

---

## 4. dev → main → production release (4 min)

**Narrative:** promotion to prod = the SemVer transition + segregation of duties.

Open a PR **dev → main** and merge:
`https://github.com/pbandi3/aws-poc-microservice-platform/compare/main...dev?expand=1`

### What to show (Actions → Release)
- `Compute version` → **stable** version (e.g. `1.1.0`, no pre-release label). ← the transition point.
- `Deploy to prod` **pauses** on the Required-reviewer gate → click **Review deployments → Approve**.
- After approval it assumes the **`GitHubActionsProdDeployRole`** (different creds than non-prod) and deploys both services.
- `Tag and publish GitHub Release` → show the **Releases** page with `v1.1.0`.
- Hit prod `…/api/version` → clean `1.1.0`.

**Talking points:**
- "Pre-release labels (`-dev.x`) become production-ready **only** at the merge to `main`." (`docs/BRANCHING.md`)
- "Prod uses a separate IAM role whose trust only accepts the protected `prod` environment — PR/feature builds physically can't assume it." (`docs/MULTI_ENVIRONMENT.md`)

---

## 5. Hotfix → patch release (2 min)

**Narrative:** urgent prod fix bypasses `dev`, still fully tested, auto-versioned as a patch.

```bash
git checkout main && git pull origin main
git checkout -b hotfix/greeting-copy
```
Edit **`src/Api/Services/GreetingService.cs`** — make a tiny visible change (e.g. change the default target `"World"` to `"there"`), then:
```bash
git add -A
git commit -m "fix(greeting): friendlier default greeting"
git push -u origin hotfix/greeting-copy
```
PR to **main**: `https://github.com/pbandi3/aws-poc-microservice-platform/compare/main...hotfix/greeting-copy?expand=1`

### What to show
- Same PR Validation path (ephemeral env + smoke).
- Merge → `Release` publishes **`v1.1.1`** (patch bump), deploys greeting only (orders unchanged → skipped).

**Talking point:** "Hotfixes are patch releases off `main`, tested the same way, no manual versioning."

---

## 6. Concurrent development (2 min, talk + optional live)

Open `docs/CONCURRENT_DEVELOPMENT.md`. Say:
- "Each developer's PR gets its **own** ephemeral env and **non-colliding** version label."
- "Branch protection requires green CI + review + up-to-date-with-`dev` before merge, so the second PR reconciles with the first."
- Optional live: keep the two feature PRs from steps 2 and 3 open at once — show both have separate `pr-<N>` environments and independent stacks.

---

## Closing (30 sec)
- Two microservices, four branch types, selective deploys, three test tiers + SIT on `dev`.
- Automatic SemVer traceable to its branch; prod is isolated by credentials and gated by a human.
- All infra is C# CDK, reproducible per environment, ephemeral where it should be.

---

## Backup plan (if a live run misbehaves)
- Keep a previously-green run of each workflow open in a tab.
- The diagrams in `docs/CICD_PIPELINE.md` + a prior GitHub Release page tell the whole story without a live run.
- Ephemeral OIDC hiccup: re-run the job (IAM trust propagation), it clears in ~60s.

## Reset after rehearsing (optional)
```bash
# delete demo branches locally + remote
for b in feature/greeting-farewell feature/orders-catalog hotfix/greeting-copy; do
  git branch -D "$b" 2>/dev/null; git push origin --delete "$b" 2>/dev/null
done
# close (don't merge) the rehearsal PRs so pr-cleanup tears down their pr-<N> stacks
```
