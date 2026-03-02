# Signavex Deployment Gaps — Localhost to Production

A practical roadmap for publishing Signavex as a public site with a free economic dashboard and gated stock scanner.

---

## 1. Current State

| Component | Current | Notes |
|-----------|---------|-------|
| Framework | .NET 8.0, Blazor Server | Interactive server-side rendering |
| Web | `Signavex.Web` — Blazor Server app | Serves all pages |
| Worker | `Signavex.Worker` — Windows Service | Runs scans, syncs data, generates briefs |
| Database | SQLite (`signavex.db`) | Shared file between Web + Worker |
| Auth | None | All pages publicly accessible |
| Hosting | `localhost` | Single-machine only |
| Data Providers | Polygon.io, Alpha Vantage, FRED, Anthropic | API keys in `appsettings.json` |
| Tests | 205 automated tests (xUnit) | Domain, Infrastructure, Engine, Signals |

---

## 2. Database: SQLite → PostgreSQL

SQLite works well for single-user local use but has concurrency limitations that will cause issues under multi-user load. Blazor Server's stateful circuits mean many concurrent DB readers/writers.

### What to change

1. **NuGet**: Replace `Microsoft.EntityFrameworkCore.Sqlite` with `Npgsql.EntityFrameworkCore.PostgreSQL` in `Signavex.Infrastructure.csproj`

2. **DbContext registration** in `ServiceCollectionExtensions.cs`:
   ```csharp
   // Before
   options.UseSqlite($"Data Source={dbPath}");
   // After
   options.UseNpgsql(connectionString);
   ```

3. **Remove PRAGMA statements** from both `Program.cs` files (Web + Worker):
   ```csharp
   // Remove these two lines:
   await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL");
   await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000");
   ```

4. **Regenerate all migrations** for PostgreSQL (SQLite migrations are not compatible)

5. **DateOnly handling**: PostgreSQL handles `DateOnly` natively via Npgsql, but verify `EconomicObservationEntity.Date` and `DailyBriefEntity.Date` column mappings

### Estimated effort
2-4 hours. The EF Core abstraction handles most of it; the main risk is migration regeneration.

---

## 3. Hosting

### Recommended architecture

| Service | Azure Resource | Tier | Monthly Cost |
|---------|---------------|------|-------------|
| Web (Blazor Server) | App Service | B1 (Basic) | ~$13/mo |
| Worker | Container Instance or WebJob | B1 or per-use | ~$5-10/mo |
| Database | Azure Database for PostgreSQL | Burstable B1ms | ~$12/mo |
| Total baseline | | | **~$30-35/mo** |

### Blazor Server hosting considerations

- **Stateful circuits**: Each user holds a SignalR connection. B1 tier supports ~100 concurrent connections. Monitor circuit count.
- **Sticky sessions**: Required if scaling to multiple instances. Azure App Service has built-in affinity.
- **WebSocket support**: Must be enabled in App Service configuration.

### Worker deployment

The Worker currently runs as a Windows Service. For Azure:

- **Option A**: Azure Container Instance running the Worker as a container (recommended for Linux)
- **Option B**: Azure WebJob attached to the App Service
- **Option C**: Keep as Windows Service on an Azure VM (most expensive, least cloud-native)

### Timezone change for Linux

The Worker uses `TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")` which is Windows-only. For Linux containers:

```csharp
// Before (Windows only)
TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")

// After (cross-platform)
TimeZoneInfo.FindSystemTimeZoneById("America/New_York")
```

This appears in:
- `DailyScanBackgroundService.cs`
- `EconomicDataSyncService.cs`
- `DailyBriefBackgroundService.cs`

---

## 4. Authentication & Authorization

### Approach

Use ASP.NET Identity with cookie auth (simplest for Blazor Server) or Auth0/Entra ID for external auth.

### Public vs gated routes

| Route | Access | Content |
|-------|--------|---------|
| `/economy` | Public | Economic dashboard |
| `/insights` | Public | AI daily briefs |
| `/about` | Public | About page |
| `/` (Dashboard) | Gated | Stock scan results |
| `/history` | Gated | Scan history |
| `/backtest` | Gated | Backtesting tool |
| `/settings` | Gated | Configuration |

