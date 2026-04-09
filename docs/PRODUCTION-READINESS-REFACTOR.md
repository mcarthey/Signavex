# Signavex — Production Readiness Refactor

**Date:** April 9, 2026
**Trigger:** Failed Azure deployment exposed environment parity gaps and architectural issues
**Goal:** Make the app deployable to any host (Azure, SmarterASP, etc.) without surprises

---

## Principles

1. **Environment parity** — develop against the same database engine you deploy to
2. **Migrations are the source of truth** — all schema AND seed data lives in EF Core migrations
3. **No conditional startup code** — Program.cs should not branch on environment for database setup
4. **No unnecessary SignalR** — static SSR by default, interactive mode only where genuinely needed
5. **Test before deploy** — verify against LocalDB/SQL Server locally before touching any host
6. **Incremental** — one change at a time, tested at each step

---

## Current State Audit

### Problem 1: Startup Code Does Too Much

`Program.cs` (lines 128-154) runs the following on every app startup:

```
1. SqliteMigrationTransition.TransitionIfNeededAsync()  — SQLite only
2. db.Database.MigrateAsync()                           — all providers
3. PRAGMA journal_mode=WAL                              — SQLite only
4. PRAGMA busy_timeout=5000                              — SQLite only
5. EconomicDataSeeder.SeedAsync()                        — seeds 21 FRED series if table empty
6. RoleSeeder.SeedAsync()                                — creates Free/Pro roles if missing
```

**Issues:**
- Steps 5 and 6 are imperative seed code that runs conditionally — should be migrations
- Step 1 is a legacy transition helper that shouldn't exist in production
- Steps 3-4 are SQLite-specific and don't belong in a production SQL Server path
- All of this blocks the first request, causing 30-40 second cold starts on Azure

### Problem 2: Blazor Server (SignalR) on Every Page

8 pages use `@rendermode InteractiveServer`, which requires a persistent WebSocket per user:

| Page | Interactive Elements | Needs SignalR? |
|------|---------------------|----------------|
| Dashboard | Scan button, filter chips, CSV export, 3s poll timer | **Partial** — button clicks can be forms, polling is the only real-time need |
| History | Ticker search, expandable rows | **No** — search can be a form GET, expand can be JS |
| Insights | Generate button, 5s poll timer | **No** — button can be a form, polling unnecessary |
| Economy | Sync button, 3s poll timer, indicator links | **No** — button can be a form, polling unnecessary |
| Backtest | Date picker, run button, CSV export | **Partial** — long-running backtest needs progress feedback |
| CandidateDetail | Chart toggle, signal expand | **No** — already uses JS interop for charts, expand can be JS |
| IndicatorDetail | None | **No** — pure data display |
| Settings | None | **No** — pure data display |

**Result:** Pages that should load in 200ms instead require a 30+ second WebSocket handshake.

### Problem 3: Dual Database Provider

- Development uses SQLite with `SqlServerTypeSqliteGenerator` translating SQL Server column types at runtime
- Production targets SQL Server
- Two code paths means bugs hide in the path you're not testing
- `SqliteMigrationTransition`, `SqlServerTypeSqliteGenerator`, and the provider switch in `ServiceCollectionExtensions.cs` all exist solely to support SQLite
- **Decision: Drop SQLite entirely.** Use SQL Server everywhere — LocalDB for local dev, Azure SQL for production. One provider, one code path, no translation layer.

### Problem 4: SOLID Violations That Caused Azure Failures

