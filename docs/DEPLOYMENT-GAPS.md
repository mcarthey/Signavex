# Signavex Deployment Gaps — Localhost to Production

A practical roadmap for publishing Signavex as a subscription product with a free acquisition layer and gated Pro tier.

---

## 1. Current State

| Component | Current | Notes |
|-----------|---------|-------|
| Framework | .NET 8.0, Blazor Server | Interactive server-side rendering |
| Web | `Signavex.Web` — Blazor Server app | Serves all pages |
| Worker | `Signavex.Worker` — Windows Service | Runs scans, syncs data, generates briefs |
| Database | SQLite (`signavex.db`) | Shared file between Web + Worker |
| Auth | ASP.NET Identity (cookie auth) | Binary logged-in/not gating — no roles, claims, or policies |
| Billing | None | No payment processor, no subscription tiers |
| Email | None | No transactional email infrastructure |
| Hosting | `localhost` | Single-machine only |
| Data Providers | Polygon.io, Alpha Vantage, FRED, Anthropic | API keys in `appsettings.json` |
| Tests | 205 automated tests (xUnit) | Domain, Infrastructure, Engine, Signals |
| Settings | Global (`appsettings.json`) | Read-only Settings page; no per-user configuration |

---

## 2. Database: SQLite → SQL Server

SQLite works well for single-user local use but has concurrency limitations that will cause issues under multi-user load. Blazor Server's stateful circuits mean many concurrent DB readers/writers.

### What to change

1. **NuGet**: Replace `Microsoft.EntityFrameworkCore.Sqlite` with `Microsoft.EntityFrameworkCore.SqlServer` in `Signavex.Infrastructure.csproj`

2. **DbContext registration** in `ServiceCollectionExtensions.cs`:
   ```csharp
   // Before
   options.UseSqlite($"Data Source={dbPath}");
   // After
   options.UseSqlServer(connectionString);
   ```

3. **Remove PRAGMA statements** from both `Program.cs` files (Web + Worker):
   ```csharp
   // Remove these two lines:
   await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL");
   await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000");
   ```

4. **Regenerate all migrations** for SQL Server (SQLite migrations are not compatible)

5. **DateOnly handling**: SQL Server maps `DateOnly` to `date` columns natively in EF Core 8+. Verify `EconomicObservationEntity.Date` and `DailyBriefEntity.Date` column mappings.

### Local development

Keep SQLite for local development. Use a config-driven provider switch:

```csharp
var provider = builder.Configuration.GetValue<string>("Signavex:DatabaseProvider") ?? "Sqlite";
if (provider == "SqlServer")
    options.UseSqlServer(connectionString);
else
    options.UseSqlite($"Data Source={dbPath}");
```

This lets local development continue unchanged while production uses SQL Server.

### Estimated effort
2-3 hours. The EF Core abstraction handles most of it; the main risk is migration regeneration.

---

## 3. Hosting

### Platform: SmarterASP.NET

SmarterASP provides .NET hosting with MSSQL databases included. No Azure provisioning needed.

### Architecture: Single-process deployment

Merge the Worker's background services into the Web app. The Worker's `BackgroundService` classes (`DailyScanBackgroundService`, `EconomicDataSyncService`, `DailyBriefBackgroundService`, `ScanCommandPollingService`) register directly in `Signavex.Web/Program.cs` alongside the web host.

**Why this works:**
- All services already use `BackgroundService` (which implements `IHostedService`)
- The Web app's generic host runs them automatically
- No need for a separate Windows Service or paid background worker on SmarterASP
- Single deployment artifact, single database connection string

**What to change:**
1. Register the Worker's background services in `Signavex.Web/Program.cs`
2. Move the Worker's scan orchestration DI registrations into the Web app
3. Ensure the Web app's `appsettings.json` has all the config sections the Worker needs (data providers, Anthropic, etc.)
4. The separate `Signavex.Worker` project remains for local development (Windows Service) but isn't deployed to production

### Blazor Server considerations

- **Stateful circuits**: Each user holds a SignalR connection. Monitor circuit count.
- **Sticky sessions**: Required if scaling to multiple instances.
- **WebSocket support**: Must be enabled in hosting configuration.

### Timezone: Use UTC everywhere