### Implementation steps

1. Add `Microsoft.AspNetCore.Identity.EntityFrameworkCore` NuGet
2. Create `ApplicationUser : IdentityUser` class
3. Add Identity tables to the DbContext (or a separate auth DbContext)
4. Add `[Authorize]` attribute to gated pages
5. Add login/register pages
6. Configure cookie auth in `Program.cs`

### Important

- Do NOT weaken auth to fix 401s. Blazor Server auth uses the circuit's `AuthenticationState`.
- Test both anonymous and authenticated flows for every page.

---

## 5. API Keys & Secrets Management

### Current state

API keys live in `appsettings.json` (Worker's is gitignored, Web's has empty placeholders).

### Production approach

| Secret | Source |
|--------|--------|
| Polygon API key | Azure Key Vault or environment variable |
| Alpha Vantage API key | Azure Key Vault or environment variable |
| FRED API key | Azure Key Vault or environment variable |
| Anthropic API key | Azure Key Vault or environment variable |
| PostgreSQL connection string | Azure Key Vault or App Service connection strings |

### Implementation

```csharp
// In Program.cs
builder.Configuration.AddAzureKeyVault(
    new Uri("https://signavex-vault.vault.azure.net/"),
    new DefaultAzureCredential());
```

Or use App Service Application Settings (mapped to environment variables), which ASP.NET Core reads automatically via `IConfiguration`.

---

## 6. Data Licensing