**4a. Liskov Substitution — Store implementations are hardcoded to SQLite**
- All stores are registered as `SqliteScanStateStore`, `SqliteScanHistoryStore`, etc. regardless of the `DatabaseProvider` config
- Even when SQL Server is configured, the "SQLite" store classes run — they work because they use `IDbContextFactory<SignavexDbContext>` (which IS provider-aware), but the naming is misleading and `WorkerScanOrchestrator` line 206 casts to the concrete type: `if (_stateStore is SqliteScanStateStore sqliteStore)`
- **Fix:** Rename stores to drop the "Sqlite" prefix (they're actually provider-agnostic EF Core stores). Move `SetCheckpointInactiveAsync` to the interface.

**4b. Single Responsibility — Program.cs does 7 things (317 lines)**
- Configuration binding, service registration, Identity setup, cookie config, database initialization, role/data seeding, route handlers (Stripe, auth, billing)
- All of this runs sequentially on startup, blocking the first request
- **Fix:** Extract into focused extension methods. Database initialization into a separate service.

**4c. Dependency Inversion — Concrete instantiation in services**
- `EconomicDashboardService` creates `new EconomicAnalysisService()`, `new CorrelationAnalysisService()`, `new RecommendationService()` directly instead of injecting interfaces
- `WorkerScanOrchestrator` casts `IScanStateStore` to `SqliteScanStateStore` (DIP violation)
- **Fix:** Extract interfaces, register in DI, inject via constructor.

**4d. Service Lifetime — Singletons with mutable cached state**
- `EconomicDashboardService`, `ScanDashboardService`, `BacktestRunnerService` are registered as Singletons
- They hold cached data in instance fields that persists across all HTTP requests and all users
- In a multi-user deployment, User A's cached results leak to User B
- **Fix:** Change to Scoped lifetime (per HTTP request). If caching is needed, use `IMemoryCache` or `IDistributedCache`.

**4e. Interface Segregation — Fat interfaces**
- `IScanStateStore` mixes checkpoint management, result storage, and status queries
- `IEconomicDataStore` mixes series metadata, observation data, and sync tracking
- UI components that only read data depend on interfaces that include write methods
- **Fix:** Split into focused interfaces (read vs. write, status vs. data).

---

## Refactor Plan

### Phase R1: Migrations as the Single Source of Truth

_Goal: Running `dotnet ef database update` against an empty database produces a fully configured, fully seeded, ready-to-use database. Program.cs calls `MigrateAsync()` and nothing else. No conditional startup code._

#### R1.0 — Stop the Worker

- [x] **R1.0** Stop the Signavex Scanner Windows Service before any changes
- `Stop-Service "Signavex Scanner"` (PowerShell as Admin)
- Verify it's stopped: `Get-Service "Signavex Scanner"` should show `Stopped`
- The Worker stays stopped for the entire refactor until R3.7 when we verify it against LocalDB
- **Do not proceed until the Worker is stopped. A running Worker writing to SQLite during migration work will cause data loss or corruption.**

#### R1.A — Set Up LocalDB and Verify Existing Migrations

LocalDB is the local development database from this point forward. No more SQLite.

- [x] **R1.1** Set up LocalDB
- Verify LocalDB is installed: `sqllocaldb info`
- Create instance if needed: `sqllocaldb create Signavex`
- Update `appsettings.Development.json`:
  ```json
  {
    "Signavex": {
      "DatabaseProvider": "SqlServer",
      "ConnectionString": "Server=(localdb)\\MSSQLLocalDB;Database=Signavex;Trusted_Connection=True;TrustServerCertificate=True;"
    }
  }
  ```

- [x] **R1.2** Verify the current migration chain applies cleanly to LocalDB

The current migration chain:
```
1. InitialSqlServer          — creates all tables (Identity + app tables)
2. AddSubscriptionFields     — adds SubscriptionPlan, StripeCustomerId to ApplicationUser
3. AddFundamentalsCache      — adds FundamentalsCache table
```

- Run `dotnet ef database update` against the empty LocalDB
- Confirm all tables are created with correct schema
- If it fails, fix the migration before proceeding
- **This is the baseline. Nothing else happens until this works.**

- [x] **R1.2b** Audit the DbContext (`SignavexDbContext.cs`) against the migration snapshot
- Verify every `DbSet<T>` has a corresponding table in the migration chain
- Verify every `OnModelCreating` configuration (indexes, keys, relationships) is reflected in the migrations
- If the snapshot is out of sync, create a new migration to capture the drift
- **Files:** `SignavexDbContext.cs`, `SignavexDbContextModelSnapshot.cs`

- [x] **R1.3** Verify all entity types are correct and complete
- Check every entity in `Persistence/Entities/` has the right properties
- Check that `FundamentalsCacheEntity` (added in Phase 6) is properly indexed
- Cross-reference with the `OnModelCreating` configuration
- **Files:** All files in `Persistence/Entities/`

#### R1.B — Move Seed Data Into Migrations

- [x] **R1.4** Create migration `SeedIdentityRoles`
- Move "Free" and "Pro" role creation into a migration's `Up()` method
- Use `migrationBuilder.InsertData()` for the `AspNetRoles` table
- `Down()` removes the seeded rows
- **Files:** New migration, then delete `RoleSeeder.cs`

- [x] **R1.5** Create migration `SeedEconomicSeries`
- Move the 21 FRED series definitions from `EconomicDataSeeder.cs` into a migration
- Use `migrationBuilder.InsertData()` for each series row
- Include all fields: SeriesId, Name, Description, Frequency, Units, SeasonalAdjustment, IsEnabled, Category
- `Down()` removes the seeded rows
- **Files:** New migration, then delete `EconomicDataSeeder.cs`

#### R1.C — Clean Up Startup and Configuration

- [x] **R1.6** Fix Worker appsettings — track the base config, secrets in environment files only
- The Worker's `appsettings.json` is currently gitignored AND contains real API keys
- Create a clean `Worker/appsettings.json` with the same placeholder pattern as Web:
  - Empty API key values
  - Full structure (Signavex options, DataProviders, Anthropic, SignalWeights, MarketSignalWeights)
  - No secrets
- Remove `src/Signavex.Worker/appsettings.json` from `.gitignore` so it's tracked
- Move real keys to `Worker/appsettings.Development.json` (already gitignored)
- Ensure `Worker/appsettings.Production.json` has production keys (already gitignored)
- **Both projects should follow the same pattern: tracked base config with structure, gitignored environment files with secrets**
- **Files:** `Worker/appsettings.json`, `.gitignore`

- [x] **R1.7** Align config structure between Web and Worker
- Both `appsettings.json` files should have identical structure for shared settings
- The `ConnectionString` should be empty in both base configs — populated in environment files
- SignalWeights, MarketSignalWeights, Universe should be identical in both
- Any setting that differs between Web and Worker should be documented with a comment
- **Files:** `Web/appsettings.json`, `Worker/appsettings.json`

- [x] **R1.8** Remove all conditional startup code from `Web/Program.cs`
- Remove `EconomicDataSeeder.SeedAsync()` call
- Remove `RoleSeeder.SeedAsync()` call
- Remove `SqliteMigrationTransition.TransitionIfNeededAsync()` call
- Remove SQLite PRAGMA statements
- What remains: `db.Database.MigrateAsync()` — and nothing else
- **Files:** `Web/Program.cs`

- [x] **R1.9** Apply the same cleanup to `Worker/Program.cs`
- Same removals as R1.8 (minus RoleSeeder which was Web-only)
- **Files:** `Worker/Program.cs`

- [x] **R1.10** Remove SQLite provider entirely
- Remove the SQLite branch from `ServiceCollectionExtensions.cs` — only the SQL Server path remains
- Remove `DatabaseProvider` config option (it's always SQL Server now)
- Remove `DataDirectory` config option (no more SQLite file path)
- Simplify `AddSignavexInfrastructure()` to just take a connection string
- **Files:** `ServiceCollectionExtensions.cs`, `SignavexOptions.cs`

- [x] **R1.11** Delete dead code
- Delete `EconomicDataSeeder.cs`
- Delete `RoleSeeder.cs`
- Delete `SqliteMigrationTransition.cs`
- Delete `SqlServerTypeSqliteGenerator.cs`
- Remove `Microsoft.EntityFrameworkCore.Sqlite` NuGet package from all projects
- **Files:** Delete from `Persistence/`, update `.csproj` files

#### R1.D — Verify End to End

- [x] **R1.12** Fresh database test against LocalDB
- Drop the LocalDB database: `sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "DROP DATABASE Signavex"`
- Run `dotnet run --project src/Signavex.Web`
- App should start, `MigrateAsync()` creates and seeds the database via migrations
- Browse all pages — verify economic series exist, roles exist, pages render
- **This must pass before moving to R2**

- [x] **R1.13** Run all tests
- `dotnet test` — all tests must pass
- Fix any tests that depended on SQLite, the seeders, or the transition code
- Tests should use LocalDB, not SQLite

- [ ] **R1.14** Verify Worker against same database
- Worker's `appsettings.Development.json` should point to the same LocalDB connection string
- Start the Worker, verify it connects and runs scans against the same database
- **Both Web and Worker must use the same LocalDB instance for local dev**

---

### Phase R2: Remove Unnecessary SignalR (Interactive Server Mode)

_Goal: Pages render as standard HTML via static SSR. No WebSocket connection needed for normal browsing. Interactive elements use client-side JavaScript._

- [ ] **R2.1** Remove `@rendermode InteractiveServer` from read-only pages
- **Settings.razor** — no interactive elements at all
- **IndicatorDetail.razor** — no interactive elements at all
- These pages will render as static SSR, load instantly, work identically
- **Verify:** Pages still render correctly, data still loads

- [ ] **R2.2** Convert History page to static SSR + forms
- Remove `@rendermode InteractiveServer`
- Ticker search: convert `@bind` + `@onclick` to a standard `<form method="get">` with query string
- Expandable scan rows: convert `@onclick="ToggleScan"` to `<details>` HTML element or vanilla JS toggle
- **Verify:** Search works, rows expand, no SignalR circuit

- [ ] **R2.3** Convert Insights page to static SSR + form
- Remove `@rendermode InteractiveServer`
- "Generate Now" button: convert to `<form method="post">` that enqueues the command
- Remove 5-second polling timer — user can refresh to see if brief is ready
- **Verify:** Brief displays, generate button works, page loads fast

- [ ] **R2.4** Convert Economy page to static SSR + form
- Remove `@rendermode InteractiveServer`
- "Sync Data" button: convert to `<form method="post">`
- Remove 3-second polling timer
- Indicator card links: already standard `<a>` tags, no change needed
- **Verify:** All indicators display, sync button works

- [ ] **R2.5** Convert CandidateDetail page to static SSR + JS
- Remove `@rendermode InteractiveServer`
- Signal expand/collapse: already using `@onclick` to toggle state — convert to vanilla JS `<details>` or `onclick` attribute
- Chart: already uses JS interop (`price-chart.js`) — load chart data via a `<script>` block or fetch API instead of Blazor interop
- Company profile: loads async — convert to a fetch call on page load
- Market signal expand: same pattern as stock signals
- **Verify:** Chart renders, signals expand, profile loads

- [ ] **R2.6** Simplify Dashboard
- Remove polling timer (3-second interval)
- "Run Scan" button: convert to `<form method="post">`
- Filter chips and sort: convert to `<form method="get">` with query parameters
- CSV export: keep as JS (`download.js` already handles this)
- Scan status: show last-known state from the database, not real-time polling. Add a small "Last updated: X minutes ago" timestamp.
- **Verify:** Dashboard loads with latest data, filters work, scan button enqueues command

- [ ] **R2.7** Simplify Backtest
- Date picker + run: convert to `<form method="post">`
- This is a long-running operation — after form POST, redirect to a results page or show "Backtest running, refresh to check"
- CSV export: keep as JS
- **Verify:** Backtest runs, results display

- [ ] **R2.8** Remove Blazor Server infrastructure (if no pages remain interactive)
- If all pages are static SSR, remove `autostart="false"` and the Blazor reconnection JS from `App.razor`
- Consider whether `blazor.web.js` is still needed (it handles static SSR enhanced navigation)
- **Verify:** All pages still work, no WebSocket connections in browser dev tools

---

### Phase R3: Migrate Existing Data to LocalDB

_Goal: Preserve the month of scan history, daily briefs, and economic data accumulated locally. Zero data loss._

#### R3.A — Pre-Migration Inventory

- [x] **R3.1** Record exact row counts from SQLite before touching anything
- Run counts against every table and save the output:
  ```sql
  SELECT 'ScanRuns' as tbl, COUNT(*) FROM ScanRuns
  UNION ALL SELECT 'ScanCandidates', COUNT(*) FROM ScanCandidates
  -- ... (all tables)
  ```
- Save this output to a file (`tools/pre-migration-counts.txt`)
- **This is the acceptance criteria for the migration — every count must match.**

- [x] **R3.2** Back up the SQLite database
- Copy `src/Signavex.Web/data/signavex.db` to a safe location outside the repo
- This is the rollback path — if anything goes wrong, we restore from this copy
- **Do not proceed until the backup exists and is verified readable**

#### R3.B — Run the Migration

- [x] **R3.3** Stop any running processes that use the database
- Stop the Worker Windows Service
- Stop the Web app if running
- No process should hold a lock on either SQLite or LocalDB during migration

- [x] **R3.4** Run the data migration tool
- Use `tools/MigrateData` project
- Source: SQLite backup (read-only mode)
- Target: LocalDB (freshly migrated from R1)
- The tool reads from source, writes to target — source is never modified
- **Watch for errors — any failure means stop and investigate, don't retry blindly**

- [x] **R3.5** Verify row counts match
- Run the same count query against LocalDB
- Compare every table count against `pre-migration-counts.txt`
- If any count differs, investigate before proceeding
- **Every row must be accounted for**

#### R3.C — Verify the Application

- [x] **R3.6** Verify the migrated data through the UI
- Start the Web app pointing at LocalDB
- Browse History — all scan runs visible with correct dates and candidate counts
- Browse Insights — all daily briefs visible with correct content
- Browse Economy — all economic indicators with sparklines and observations
- Check a candidate detail page — signal results, scores intact
- **Spot-check at least 3 pages of data visually — don't just trust row counts**

- [ ] **R3.7** Verify the Worker against LocalDB
- Start the Worker pointing at LocalDB
- Verify it picks up the existing scan schedule
- Verify it doesn't re-run or duplicate anything

#### R3.D — Retire SQLite

- [ ] **R3.8** Keep the SQLite backup for 30 days
- Store the backup copy somewhere safe (external drive, cloud storage, etc.)
- Do NOT delete it until we've been running on LocalDB for at least 30 days without issues
- After 30 days, the backup can be deleted

- [ ] **R3.9** Remove the SQLite data directory from the project
- Delete `src/Signavex.Web/data/signavex.db` (the live copy, not the backup)
- Remove `data/` from `.gitignore` if it was listed there
- Verify the app no longer references any SQLite file path

---

### Phase R4: Test Coverage Gaps

_Goal: Confidence that the app works before deploying anywhere._

- [ ] **R4.1** Audit existing test coverage
- Current: 207 tests (Domain: 46, Signals: 57, Engine: 26, Worker: 2, Infrastructure: 76)
- Worker has only 2 tests — significant gap
- No integration tests that hit a real SQL Server database
- No smoke tests for page rendering

- [ ] **R4.2** Add integration tests against LocalDB
- Test that migrations apply cleanly to a fresh database
- Test that seed data (economic series, roles) exists after migration
- Test that scan results persist and load correctly via SQL Server

- [ ] **R4.3** Add page rendering smoke tests
- Use `WebApplicationFactory<Program>` to spin up the app in-memory
- Verify each page returns 200 and contains expected content
- These catch startup crashes and missing DI registrations before deployment

- [ ] **R4.4** Add Worker service tests
- Test that `DailyScanBackgroundService` correctly schedules scans
- Test that `FundamentalsBackfillService` respects rate limits
- Test that `EconomicDataSyncService` syncs correctly

---

### Phase R5: SOLID Refactoring

_Goal: Clean architecture that works identically in any hosting environment. No concrete casts, no static state, no leaked caches._

#### R5.A — Fix Store Naming and Interface Violations

- [ ] **R5.1** Rename store implementations to drop "Sqlite" prefix
- `SqliteScanStateStore` → `ScanStateStore`
- `SqliteScanHistoryStore` → `ScanHistoryStore`
- `SqliteScanCommandStore` → `ScanCommandStore`
- `SqliteEconomicDataStore` → `EconomicDataStore`
- `SqliteDailyBriefStore` → `DailyBriefStore`
- These are EF Core stores, not SQLite-specific — the name was misleading
- Update all DI registrations in `ServiceCollectionExtensions.cs`
- **Verify:** Build succeeds, all tests pass

- [ ] **R5.2** Move `SetCheckpointInactiveAsync` to the `IScanStateStore` interface
- Currently only accessible via concrete cast in `WorkerScanOrchestrator` line 206
- Add to interface, remove the `is SqliteScanStateStore` cast
- **Files:** `IScanStateStore.cs`, `ScanStateStore.cs` (renamed), `WorkerScanOrchestrator.cs`

- [ ] **R5.3** Split `IScanStateStore` into focused interfaces
- `IScanCheckpointStore` — SaveCheckpoint, LoadCheckpoint, DeleteCheckpoint, SetInactive
- `IScanResultStore` — SaveCompletedResult, LoadLatestResult
- `IScanStatusProvider` — GetScanStatus
- Update consumers to inject only what they need
- **Files:** New interfaces in Domain, update all store implementations and consumers

- [ ] **R5.4** Split `IEconomicDataStore` into focused interfaces
- `IEconomicSeriesStore` — GetAllSeries, GetSeriesById
- `IEconomicObservationStore` — GetObservations, UpsertObservations
- `IEconomicSyncTracker` — GetSyncStatus, UpdateSyncTimestamp
- **Files:** New interfaces in Domain, update store implementation and consumers

#### R5.B — Fix Service Lifetimes and Dependency Inversion

- [ ] **R5.5** Change application services from Singleton to Scoped
- `ScanDashboardService` → Scoped
- `EconomicDashboardService` → Scoped (remove instance-level cache fields)
- `BacktestRunnerService` → Scoped
- `DailyBriefService` → Scoped
- If caching is needed, use `IMemoryCache` (already registered)
- **Verify:** No data leaks between requests in multi-user scenario

- [ ] **R5.6** Extract interfaces for analysis services and inject via DI
- Create `IEconomicAnalyzer` for `EconomicAnalysisService`
- Create `ICorrelationAnalyzer` for `CorrelationAnalysisService`
- Create `IRecommendationEngine` for `RecommendationService`
- Replace all `new EconomicAnalysisService()` with constructor injection
- Register in DI as Scoped
- **Files:** New interfaces in Domain, update `EconomicDashboardService`, `DailyBriefBackgroundService`

- [ ] **R5.7** Wrap Stripe in an injectable service
- Create `IPaymentGateway` interface
- Create `StripePaymentGateway` implementation that uses `IOptions<StripeOptions>`
- Remove `StripeConfiguration.ApiKey = ...` static assignment from Program.cs
- Move Stripe route handlers into a proper controller or minimal API group
- **Files:** New interface + implementation, update Program.cs route handlers

#### R5.C — Clean Up Program.cs

- [ ] **R5.8** Extract Program.cs into focused extension methods
- `builder.Services.AddSignavexIdentity(config)` — Identity + cookie config
- `builder.Services.AddSignavexApplicationServices()` — dashboard/backtest/brief services
- `app.MapSignavexAuthRoutes()` — login, logout, register routes
- `app.MapSignavexBillingRoutes()` — Stripe upgrade, webhook, portal routes
- Program.cs should read like a table of contents, not a novel
- **Files:** New extension method classes, simplified `Program.cs`

- [ ] **R5.9** Extract route handlers into minimal API groups or controllers
- Auth routes (logout, register redirect) → `AuthEndpoints.cs`
- Billing routes (upgrade, webhook, portal) → `BillingEndpoints.cs`
- **Files:** New endpoint classes, remove from Program.cs

#### R5.D — Verify

- [ ] **R5.10** Run all tests
- All existing tests must pass
- New interface splits may require updating test mocks
- **No behavioral changes — only structural. If a test fails, the refactor introduced a bug.**

---

## Execution Order

```
R1.0 (stop Worker)
  └──> R1.A (set up LocalDB, verify existing migrations)
         └──> R1.B (seed data → migrations)
                └──> R1.C (clean up Program.cs, remove SQLite)
                       └──> R1.D (verify end to end against LocalDB)
                              └──> R2 (remove SignalR, one page at a time)
                                     └──> R3 (migrate existing data to LocalDB)
                                            └──> R4 (add missing tests)
                                                   └──> R5 (SOLID refactoring)
                                                          └──> Ready to deploy
```

Each phase is independently valuable. We can stop after any phase and the app is better than before. Every step is verified before moving to the next.

---

## Files Affected

| Phase | Files Changed | Files Deleted |
|-------|--------------|---------------|
| R1.0 | None (operational) | |
| R1.1-R1.2 | `appsettings.Development.json` | |
| R1.3 | Verify only — no changes | |
| R1.4 | New migration `SeedIdentityRoles` | |
| R1.5 | New migration `SeedEconomicSeries` | |
| R1.6-R1.7 | `Worker/appsettings.json`, `.gitignore`, `Web/appsettings.json` | |
| R1.8-R1.9 | `Web/Program.cs`, `Worker/Program.cs` | |
| R1.10 | `ServiceCollectionExtensions.cs`, `SignavexOptions.cs` | |
| R1.11 | `.csproj` files | `EconomicDataSeeder.cs`, `RoleSeeder.cs`, `SqliteMigrationTransition.cs`, `SqlServerTypeSqliteGenerator.cs` |
| R2.1-R2.7 | 8 `.razor` files, possibly new JS | |
| R2.8 | `App.razor` | |
| R3.1-R3.9 | `tools/MigrateData`, `.gitignore` | `data/signavex.db` |
| R4.1-R4.4 | New test files, test `.csproj` updates | |
| R5.1 | Rename 5 store files, update `ServiceCollectionExtensions.cs` | |
| R5.2 | `IScanStateStore.cs`, `ScanStateStore.cs`, `WorkerScanOrchestrator.cs` | |
| R5.3-R5.4 | New interfaces in Domain, update all store consumers | |
| R5.5 | `Web/Program.cs` (service lifetimes) | |
| R5.6 | New interfaces, `EconomicDashboardService.cs`, `DailyBriefBackgroundService.cs` | |
| R5.7 | New `IPaymentGateway`, `StripePaymentGateway`, `Program.cs` | |
| R5.8-R5.9 | New extension methods/endpoint classes, simplified `Program.cs` | |

---

## Success Criteria

- [ ] Single database provider: SQL Server everywhere (LocalDB local, Azure SQL production)
- [ ] `dotnet ef database update` against a fresh LocalDB produces a fully seeded, ready-to-use database
- [ ] No conditional startup code in Program.cs — just `MigrateAsync()`
- [ ] No SQLite code remaining in the codebase
- [ ] Zero pages require WebSocket connections for normal browsing
- [ ] All tests pass against SQL Server (LocalDB)
- [ ] App starts and serves first page in under 5 seconds
- [ ] No concrete type casts in service code (no `is SqliteScanStateStore`)
- [ ] No `new ServiceClass()` in application services — all dependencies injected
- [ ] No static mutable state (no `StripeConfiguration.ApiKey = ...`)
- [ ] No singleton services with mutable instance-level caches
- [ ] Program.cs is under 50 lines — delegates to extension methods
- [ ] A new developer can clone the repo, run `dotnet ef database update`, and have a working app
