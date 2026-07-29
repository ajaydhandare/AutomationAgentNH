# Deployment / environment setup

## The two databases

The agent has **its own** database, entirely separate from the ERP's. Nothing in this solution may
ever connect to the ERP database — the agent reads and writes ERP data only through ERP application
APIs, so ERP validation, permissions, audit and transactions always apply.

| | Database | Owned by | This solution connects? |
|---|---|---|---|
| ERP | e.g. `PGTPL_MihiR_A_11062026` | ERP product | **Never** |
| Automation agent | `PGTPL_AutomationAgent` | this solution | Yes — the only connection string it has |

The `PGTPL_` prefix matches the client-code grouping already used on the server. The `_<user>_A_<ddMMyyyy>`
suffix is deliberately **not** used: that pattern marks dated ERP copies, whereas the agent database
is long-lived state (jobs, checkpoints, logs) that must outlive any individual ERP refresh.

## Provisioning a new installation

```powershell
# 1. Create the database (run against master on the target instance)
sqlcmd -S <server>\<instance> -Q "IF DB_ID('PGTPL_AutomationAgent') IS NULL CREATE DATABASE [PGTPL_AutomationAgent];"
sqlcmd -S <server>\<instance> -d PGTPL_AutomationAgent -Q "ALTER DATABASE [PGTPL_AutomationAgent] SET RECOVERY SIMPLE; ALTER DATABASE [PGTPL_AutomationAgent] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;"

# 2. Point the agent at it (see "Secrets" below), then create the schema
dotnet ef database update -p src/NewHorizon.Automation.Infrastructure -s src/NewHorizon.Automation.Worker

# 3. Seed the per-module settings rows (idempotent)
sqlcmd -S <server>\<instance> -d PGTPL_AutomationAgent -i deploy/sql/002_SeedAutomationConfig.sql
```

`READ_COMMITTED_SNAPSHOT` is on so the management/read API's queries never block the workers'
job-claiming `UPDLOCK, READPAST` updates.

Offline alternative to step 2, where `dotnet ef` cannot run on the server:
`deploy/sql/001_InitialAutomationSchema.sql` is the same migration as an idempotent script.

## Secrets

`appsettings.json` carries **placeholders only** (`<store-protected>`, `<shared-secret>`) and is
committed. Real values never are.

Local development — per-developer secret store, outside the repository:

```powershell
cd src/NewHorizon.Automation.Worker
dotnet user-secrets set "AutomationAgent:Database:ConnectionString" "Server=<server>\<instance>;Database=PGTPL_AutomationAgent;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "AutomationAgent:ErpApi:ClientSecret" "<real-secret>"
dotnet user-secrets set "AutomationAgent:Host:InboundApiKey" "<real-key>"
```

Servers: use environment variables (`AutomationAgent__Database__ConnectionString`) or a
DPAPI/machine-protected store. `AutomationDbContextFactory` reads user secrets and environment
variables with the same precedence the running service uses, so `dotnet ef` always targets the same
database the agent does.

### SQL login

Use a **dedicated least-privilege SQL login** for the agent — `db_datareader`, `db_datawriter` and
`EXECUTE` on the automation database, and nothing on the ERP database. Do not ship `sa`, and do not
ship a blank password; either would give the agent's process full control of every database on the
instance, including the ERP's.

## Verifying an installation

```powershell
curl http://localhost:5080/api/automation/health
```

Expect `checks.database = "Healthy"`. `checks.erpApi` stays `Unhealthy` until `ErpApi:BaseUrl` and
the service credentials point at a reachable ERP.

## Update sequence

Stop the service → deploy binaries → `dotnet ef database update` → start. In-flight jobs resume on
their own: resume is the first operation whose status is not `Completed`.
