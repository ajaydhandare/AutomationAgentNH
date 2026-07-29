# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Start here (every session, every machine)

1. Read `.claude/context/` — decisions confirmed with the client that the code and the design doc
   do **not** record. Several of them override the design doc; see the note below.
2. `Plan/NewHorizon_AutomationAgent_Design_v2.md` is the architecture baseline for structure, data
   model, API surface, and execution semantics — authoritative **except** where a `.claude/context/`
   note supersedes it (notably: the workflow is a timer-driven site-scoped batch cycle, not the
   §6 per-document push model, and there is no Company/tenant dimension).
3. Local SDK is .NET 10 (`dotnet --version` → 10.0.300).

This repo is worked on by several developers from different machines, mostly through Claude Code.
The shared Claude setup — `CLAUDE.md`, `.claude/settings.json`, `.claude/context/`, `.mcp.json` — is
committed, so a fresh clone can start work from a prompt with no manual setup. Per-machine
overrides belong in `.claude/settings.local.json`, which is git-ignored. See
[`.claude/README.md`](.claude/README.md).

## Current state

The solution is scaffolded and building: all five `src/` projects, both test projects, EF Core
migrations under `src/NewHorizon.Automation.Infrastructure/Persistence/Migrations`, and `deploy/`.
Layout (§4 of the design doc):

```
NewHorizon.AutomationAgent.slnx
└── src/
    ├── NewHorizon.Automation.Worker          # Windows Service host + minimal management/read API
    ├── NewHorizon.Automation.Application     # use cases, workflow orchestration, ports (interfaces)
    ├── NewHorizon.Automation.Domain          # entities, workflow model, state machine (pure)
    ├── NewHorizon.Automation.Infrastructure  # EF Core, hosted services, Serilog
    └── NewHorizon.Automation.ErpClient       # typed ERP HTTP clients + Polly + auth handler
└── tests/{UnitTests,IntegrationTests}
```

Clean Architecture: dependencies point inward only. Domain has no project references; Application
defines ports (`IErpClient`, `IJobRepository`, `IWorkflowEngine`, `IDecisionService`,
`INotificationService`, `IClock`) that Infrastructure and ErpClient implement.

## Commands

```powershell
dotnet build NewHorizon.AutomationAgent.slnx
dotnet run --project src/NewHorizon.Automation.Worker
dotnet test
dotnet test tests/NewHorizon.Automation.UnitTests                 # one project
dotnet test --filter "FullyQualifiedName~WorkflowEngineTests"    # one class/test
dotnet ef migrations add <Name> -p src/NewHorizon.Automation.Infrastructure -s src/NewHorizon.Automation.Worker
dotnet ef database update -p src/NewHorizon.Automation.Infrastructure -s src/NewHorizon.Automation.Worker
```

The real connection string lives in `dotnet user-secrets` (Worker project), never in
`appsettings.json`, which ships placeholders only. See [`deploy/README.md`](deploy/README.md) for
the database and secret setup, and for the install/update/uninstall scripts in `deploy/`.

## What actually runs (AutoShopCycle)

The only live workflow. A **timer** — not the design doc's §6 ERP push, which does not apply since
automation starts after a manually authorised OAF — enqueues one cycle; `JobDispatcherService`
claims it; the engine walks it. Three hosted services in
`Infrastructure/Hosting/`: scheduler, dispatcher, orphan recovery. They are registered by
`AddAutomationHostedServices()` separately from `AddAutomationInfrastructure()` so test hosts get
the application without a live timer.

Two things about this workflow that differ from the rest of the design:

- **A cycle has no document.** Its `DocumentId` is its start timestamp and its `DocumentType` is
  `Cycle`, so the per-document idempotency key cannot express "only one at a time". A second
  filtered unique index, `UX_AutomationJob_LiveCycle`, admits one live cycle per workflow type —
  excluding `Completed` as well as `Cancelled`, because unlike a document a cycle is meant to run
  again. `JobRepository.FindLiveEquivalentAsync` mirrors whichever index applies.
- **The agent holds no business logic here.** Each operation is GET → build body → POST. The rows
  travel as `JsonObject`, not a typed model, so every property the ERP sent comes back untouched;
  the agent only sorts by delivery date and sets one flag. Typing the row would silently drop
  fields on the way back.

ERP paths (`AutomationAgent:ErpEndpoints`) and the row property names
(`AutomationAgent:AutoShop`) are **configuration**, because most are still unconfirmed by the ERP
team — correct one there rather than editing code.

## Architecture invariants

These are the constraints that make the design work. Violating any of them silently breaks
duplicate-safety, tenancy, or the ERP boundary.

