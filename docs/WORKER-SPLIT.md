# Worker Split — Setup & Operations Guide

## Architecture Overview

Signavex is split into two processes that communicate via the shared SQLite database:

```
Signavex.Worker  — Windows Service that owns scan orchestration
Signavex.Web     — Blazor UI viewer (read-only except for scan commands)
```

**Worker WRITES:** checkpoints (with scalar progress columns), completed results, command status updates
**Web WRITES:** scan command requests only
**Web READS:** everything else (results, progress, history)

Both processes point to the same `signavex.db` SQLite file. WAL mode + `PRAGMA busy_timeout=5000` ensure safe concurrent access.

---

## Installing the Worker as a Windows Service

From an **elevated (Admin) PowerShell**:

```powershell
# Publish
dotnet publish src/Signavex.Worker -c Release -o C:\Signavex\Worker

# Install as auto-start service
sc.exe create "Signavex Scanner" binPath="C:\Signavex\Worker\Signavex.Worker.exe" start=auto

# Start it
sc.exe start "Signavex Scanner"
```

The `start=auto` flag means the service **restarts automatically on reboot**. If the machine shuts down mid-scan, the checkpoint is preserved and the service auto-resumes on next boot.

### Managing the Service

```powershell
sc.exe query "Signavex Scanner"    # Check status
sc.exe stop "Signavex Scanner"     # Stop
sc.exe start "Signavex Scanner"    # Start
sc.exe delete "Signavex Scanner"   # Uninstall
```

### Updating After Code Changes