The Worker currently uses `TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")` for scheduling. Replace with UTC-based scheduling throughout:

```csharp
// Before
var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, eastern);

// After
var now = DateTime.UtcNow;
// Schedule scans at 22:00 UTC (5:00 PM ET) / economic sync at 21:30 UTC (4:30 PM ET)
```

Display times are adjusted to the user's local timezone in the UI (future: user profile setting after auth is established).

**Files to change:**
- `DailyScanBackgroundService.cs`
- `EconomicDataSyncService.cs`
- `ScanCommandPollingService.cs`

---

## 4. Authentication & Authorization

### Current state

ASP.NET Identity with cookie auth is implemented. Login and register pages work. Five pages are gated with `[Authorize]`, nine pages are public. The gating is binary — logged in or not. No roles, claims, or custom authorization policies exist.

**Implemented:**
- `ApplicationUser : IdentityUser` (no custom fields)
- Cookie auth with 30-day sliding expiration
- Login (`/account/login`), Register (`/account/register`), Logout (POST endpoint)
- `AuthorizeView` in NavMenu toggles Sign In / Sign Out

**Gated routes:** `/` (Dashboard), `/history`, `/backtest`, `/settings`, `/candidate/{Ticker}`

**Public routes:** `/economy`, `/insights`, `/about`, `/privacy`, `/terms`, `/account/login`, `/account/register`

### Upgrade: Plan-based authorization

For subscription tiers, the binary `[Authorize]` must be replaced with plan-aware gating.

**Option A — Role-based (simpler):**
Assign users to "Free" or "Pro" roles. The Identity schema already has `AspNetRoles` and `AspNetUserRoles` tables.
```csharp
[Authorize(Roles = "Pro")]  // Only Pro subscribers
```
Stripe webhook updates the user's role on subscription change.

**Option B — Claims-based (more flexible):**
Add a `SubscriptionPlan` claim to the user. Register a custom policy.
```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ProRequired", policy =>
        policy.RequireClaim("SubscriptionPlan", "Pro"));
```
More flexible if tiers expand later, but more wiring.

**Recommendation:** Start with Role-based. Two roles (`Free`, `Pro`) cover the two-tier model. Migration to claims-based is straightforward later if needed.

### Route gating by tier

| Route | Free | Pro | Public |
|-------|------|-----|--------|
| `/economy` | - | - | Yes — FRED data is public domain, drives organic traffic |
| `/insights` | - | - | Yes — AI briefs as acquisition hook |
| `/about`, `/privacy`, `/terms` | - | - | Yes |
| `/` (Dashboard) | Top 5 candidates only | Full candidate list | No |
| `/candidate/{Ticker}` | Only for surfaced candidates | All candidates | No |
| `/history` | No | Yes | No |
| `/backtest` | No | Yes | No |
| `/settings` | No | Yes (per-user weights) | No |

### Important

- Do NOT weaken auth to fix 401s. Blazor Server auth uses the circuit's `AuthenticationState`.
- Test anonymous, Free, and Pro flows for every page.
- The Worker process is not user-facing — it runs scans at the site level regardless of user tiers. Users consume cached results; they do not trigger individual scans.

---

## 5. Subscription & Billing Infrastructure

This is the critical path item for a subscription launch. Nothing in the codebase touches payment processing.

### What to build

1. **`SubscriptionPlan` on `ApplicationUser`** — Add a plan field (Free/Pro) and a `StripeCustomerId` field. New registrations default to Free.

2. **Stripe Checkout** — When a Free user upgrades, redirect to a Stripe Checkout session. On success, Stripe redirects back and the webhook confirms the subscription.

3. **Stripe Webhooks** — Handle subscription lifecycle events:
   - `checkout.session.completed` → assign Pro role
   - `customer.subscription.updated` → handle plan changes
   - `customer.subscription.deleted` → downgrade to Free role
   - `invoice.payment_failed` → flag past-due, send notification

4. **Stripe Customer Portal** — Let users manage billing, update payment method, cancel subscription, and view invoices themselves. This eliminates most billing support overhead.

5. **Feature gating in Blazor** — Use `AuthorizeView` with roles to conditionally render components:
   ```razor
   <AuthorizeView Roles="Pro">
       <Authorized><!-- Full candidate list --></Authorized>
   </AuthorizeView>
   <AuthorizeView Roles="Free">
       <Authorized><!-- Top 5 only with upgrade prompt --></Authorized>
   </AuthorizeView>
   ```