- **API-only in both directions.** ERP → Agent for control/read; Agent → ERP for execution.
  Neither side ever opens the other's database. No EF entity, connection string, or SQL in this
  solution may point at the ERP database.
- **The agent never writes ERP data directly.** Every ERP mutation goes through an ERP application
  API so ERP validation, permissions, audit, and transactions apply.
- **AI is never in the execution path.** `IDecisionService` only recommends (vendor, priority,
  risk); every create is a deterministic ERP API call.
- **Everything is idempotent and resumable.** Job level: unique filtered index on
  `IdempotencyKey = hash(Company, DocumentType, DocumentId, WorkflowType)` where
  `Status <> Cancelled`. Operation level: check stored `ErpDocumentRef` or query-before-create.
  Push triggers and the reconciliation poll both run, so both layers must hold.
- **Automation is license/config gated.** Disabled ⇒ the ERP behaves exactly as today.

## The execution model

Four levels, defined in §7 of the design doc:

| Level | Persisted as |
|---|---|
| Workflow — one run for one document | `AutomationJob` |
| Stage — SJO / OAF / MIL / CBOM / AutoShop, run sequentially | grouping column on steps |
| **Operation** — API-group inside a stage, **the checkpoint unit** | `AutomationJobStep` (one row each) |
| ERP API call | `AutomationLog` |

Checkpoint after *every* operation (status + `ErpDocumentRef` + payloads) before advancing.
**Resume = first operation whose status is not `Completed`.** Adding a new workflow means adding a
new `WorkflowDefinition` (ordered stages of ordered operations) — the engine, queue, retry, logging,
and API surface must not need changes.

Job states: `Pending → Running → {AwaitingApproval, Failed, Completed, Cancelled}`, with
`Failed → Running` on retry/resume and `AwaitingApproval → Running` on approve.

Semantics that are easy to get wrong:
- `resume` is failure recovery; `approve`/`reject` are business decisions on an `AwaitingApproval`
  gate and must record actor + remarks for audit. The approval UI never calls `resume`.
- Only transient failures (timeout, 5xx, breaker-open, network) retry with backoff+jitter. Business
  failures go straight to human review with a layman message — never retried.
- Manual retry re-queues at elevated priority; the claiming query orders by `Priority`.
- Job claiming uses `UPDATE TOP (@batch) ... WITH (UPDLOCK, READPAST)` so parallel workers skip
  locked rows instead of blocking.
- Errors carry both `TechnicalMessage` and `LaymanMessage`; the ERP UI shows layman by default.

## Triggers

Three sources funnel into one idempotent `enqueue`: (1) ERP push on Sales Order save
(fire-and-forget), (2) the `Pending` job set itself as the internal queue — no external broker,
(3) a 5-minute reconciliation poll that asks the ERP for documents with no started job. Write
execution logic once, behind `enqueue`.

## Configuration split

`appsettings.json` holds **only bootstrap**: SQL connection string, ERP base URL + service auth,
host port / loopback binding / inbound API key, and defaults (§16). Per-tenant runtime behavior —
Full/Partial mode, working hours, retry count, parallel workers, retention windows — lives in the
`AutomationConfig` table per Company+Module and is changed through the UI, never the file.
The agent reads config **fresh at the start of each job**; a running job keeps the mode it captured
at creation.

## Auth

Agent → ERP signs in at `POST /api/v1/auth/login` with `userName` / `password` / `connStr` — the
same endpoint the ERP UI uses. There is no service-token endpoint; this supersedes §15 of the design
doc. See [`.claude/context/erp-login-authentication.md`](.claude/context/erp-login-authentication.md)
for the verified contract and why the credentials sit in `appsettings.json` in clear.

`ErpTokenProvider` holds one token for the whole process (24-hour lifetime, honouring the ERP's
`validTo`), collapses a startup stampede into one login, and logs `ERP login successful …` with the
timestamp and expiry. `ErpAuthHandler : DelegatingHandler` attaches it, refreshes ~2 min before
expiry, and re-authenticates once on 401 — operation code never touches tokens.
`ErpLoginStartupService` (registered by `AddErpLoginStartup()`, separately from `AddErpClient()`)
signs in as soon as the agent starts.

ERP → Agent is protected by a shared inbound API key plus loopback-only binding.

## Open questions

§18 of the design doc lists five items to confirm before/at build — notably whether ERP create
endpoints accept an idempotency key (decides query-before-create logic), the exact operation lists
for the CBOM and AutoShop stages, and which operations require approval in Partial mode. Don't
invent answers; flag them.