The service runs from the published files at `C:\Signavex\Worker\`, not your source code. To deploy changes:

```powershell
sc.exe stop "Signavex Scanner"
dotnet publish src/Signavex.Worker -c Release -o C:\Signavex\Worker
sc.exe start "Signavex Scanner"
```

**Note:** The stop may take a few seconds if a stock is mid-evaluation — the service waits for the current HTTP call to finish before shutting down gracefully.

---

## Starting the Web App

The Web app is just a viewer. Start and stop it whenever you want — it does not affect scans.

```bash
cd src/Signavex.Web
dotnet run
```

You can also run it from the repo root: `dotnet run --project src/Signavex.Web`

---

## Shared Database

Both projects share the same database via the `Signavex.DataDirectory` setting in `appsettings.json`:

```json
"Signavex": {
  "DataDirectory": "E:\\Documents\\Work\\dev\\repos\\Signavex\\data",
  ...
}
```

This absolute path is configured in both `src/Signavex.Web/appsettings.json` and `src/Signavex.Worker/appsettings.json`. If `DataDirectory` is empty or missing, each process falls back to `{ContentRootPath}/data/` (which would create separate DBs — avoid this).

### Database Migration

Whichever process starts first auto-applies pending EF Core migrations. The second process sees the migration is already applied and skips it. This is safe and idempotent.

---

## API Key Configuration

- **Web:** Keys are in `src/Signavex.Web/appsettings.Development.json` (overrides the empty placeholders in base `appsettings.json`)
- **Worker:** Keys are in `src/Signavex.Worker/appsettings.json` directly (since the Windows Service runs in Production environment, not Development)

Both must have the same Polygon, AlphaVantage, and FRED keys.

---

## How Scans Work Now

1. **Manual scan:** Dashboard "Run Scan" button writes a row to `ScanCommands` table
2. **Worker polls:** `ScanCommandPollingService` checks every 5 seconds for new commands
3. **Worker executes:** `WorkerScanOrchestrator` runs the scan, saving scalar progress to `ScanCheckpoints` after each stock
4. **Web polls:** Dashboard polls `ScanCheckpoints` every 3 seconds to show live progress
5. **Completion:** Worker saves `CompletedScanResult` to `ScanRuns`/`ScanCandidates`, deletes checkpoint, marks command complete
6. **Web detects:** Next poll sees `IsActive=false`, reloads latest results from `ScanRuns`

### Daily Scan
The Worker runs a daily scan at **5:00 PM ET** (after market close), skipping weekends. This is handled by `DailyScanBackgroundService` in the Worker.

### Resume on Crash
If the Worker stops mid-scan, `ScanResumeBackgroundService` auto-detects the checkpoint on restart and resumes from where it left off (within 48 hours).

---

## Existing Data — No Data Loss

The `AddWorkerSplitSchema` migration only:
- **Adds** 6 scalar columns to `ScanCheckpoints` (all with safe defaults)
- **Creates** a new `ScanCommands` table

It does NOT modify or delete any existing rows. All historical scans, candidates, and checkpoints are preserved.

The existing database was copied from `src/Signavex.Web/data/` to the shared `data/` directory at the repo root.

---

## What Changed (File Summary)

### New Files
| File | Purpose |
|------|---------|
| `src/Signavex.Domain/Models/ScanCommand.cs` | Command queue record |
| `src/Signavex.Domain/Models/ScanStatus.cs` | Lightweight scan progress record |
| `src/Signavex.Domain/Interfaces/IScanCommandStore.cs` | Command store interface |
| `src/Signavex.Infrastructure/Persistence/Entities/ScanCommandEntity.cs` | EF entity for ScanCommands table |
| `src/Signavex.Infrastructure/Persistence/SqliteScanCommandStore.cs` | SQLite implementation of IScanCommandStore |
| `src/Signavex.Worker/` | Entire new project (7 files) |
| `src/Signavex.Web/Services/ScanDashboardService.cs` | DB-only dashboard service |

### Modified Files
| File | Change |
|------|--------|
| `src/Signavex.Domain/Configuration/SignavexOptions.cs` | Added `DataDirectory` property |
| `src/Signavex.Domain/Interfaces/IScanStateStore.cs` | Added `GetScanStatusAsync()` |
| `src/Signavex.Infrastructure/Persistence/Entities/ScanCheckpointEntity.cs` | Added scalar progress columns |
| `src/Signavex.Infrastructure/Persistence/SignavexDbContext.cs` | Added `ScanCommands` DbSet |
| `src/Signavex.Infrastructure/Persistence/SqliteScanStateStore.cs` | Populates scalar columns, implements `GetScanStatusAsync()`, added `SetCheckpointInactiveAsync()` |
| `src/Signavex.Infrastructure/ServiceCollectionExtensions.cs` | Registers `IScanCommandStore` |
| `src/Signavex.Web/Program.cs` | Removed scan background services, added `ScanDashboardService`, reads `DataDirectory` from config |
| `src/Signavex.Web/appsettings.json` | Added `DataDirectory` to `Signavex` section |
| `src/Signavex.Web/Components/Pages/Dashboard.razor` | Switched from event-driven to 3s polling model |
| `src/Signavex.Web/Components/Pages/CandidateDetail.razor` | Removed `ScanResultsService` dependency, DB-only |
| `Signavex.sln` | Added Worker project |

### Deleted Files
| File | Reason |
|------|--------|
| `src/Signavex.Web/Services/ScanResultsService.cs` | Replaced by Worker orchestrator + Web dashboard service |
| `src/Signavex.Web/Services/DailyScanBackgroundService.cs` | Moved to Worker |
| `src/Signavex.Web/Services/ScanResumeBackgroundService.cs` | Moved to Worker |

### Unchanged
- `Signavex.Signals` — completely unchanged
- `Signavex.Engine` — completely unchanged
- `History.razor`, `Backtest.razor`, `Settings.razor`, `Home.razor` — unchanged
- All shared components (MarketContextBar, ScoreBadge, SignalBreakdown, NavMenu, MainLayout)
- All test projects (57 + 26 + 51 = 134 tests, all passing)

---

## Troubleshooting

### "Connection string keyword 'busytimeout' is not supported"
Microsoft.Data.Sqlite does not support `BusyTimeout` in the connection string. The busy timeout is set via `PRAGMA busy_timeout=5000` in each project's `Program.cs` startup, alongside the WAL mode PRAGMA.

### Both processes creating separate databases
Ensure both `appsettings.json` files have the same absolute `DataDirectory` path under the `Signavex` section. If `DataDirectory` is empty, the fallback is `{ContentRootPath}/data/` which differs per project.

### Scan not starting after clicking "Run Scan"
- Verify the Worker is running
- Check Worker logs for command polling activity
- The Worker polls every 5 seconds, so there's up to a 5-second delay

### Dashboard not showing progress
- The Web polls every 3 seconds
- Verify the Worker is writing to the same database
- Check that `ScanCheckpoints.IsActive` is `true` in the DB during a scan