### Tier structure

**Free (acquisition layer):**
- Top 5 surfaced candidates with scores and signal breakdown
- Market context bar (Tier 1 signals)
- No AI brief, no history, no backtest, no signal customization

**Pro ($X/month):**
- Full candidate list with all signals
- AI daily brief
- Economic health dashboard correlations
- Scan history and candidate history
- Signal weight customization (within available data — see below)
- Backtesting
- CSV export

### Per-user settings: bounded by site-level data

The Worker collects data at the site level — daily scans run against the full universe regardless of individual user preferences. Per-user customization operates within the boundaries of what the Worker has already collected:

- **Signal weight customization** — Users can adjust how signals are weighted in their personal scoring view. The underlying signal data is the same for everyone; only the weighting and resulting sort order change.
- **Watchlists / stock focus** — Users can flag specific tickers for attention, but this filters the existing dataset rather than triggering new scans.
- **Time frame views** — Users can view different historical windows, but only within the data the Worker has already synced.

If demand reveals gaps (e.g., users consistently want a sector the universe doesn't cover), data expansion happens at the site level — new tickers added to the universe config, not per-user scan jobs. This keeps API costs predictable and the Worker architecture simple.

### Per-user settings: implementation

The current Settings page is read-only and global (`appsettings.json`). For Pro users, per-user weights require:

1. **New entity:** `UserSignalWeights` table linked to `ApplicationUser` — stores per-user overrides for the 15 signal weights
2. **Service layer:** A service that merges user overrides with site-wide defaults — if a user hasn't customized a weight, they get the default
3. **Settings page upgrade:** Make the Settings page editable for Pro users with save/reset-to-defaults functionality
4. **Scoring adjustment:** The candidate scoring pipeline reads user-specific weights when rendering for an authenticated Pro user, site defaults otherwise

---

## 6. Transactional Email

No email infrastructure exists. Identity is configured with `RequireConfirmedAccount = false` because there is no email sender. Password reset does not work.

### What to build

1. **Email provider integration** — SendGrid (free tier: 100 emails/day) or Resend (free tier: 3,000 emails/month). Implement `IEmailSender<ApplicationUser>` for ASP.NET Identity integration.

2. **Email templates** — Branded HTML templates for:
   - **Welcome** — sent on registration, introduces the product
   - **Email verification** — confirm email address (enable `RequireConfirmedAccount = true`)
   - **Password reset** — secure token-based reset link
   - **Subscription confirmed** — Pro upgrade acknowledgment with billing details
   - **Subscription cancelled** — confirms cancellation, notes when access expires
   - **Payment failed** — alerts user to update payment method, includes portal link
   - **Weekly brief digest** (post-launch) — summary of the week's AI briefs for Pro users

3. **Template approach** — Use a simple Razor-based email template engine or pre-built HTML templates. Keep templates in a shared location (`Signavex.Infrastructure/Email/Templates/`).

4. **Configuration:**
   ```
   Email__Provider = SendGrid
   Email__ApiKey = your-key-here
   Email__FromAddress = noreply@signavex.com
   Email__FromName = Signavex
   ```

### Identity integration

Once `IEmailSender<ApplicationUser>` is registered:
- Enable `RequireConfirmedAccount = true`
- Identity automatically sends verification and password reset emails
- Custom emails (subscription events) are sent from the Stripe webhook handler

---

## 7. API Keys & Secrets Management

### Current state

API keys live in `appsettings.json` (Worker's is gitignored, Web's has empty placeholders).

### Production approach

SmarterASP supports environment variables and app settings through their control panel.

| Secret | Source |
|--------|--------|
| Polygon API key | SmarterASP environment variable |
| Alpha Vantage API key | SmarterASP environment variable |
| FRED API key | SmarterASP environment variable |
| Anthropic API key | SmarterASP environment variable |
| Stripe API key (secret) | SmarterASP environment variable |
| Stripe webhook secret | SmarterASP environment variable |
| Email provider API key | SmarterASP environment variable |
| SQL Server connection string | SmarterASP connection strings panel |

ASP.NET Core reads environment variables automatically via `IConfiguration` using the `__` (double underscore) separator for nested keys:

```
# Database (required for production)
Signavex__DatabaseProvider = SqlServer
Signavex__ConnectionString = Server=your-server;Database=Signavex;User Id=xxx;Password=xxx;

# App settings
Signavex__DataDirectory = ./data
Signavex__RunBackgroundServices = true

# Data provider API keys
DataProviders__Polygon__ApiKey = your-key-here
DataProviders__AlphaVantage__ApiKey = your-key-here
DataProviders__Fred__ApiKey = your-key-here
Anthropic__ApiKey = your-key-here

# Future (Phase 2+)
Stripe__SecretKey = sk_live_xxx
Stripe__WebhookSecret = whsec_xxx
Email__ApiKey = your-key-here
```

### Gitignore coverage

| File | Tracked | Contains secrets |
|------|---------|-----------------|
| `Web/appsettings.json` | Yes | No — empty placeholders |
| `Web/appsettings.Development.json` | No (gitignored) | Yes — local dev keys |
| `Worker/appsettings.json` | No (gitignored) | Yes — local dev keys |
| `Worker/appsettings.Development.json` | No (gitignored) | Yes — local dev keys |

---

## 8. Data Licensing

| Provider | License | Redistribution |
|----------|---------|---------------|
| FRED (Federal Reserve) | Public domain | Free to redistribute. Attribution appreciated but not required. |
| Polygon.io | Commercial | Free tier: personal use only. Starter ($29/mo) or higher needed for public redistribution. Auth gating satisfies personal-use restriction for now. |
| Alpha Vantage | Commercial | Free tier: 25 req/day, personal use. Premium required for redistribution. |
| Anthropic (Claude API) | Commercial | Generated content can be published. Standard API pricing applies. |

### Action required

- Auth gating on scanner pages satisfies Polygon free-tier personal-use requirement
- If the site grows, upgrade Polygon to Starter tier ($29/mo) for redistribution rights
- Verify Alpha Vantage redistribution terms for your usage tier
- Add data attribution where required

---

## 9. Legal Requirements

### Must-have before launch

1. **"Not Financial Advice" disclaimer** — appears on every page with financial data. Already present in AI-generated briefs footer.

2. **Terms of Service** — cover:
   - Data is informational only, not investment advice
   - No guarantee of accuracy or timeliness
   - User assumes all risk for decisions based on the data
   - Data sourced from third parties (FRED, Polygon, etc.)
   - Subscription billing terms, auto-renewal disclosures, and cancellation policy
   - Refund policy (required by Stripe and most US states for auto-renewing subscriptions)
   - Service availability — no SLA guarantees at this stage

3. **Privacy Policy** — cover:
   - What data is collected (email, usage data, payment info via Stripe)
   - How data is stored and protected
   - Cookie usage (Blazor Server uses cookies for circuit affinity + auth)
   - Third-party data processors (Stripe for billing, SendGrid/Resend for email)
   - GDPR/CCPA compliance if applicable

4. **Data source attribution** — visible credits for FRED, market data providers

### Marketing language

All copy — landing page, sign-up flow, emails, social media — must consistently frame Signavex as a **discovery and signal analysis tool, not investment advice**. Avoid language like "find winning stocks" or "beat the market." Use framing like "surface candidates," "signal-based screening," and "data-driven discovery."

---

## 10. CI/CD Pipeline

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

  deploy:
    needs: build-and-test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet publish src/Signavex.Web -c Release -o ./publish
      # Deploy to SmarterASP via FTP or Web Deploy
      - name: Deploy to SmarterASP
        uses: SamKirkland/FTP-Deploy-Action@v4.3.5
        with:
          server: ${{ secrets.SMARTERASP_FTP_HOST }}
          username: ${{ secrets.SMARTERASP_FTP_USER }}
          password: ${{ secrets.SMARTERASP_FTP_PASS }}
          local-dir: ./publish/
```

### Key points

- **205 tests gate deployment** — if tests fail, nothing deploys
- Build in Release mode for production
- Single deployment artifact (Web app includes background services)
- Secrets stored in GitHub repository secrets
- SmarterASP supports FTP and Web Deploy for publishing

---

## 11. Monitoring & Observability

### Approach

SmarterASP provides basic monitoring through their control panel. For deeper observability, add structured logging to a free tier service.

### Options

1. **Seq** (free single-user) — structured log viewer, works great with Serilog
2. **Application Insights** (free tier: 5 GB/mo) — if you want Azure-level monitoring without Azure hosting
3. **Built-in logging** — the codebase already uses `ILogger<T>` throughout; logs are visible in SmarterASP's log viewer

### Key metrics to watch

| Metric | Why |
|--------|-----|
| Scan duration | Worker health — typical: hours for full universe |
| API rate limit hits | Data provider throttling |
| Brief generation failures | AI service health |
| Error rate | Application health |
| Circuit count | Blazor Server resource pressure |
| Subscription conversions | Free → Pro conversion rate |
| Churn rate | Pro → cancelled, indicates product-market fit |
| Webhook failures | Stripe event processing health |

---

## 12. Subscription Economics & Cost Model

### API call volume per daily scan (code-derived, March 2026)

The scan universe is configured as `["SP500", "SP400"]` = 903 tickers. Each weekday scan generates:

| Provider | Endpoint | Calls per scan | Monthly (22 workdays) |
|----------|----------|---------------|----------------------|
| Polygon | OHLCV (`/v2/aggs/ticker/{t}/range/1/day/...`) | 904 (903 + SPY) | 19,888 |
| Polygon | News (`/v2/reference/news?ticker={t}`) | 903 | 19,866 |
| **Polygon total** | | **1,807** | **39,754** |
| Alpha Vantage | OVERVIEW (`/query?function=OVERVIEW`) | 903 | 19,866 |
| Alpha Vantage | EARNINGS (`/query?function=EARNINGS`) | 903 | 19,866 |
| **Alpha Vantage total** | | **1,806** | **39,732** |
| Anthropic | Claude Sonnet 4 brief generation | 1 | 22 |
| FRED | Series observations (nightly sync, 12 series) | 12 | 264 |
| FRED | Macro indicators (during scan) | 2 | 44 |
| **Grand total** | | **3,628** | **79,816** |

**Scan duration at current rate limits:**
- Polygon (5 req/min free tier): ~6 hours
- Alpha Vantage (5 req/min free tier, 25/day limit): **only ~12 tickers/day get fundamentals data**

### Free tier viability assessment

| Provider | Free tier limit | Daily need | Viable? |
|----------|----------------|-----------|---------|
| Polygon | 5 req/min (no hard daily cap) | 1,807 calls | Yes — slow (6h scan) but completes. Auth gate satisfies personal-use restriction. |
| Alpha Vantage | 25 calls/day, 5 req/min | 1,806 calls | **No** — only ~12 tickers get fundamentals. Provider returns null on failure, so scan completes but signal quality degrades for ~99% of tickers. |
| Anthropic | Pay-per-token | 1 call (~4K tokens) | Yes — token cost is negligible |
| FRED | Unlimited (public domain) | 14 calls | Yes |

### Anthropic token cost calculation

Based on `AnthropicBriefGenerator.cs` (model: `claude-sonnet-4-20250514`, max output: 4,096 tokens):

| Metric | Per brief | Monthly (22 workdays) |
|--------|----------|----------------------|
| Input tokens | ~2,500 | ~55,000 |
| Output tokens | ~1,800 | ~39,600 |
| Input cost ($3.00/M tokens) | $0.0075 | $0.165 |
| Output cost ($15.00/M tokens) | $0.027 | $0.594 |
| **Total** | **$0.035** | **$0.76** |

### Infrastructure costs (required for product to function)

| Resource | Cost | Notes |
|----------|------|-------|
| SmarterASP hosting (Basic plan) | ~$5-10/mo | .NET 8.0, MSSQL included |
| Anthropic API (Claude Sonnet 4) | ~$0.76/mo | 22 briefs/month, shared across all users |
| FRED | $0 | Public domain, unlimited |
| Email provider (SendGrid free tier) | $0 | Up to 100 emails/day |
| **Minimum baseline** | **~$6-11/mo** | Polygon free tier + degraded AV fundamentals |

### Data provider upgrade costs

| Resource | Cost | What it unlocks |
|----------|------|----------------|
| Alpha Vantage Premium | $49.99/mo | Full fundamentals (PE, EPS, analyst ratings) for all 903 tickers. **Required for production-quality signal scoring.** |
| Polygon Starter | $29/mo | Redistribution rights, faster rate limits. Free tier works behind auth gate but scans take 6 hours. |
| Email provider (paid tier) | ~$15-20/mo | When exceeding 100 emails/day |
| Custom domain + SSL | ~$10-15/yr | Professional appearance |

### Realistic cost scenarios

**Scenario A — Launch with degraded fundamentals (minimize cost):**
Free Polygon + free AV + Anthropic = ~$6-11/mo. Fundamentals signals (PE ratio, EPS, analyst ratings) will be null for ~99% of tickers. Volume, momentum, and market-context signals still work. Viable for validating demand but not production quality.

**Scenario B — Launch with full signal quality (recommended):**
Free Polygon + AV Premium + Anthropic = ~$56-61/mo. All 15 signals fire for all tickers. Scans take ~6 hours (Polygon rate limit).

**Scenario C — Full production with redistribution rights:**
Polygon Starter + AV Premium + Anthropic = ~$85-90/mo. Faster scans, redistribution-safe.

### Why per-subscriber marginal cost is near zero

The Worker runs daily scans against the full universe and stores results in the database. Users query cached scan results — they do not trigger live API calls. This means:

- **Polygon cost** is fixed by universe size (903 tickers), not subscriber count
- **Alpha Vantage cost** is fixed by universe size, not subscriber count
- **Anthropic cost** is fixed at one brief per day (shared), not per user
- **FRED sync** is fixed at the configured series count (12 series)

The only costs that scale with subscribers are email volume and database storage, both of which are negligible at early scale.

### Pricing implications

At $9-15/mo per Pro subscriber:

| Scenario | Monthly cost | Break-even subscribers | Net at 50 subs | Net at 100 subs |
|----------|-------------|----------------------|----------------|-----------------|
| A (degraded) | ~$11 | 1-2 | $439-739 | $889-1,489 |
| B (full signals) | ~$61 | 5-7 | $389-689 | $839-1,439 |
| C (production) | ~$90 | 6-10 | $360-660 | $810-1,410 |

### Cost optimization opportunity: persistent fundamentals cache

Alpha Vantage fundamentals (PE ratio, EPS, analyst ratings) change quarterly at most. The current `CachedFundamentalsProvider` uses in-memory cache with 24-hour sliding expiration — cleared on process restart. A persistent cache (database-backed) with weekly refresh would:
- Reduce AV calls from ~39,732/month to ~7,224/month (5× reduction)
- Survive process restarts and deployments
- Make the AV premium tier more cost-effective
- Not save money directly (AV Premium is flat-rate) but improve scan resilience and speed

This optimization is logged as a post-Phase-1 item — not a launch blocker.

### Comparison to Azure

SmarterASP is significantly cheaper than Azure for this use case (~$6-11/mo vs ~$30-35/mo) because hosting and MSSQL are bundled. The tradeoff is less control over infrastructure and scaling.

---

## 13. Priority Roadmap

### Phase 0: Cost modeling (before building billing)

| Item | Effort | Notes |
|------|--------|-------|
| Measure actual Polygon API costs from dashboard | 30 min | Determines pricing floor |
| Measure actual Anthropic API costs from dashboard | 30 min | |
| Set Pro tier price based on real cost data | Decision | Informs Stripe product configuration |

### Phase 1: Must-have (before launch)

| Item | Effort | Risk |
|------|--------|------|
| SQL Server migration | 2-3 hours | Low — EF Core handles it |
| Merge Worker into Web app | 2-3 hours | Low — services are already BackgroundService |
| UTC timezone conversion | 30 min | Low |
| API key extraction to env vars | 1-2 hours | Low |
| "Not financial advice" disclaimer on all pages | 1 hour | Low |
| Update Terms of Service (billing terms, refund policy) | 2-3 hours | Low |
| Update Privacy Policy (Stripe, email provider) | 1-2 hours | Low |
| SmarterASP provisioning + publish | 1-2 hours | Low |

### Phase 2: Subscription infrastructure

| Item | Effort | Risk |
|------|--------|------|
| Add `SubscriptionPlan` + `StripeCustomerId` to `ApplicationUser` | 1-2 hours | Low |
| Seed Free and Pro roles | 30 min | Low |
| Stripe Checkout integration (create subscription flow) | 3-4 hours | Medium |
| Stripe webhook handler (subscription lifecycle events) | 3-4 hours | Medium — must handle all edge cases |
| Stripe Customer Portal integration | 1-2 hours | Low |
| Plan-based route gating (replace binary `[Authorize]`) | 2-3 hours | Low |
| Free tier candidate limiting (top 5 only) | 2-3 hours | Medium — UI changes in Dashboard + CandidateDetail |
| Upgrade prompts on gated features | 1-2 hours | Low |

### Phase 3: Transactional email

| Item | Effort | Risk |
|------|--------|------|
| Email provider integration (SendGrid or Resend) | 2-3 hours | Low |
| Implement `IEmailSender<ApplicationUser>` | 1-2 hours | Low |
| Email templates (welcome, verify, password reset) | 3-4 hours | Low |
| Email templates (subscription confirmed, cancelled, payment failed) | 2-3 hours | Low |
| Enable `RequireConfirmedAccount = true` | 30 min | Low — depends on email working |

### Phase 4: Launch extras

| Item | Effort | Risk |
|------|--------|------|
| Marketing landing page (static, separate from app) | 4-8 hours | Low |
| CI/CD pipeline (GitHub Actions + FTP deploy) | 2-3 hours | Low |
| Custom error pages (404, 500) | 1-2 hours | Low |
| Health check endpoints | 1 hour | Low |
| Rate limiting on public pages | 1-2 hours | Low |

### Post-launch

| Item | Effort | Risk |
|------|--------|------|
| Per-user signal weight customization (Pro) | 6-8 hours | Medium — new entity, merge service, UI |
| User profiles with timezone preference | 4-6 hours | Low |
| Weekly brief digest email (Pro) | 4-6 hours | Low |
| User watchlists | 8-12 hours | Medium |
| CSV export (Pro) | 2-4 hours | Low |
| API endpoint for programmatic access | 8-12 hours | Medium |
| Polygon licensing upgrade | 0 (just pay) | Low |

---

## 14. Deployment Checklist

### Before first deploy (Phase 1)

- [ ] SQL Server migration created and tested locally
- [ ] Config-driven database provider switch working (SQLite local, SQL Server production)
- [ ] All PRAGMA statements removed (production path)
- [ ] Worker background services registered in Web app's `Program.cs`
- [ ] Timezone scheduling converted to UTC
- [ ] API keys moved to environment variables
- [ ] "Not financial advice" disclaimer on all data pages
- [ ] Terms of Service updated with billing and refund terms
- [ ] Privacy Policy updated with Stripe and email provider disclosures
- [ ] SmarterASP site provisioned and MSSQL database created
- [ ] DNS configured (if custom domain)
- [ ] HTTPS enforced
- [ ] Smoke test: browse all pages, trigger scan, trigger sync, generate brief

### Before subscription launch (Phase 2 + 3)

- [ ] `SubscriptionPlan` and `StripeCustomerId` fields on `ApplicationUser`
- [ ] Free and Pro roles seeded in `AspNetRoles`
- [ ] New user registration assigns Free role by default
- [ ] Stripe product and price configured in Stripe Dashboard
- [ ] Stripe Checkout flow working (Free → Pro upgrade)
- [ ] Stripe webhook endpoint registered and verified
- [ ] Webhook handles: `checkout.session.completed`, `customer.subscription.updated`, `customer.subscription.deleted`, `invoice.payment_failed`
- [ ] Stripe Customer Portal accessible from user account
- [ ] Plan-based route gating tested (anonymous, Free, Pro flows)
- [ ] Free tier shows top 5 candidates only on Dashboard
- [ ] Free tier can view candidate detail for surfaced candidates only
- [ ] Gated pages (history, backtest, settings) show upgrade prompt for Free users
- [ ] Email provider configured and sending
- [ ] Welcome, verification, and password reset emails working
- [ ] Subscription lifecycle emails working (confirmed, cancelled, payment failed)
- [ ] `RequireConfirmedAccount` enabled
- [ ] Marketing language reviewed — no "find winning stocks" or investment advice framing
- [ ] End-to-end test: register → verify email → Free experience → upgrade to Pro → full access → cancel → downgraded to Free