| Provider | License | Redistribution |
|----------|---------|---------------|
| FRED (Federal Reserve) | Public domain | Free to redistribute. Attribution appreciated but not required. |
| Polygon.io | Commercial | Free tier: personal use only. Starter ($29/mo) or higher needed for public redistribution. Check [Polygon market data licensing](https://polygon.io/pricing). |
| Alpha Vantage | Commercial | Free tier: 25 req/day, personal use. Premium required for redistribution. |
| Anthropic (Claude API) | Commercial | Generated content can be published. Standard API pricing applies ($3-5/MTok input). |

### Action required

- Upgrade Polygon to at least Starter tier before going public
- Verify Alpha Vantage redistribution terms for your usage tier
- Add data attribution where required

---

## 7. Legal Requirements

### Must-have before launch

1. **"Not Financial Advice" disclaimer** — appears on every page with financial data. Already present in AI-generated briefs footer.

2. **Terms of Service** — cover:
   - Data is informational only, not investment advice
   - No guarantee of accuracy or timeliness
   - User assumes all risk for decisions based on the data
   - Data sourced from third parties (FRED, Polygon, etc.)

3. **Privacy Policy** — cover:
   - What data is collected (if auth: email, usage data)
   - How data is stored and protected
   - Cookie usage (Blazor Server uses cookies for circuit affinity)
   - GDPR/CCPA compliance if applicable

4. **Data source attribution** — visible credits for FRED, market data providers

---

## 8. CI/CD Pipeline

### Recommended: GitHub Actions

```yaml
# .github/workflows/deploy.yml
name: Build, Test, Deploy

on:
  push:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore --configuration Release
      - run: dotnet test --no-build --configuration Release

  deploy-web:
    needs: build-and-test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet publish src/Signavex.Web -c Release -o ./publish-web
      - uses: azure/webapps-deploy@v3
        with:
          app-name: signavex-web
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
          package: ./publish-web

  deploy-worker:
    needs: build-and-test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet publish src/Signavex.Worker -c Release -o ./publish-worker
      # Deploy to Container Instance or WebJob
```

### Key points

- **205 tests gate deployment** — if tests fail, nothing deploys
- Build in Release mode for production
- Separate deployment targets for Web and Worker
- Secrets stored in GitHub repository secrets

---

## 9. Monitoring & Observability

### Recommended: Application Insights

```csharp
// In Web Program.cs
builder.Services.AddApplicationInsightsTelemetry();

// In Worker Program.cs
builder.Services.AddApplicationInsightsTelemetryWorkerService();
```

### Key metrics to track

| Metric | Why | Alert Threshold |
|--------|-----|-----------------|
| Blazor circuit count | Stateful connections = resource pressure | > 80 on B1 |
| Scan duration | Worker health | > 60 min (typical: 30-45 min) |
| API rate limit hits | Data provider throttling | > 10/hour |
| Brief generation failures | AI service health | Any failure |
| DB connection pool exhaustion | PostgreSQL health | > 90% usage |
| Response time (p95) | User experience | > 2s |
| Error rate | Application health | > 1% |

### Structured logging

The codebase already uses `ILogger<T>` throughout. Add a sink for Application Insights:

```csharp
builder.Logging.AddApplicationInsights();
```

---

## 10. Cost Estimate Breakdown

### Monthly baseline (~$30-35/mo)

| Resource | Tier | Cost |
|----------|------|------|
| App Service (Web) | B1 (1 core, 1.75 GB) | $13.14 |
| PostgreSQL Flexible Server | Burstable B1ms | $12.27 |
| Container Instance (Worker) | 1 vCPU, 1.5 GB, ~12 hr/day | $5-8 |
| Application Insights | First 5 GB free | $0 |
| **Total** | | **~$30-35** |

### Variable costs

| Resource | Cost |
|----------|------|
| Polygon API (Starter) | $29/mo |
| Alpha Vantage (Premium) | $49.99/mo (if needed) |
| Anthropic API (Claude) | ~$0.50-2/mo (1 brief/day) |
| Bandwidth (egress) | First 100 GB free |

### Scaling costs

If traffic grows beyond B1 tier:
- **S1 App Service** ($69/mo) — supports auto-scaling, staging slots
- **PostgreSQL General Purpose** ($100+/mo) — needed for heavy concurrent reads

---

## 11. Priority Roadmap

### Must-have (before launch)

| Item | Effort | Risk |
|------|--------|------|
| PostgreSQL migration | 2-4 hours | Low — EF Core handles it |
| API key extraction to env vars/Key Vault | 1-2 hours | Low |
| Timezone fix for Linux | 30 min | Low |
| "Not financial advice" disclaimer on all pages | 1 hour | Low |
| Terms of Service + Privacy Policy pages | 2-3 hours | Low |
| Basic auth (ASP.NET Identity) | 4-6 hours | Medium |
| Route gating (`[Authorize]`) | 1-2 hours | Low — once auth exists |
| CI/CD pipeline (GitHub Actions) | 2-3 hours | Low |
| Azure resource provisioning | 1-2 hours | Low |
| Polygon licensing upgrade | 0 (just pay) | Low |

### Nice-to-have (launch week)

| Item | Effort | Risk |
|------|--------|------|
| Application Insights | 1-2 hours | Low |
| Health check endpoints | 1 hour | Low |
| Custom error pages (404, 500) | 1-2 hours | Low |
| Rate limiting on public pages | 1-2 hours | Low |
| Stripe integration for paid tier | 4-8 hours | Medium |

### Post-launch

| Item | Effort | Risk |
|------|--------|------|
| Email alerts for scan results | 4-6 hours | Low |
| User profiles / watchlists | 8-12 hours | Medium |
| Mobile-responsive improvements | 4-6 hours | Low |
| API endpoint for programmatic access | 8-12 hours | Medium |
| Multi-tenant Worker (shared scans) | 8-12 hours | High |

---

## 12. Deployment Checklist

Before first deploy:

- [ ] PostgreSQL migration created and tested locally
- [ ] All PRAGMA statements removed
- [ ] Timezone strings changed to IANA format (`America/New_York`)
- [ ] API keys moved to environment variables / Key Vault
- [ ] Auth implemented and tested (anonymous + authenticated flows)
- [ ] Route gating applied to scanner pages
- [ ] "Not financial advice" disclaimer on all data pages
- [ ] Terms of Service and Privacy Policy pages created
- [ ] CI/CD pipeline running (build + 205 tests gate deploy)
- [ ] Azure resources provisioned (App Service, PostgreSQL, Container)
- [ ] DNS configured (if custom domain)
- [ ] HTTPS enforced
- [ ] Application Insights connected
- [ ] Polygon API upgraded to redistribution-compatible tier
- [ ] Smoke test: browse all pages, trigger scan, trigger sync, generate brief
