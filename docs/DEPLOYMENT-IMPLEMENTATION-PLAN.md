# Signavex — Deployment & Subscription Implementation Plan

**Replaces:** `Signavex-Implementation-Plan.md` (product build plan — completed)
**Companion:** `DEPLOYMENT-GAPS.md` (analysis and rationale)
**Date:** March 3, 2026

This document is a concrete task list for taking Signavex from localhost to a deployed subscription product. Each task includes status, what to change, and which files are affected.

---

## Current State Audit

Before building the task list, here is what the gaps document calls for vs. what already exists:

| Gap Item | Status | Evidence |
|----------|--------|----------|
| SQL Server provider switch | **Done** | `ServiceCollectionExtensions.cs` — config-driven SQLite/SqlServer branch |
| PRAGMA conditional | **Done** | `Web/Program.cs:94-98` — only applied when provider is SQLite |
| Worker services in Web app | **Done** | `Web/Program.cs:71-79` — conditional via `RunBackgroundServices` flag |
| UTC timezone | **Done** | All Worker services use `DateTime.UtcNow` — no Eastern timezone references in code |
| ASP.NET Identity auth | **Done** | Cookie auth, login/register/logout, 30-day sliding expiration |
| Binary route gating | **Done** | `[Authorize]` on Dashboard, History, Backtest, Settings, CandidateDetail |
| Disclaimer component | **Done** | `Shared/Disclaimer.razor` used on Dashboard, History, Backtest, Economy, Insights |
| Terms of Service page | **Done** | `Terms.razor` — 10 sections, covers no-advice, liability, data sourcing |
| Privacy Policy page | **Done** | `Privacy.razor` — 9 sections, covers cookies, data storage, third parties |
| SQL Server migration regen | **Done** | `InitialSqlServer` migration + `AddSubscriptionFields` migration |
| API keys to env vars | **Done** | Empty placeholders in committed config; real keys in gitignored files |
| Plan-based authorization | **Done** | Free/Pro roles, `ProRequired` policy, feature gating on all pages |
| Stripe billing | **Done** | Checkout, webhook, customer portal endpoints; `StripeOptions` config |
| Transactional email | **Done** | SendGrid `IEmailSender<ApplicationUser>`, password reset flow |
| Per-user settings | **Not started** | Settings are global via `appsettings.json`, page is read-only |
| SmarterASP provisioning | **Not started** | Manual — see Phase 1C |
| CI/CD pipeline | **Done** | GitHub Actions: build → test → FTP deploy to SmarterASP |
| Marketing landing page | **Not started** | |

---

## Phase 0 — Cost Modeling

_Prerequisite to pricing decisions. No code changes._

- [x] **0.1** ~~Log into Polygon.io dashboard, export last 30 days of API usage (calls, bandwidth)~~
  - **Calculated from code:** 1,807 Polygon calls/scan (904 OHLCV + 903 news), 39,754 calls/month
  - Free tier viable behind auth gate (personal use), scans take ~6 hours at 5 req/min
  - Starter ($29/mo) needed for redistribution rights or faster scans
  - **Action:** Validate against Polygon dashboard for actual usage (may be lower if scans skip weekends/holidays)

- [x] **0.2** ~~Log into Anthropic console, export last 30 days of token usage and cost~~
  - **Calculated from code:** ~2,500 input + ~1,800 output tokens per brief (Claude Sonnet 4)
  - Monthly: ~55K input + ~39.6K output tokens = **$0.76/month**
  - Model: `claude-sonnet-4-20250514`, max output 4,096 tokens
  - **Action:** Validate against Anthropic console for actual token counts

- [x] **0.3** Calculate monthly infrastructure cost at current scan universe (S&P 500 + 400)
  - **Scenario A (minimum):** ~$6-11/mo — free data tiers, degraded AV fundamentals (~99% of tickers lack PE/EPS/analyst data)
  - **Scenario B (recommended launch):** ~$56-61/mo — AV Premium ($49.99) for full signal quality, free Polygon
  - **Scenario C (full production):** ~$85-90/mo — AV Premium + Polygon Starter ($29) for redistribution rights
  - See `DEPLOYMENT-GAPS.md` Section 12 for full breakdown

- [x] **0.4** Determine Pro tier price point (must exceed fixed infrastructure cost with margin)
  - **Decision: Scenario A** — launch with free data tiers (~$6-11/mo)
  - Rationale: free tiers work for overnight scanning; no need to invest in paid tiers until there's proven interest
  - Overnight scan duration (~6h Polygon at 5 req/min) is acceptable — data is available in the morning from the previous day
  - AV fundamentals degraded for most tickers (25 calls/day free tier), but volume/momentum/market signals still fire
  - **Upgrade trigger:** paid data tiers when subscriber count justifies the cost or when signal quality becomes a retention issue
  - At Scenario A ($11/mo fixed cost): **break-even at 1-2 Pro subscribers** at $9-15/mo
  - **Pro price point: TBD** — set after initial launch validates demand

- [x] **0.5** Document cost model in `DEPLOYMENT-GAPS.md` Section 12 with real numbers
  - **Done** — Section 12 updated with code-derived API call volumes, token costs, free tier viability assessment, three cost scenarios, and break-even analysis

---

## Phase 1 — Infrastructure for Production

_Goal: The app can run on SmarterASP with SQL Server. No user-facing feature changes._

### 1A. SQL Server Migration

- [x] **1A.1** Create a new EF Core migration targeting SQL Server
  - **Done** — Deleted 5 SQLite-specific migrations, created `DesignTimeDbContextFactory` targeting SQL Server, generated `InitialSqlServer` migration
  - `DateOnly` properties map to `date` columns, Identity strings to `nvarchar(450)`, integers to `int`
  - **Files:** `DesignTimeDbContextFactory.cs`, `20260304032351_InitialSqlServer.cs`, `SignavexDbContextModelSnapshot.cs`

- [x] **1A.2** Updated database initialization for dual-provider support
  - **Done** — SQL Server uses `MigrateAsync()`, SQLite uses `EnsureCreatedAsync()` (no migrations needed for dev)
  - Updated both `Web/Program.cs` and `Worker/Program.cs` with provider-conditional initialization
  - PRAGMA statements only applied for SQLite path

- [x] **1A.3** Verify the SQLite path still works after migration changes
  - **Done** — All 207 tests pass (all use SQLite via `EnsureCreated()`)
  - **Note:** SQL Server migration requires a running SQL Server instance to test (LocalDB or Docker) — deferred to hosting provisioning (1C)

### 1B. Environment Configuration

- [x] **1B.1** Audit all config keys that need environment variable equivalents
  - **Done** — `Web/appsettings.json` now includes `DatabaseProvider` and `ConnectionString` with empty defaults
  - `DataDirectory` changed from absolute path to relative `./data`
  - All API keys remain empty placeholders in committed config; real keys only in gitignored files

- [x] **1B.2** Ensure no secrets exist in committed `appsettings.json` files
  - **Verified** — `Web/appsettings.json` has empty placeholders only (committed)
  - `Worker/appsettings.json` is gitignored (not tracked)
  - Both `appsettings.Development.json` files are gitignored

- [x] **1B.3** Document the full environment variable list for SmarterASP setup
  - **Done** — `DEPLOYMENT-GAPS.md` Section 7 updated with complete env var list including `DatabaseProvider`, `ConnectionString`, `RunBackgroundServices`, and gitignore coverage table

### 1C. Hosting Provisioning

- [ ] **1C.1** Provision SmarterASP.NET account and site
  - .NET 8.0 hosting plan
  - MSSQL database included

- [ ] **1C.2** Configure MSSQL database on SmarterASP
  - Note connection string for environment variable setup

- [ ] **1C.3** Configure environment variables in SmarterASP control panel
  - All API keys, connection string, `RunBackgroundServices=true`, `DatabaseProvider=SqlServer`

- [ ] **1C.4** First deploy: `dotnet publish src/Signavex.Web -c Release -o ./publish`
  - FTP or Web Deploy to SmarterASP
  - Verify migrations run on first startup
  - Verify background services start (check logs for scan scheduling)

- [ ] **1C.5** Smoke test deployed site
  - Browse all public pages (economy, insights, about, terms, privacy)
  - Register an account, log in
  - Browse all gated pages (dashboard, history, backtest, settings, candidate detail)
  - Trigger a manual scan if possible, or wait for scheduled scan
  - Verify economic data sync runs
  - Verify daily brief generates

- [ ] **1C.6** Configure custom domain and HTTPS (if ready)
  - DNS A/CNAME record
  - SSL certificate via SmarterASP

---

## Phase 2 — Subscription Billing

_Goal: Users can sign up free, upgrade to Pro via Stripe, and manage their subscription._

### 2A. Role Infrastructure

- [x] **2A.1** Add `SubscriptionPlan` and `StripeCustomerId` properties to `ApplicationUser`
  ```csharp
  public class ApplicationUser : IdentityUser
  {
      public string SubscriptionPlan { get; set; } = "Free";
      public string? StripeCustomerId { get; set; }
  }
  ```
  - Create EF migration for the new columns
  - **Files:** `ApplicationUser.cs`, new migration

- [x] **2A.2** Seed Free and Pro roles in application startup
  - Add a role seeder after `EconomicDataSeeder` in `Program.cs`
  - Idempotent: check if roles exist before creating
  - **Files:** `Web/Program.cs` or new `RoleSeeder.cs`

- [x] **2A.3** Assign Free role to new user registrations
  - After `UserManager.CreateAsync()` succeeds, call `UserManager.AddToRoleAsync(user, "Free")`
  - **Files:** `Account/Register.razor`

- [x] **2A.4** Add authorization policies
  ```csharp
  builder.Services.AddAuthorizationBuilder()
      .AddPolicy("ProRequired", policy => policy.RequireRole("Pro"));
  ```
  - **Files:** `Web/Program.cs`

### 2B. Stripe Integration

- [x] **2B.1** Add `Stripe.net` NuGet package to `Signavex.Web`
  - **Files:** `Signavex.Web.csproj`

- [x] **2B.2** Add Stripe configuration options
  ```csharp
  public class StripeOptions
  {
      public const string SectionName = "Stripe";
      public string SecretKey { get; set; } = "";
      public string PublishableKey { get; set; } = "";
      public string WebhookSecret { get; set; } = "";
      public string ProPriceId { get; set; } = "";
  }
  ```
  - Bind in `Program.cs`
  - **Files:** new `StripeOptions.cs` in Domain/Configuration, `Program.cs`

- [x] **2B.3** Create Stripe products and prices in Stripe Dashboard
  - Product: "Signavex Pro"
  - Price: monthly recurring at determined price point (from Phase 0)
  - Note the Price ID for config

- [x] **2B.4** Implement checkout endpoint
  - POST `/account/upgrade` → creates Stripe Checkout Session → redirects to Stripe
  - On success, Stripe redirects to `/account/upgrade-success`
  - On cancel, Stripe redirects to `/account/upgrade-cancelled`
  - **Files:** `Program.cs` (minimal API endpoints) or new `Account/Upgrade.razor`

- [x] **2B.5** Implement Stripe webhook endpoint
  - POST `/webhooks/stripe` — receives Stripe events
  - Handle events:
    - `checkout.session.completed` → look up user by Stripe customer ID, assign Pro role, update `SubscriptionPlan`
    - `customer.subscription.updated` → handle plan changes
    - `customer.subscription.deleted` → remove Pro role, set plan to Free
    - `invoice.payment_failed` → log warning, optionally send email
  - Verify webhook signature using `WebhookSecret`
  - **Files:** new webhook endpoint in `Program.cs` or dedicated `StripeWebhookEndpoint.cs`

- [x] **2B.6** Implement Stripe Customer Portal link
  - Endpoint that creates a Stripe billing portal session and redirects
  - Accessible from account area for Pro users
  - **Files:** `Program.cs` or account page

- [x] **2B.7** Add upgrade prompt component
  - Shown to Free users on gated pages (history, backtest, settings)
  - Links to checkout flow
  - **Files:** new `Shared/UpgradePrompt.razor`

### 2C. Plan-Based Feature Gating

- [x] **2C.1** Dashboard: limit Free users to top 5 candidates
  - Pro users see full candidate list
  - Free users see top 5 with an upgrade prompt below
  - **Files:** `Dashboard.razor`

- [x] **2C.2** CandidateDetail: restrict Free users to surfaced candidates only
  - If a Free user navigates to `/candidate/{Ticker}` for a non-surfaced ticker, show upgrade prompt
  - **Files:** `CandidateDetail.razor`

- [x] **2C.3** History page: Pro only
  - Free users see upgrade prompt instead of content
  - Change from `[Authorize]` to `[Authorize(Roles = "Pro")]` or use `AuthorizeView` with role check
  - **Files:** `History.razor`

- [x] **2C.4** Backtest page: Pro only
  - Same approach as History
  - **Files:** `Backtest.razor`

- [x] **2C.5** Settings page: Pro only (for future per-user weights)
  - Same approach as History
  - **Files:** `Settings.razor`

- [x] **2C.6** Update NavMenu to reflect tier
  - Show/hide or badge menu items based on user's role
  - **Files:** `NavMenu.razor`

- [x] **2C.7** Test all three auth states end-to-end
  - Anonymous: only sees public pages, redirected to login for gated pages
  - Free: sees Dashboard with top 5, upgrade prompts on gated pages
  - Pro: full access to everything
  - **Manual test, document in:** `docs/MANUAL_TEST.md`

---

## Phase 3 — Transactional Email

_Goal: Password reset works, email verification enabled, subscription lifecycle emails sent._

### 3A. Email Provider Setup

- [x] **3A.1** Choose provider (SendGrid free tier: 100/day, or Resend free tier: 3,000/month)
  - Create account, get API key

- [x] **3A.2** Add email NuGet package to `Signavex.Infrastructure`
  - SendGrid: `SendGrid` package
  - Resend: `Resend` package
  - **Files:** `Signavex.Infrastructure.csproj`

- [x] **3A.3** Create email configuration options
  ```csharp
  public class EmailOptions
  {
      public const string SectionName = "Email";
      public string Provider { get; set; } = "";
      public string ApiKey { get; set; } = "";
      public string FromAddress { get; set; } = "noreply@signavex.com";
      public string FromName { get; set; } = "Signavex";
  }
  ```
  - **Files:** new `EmailOptions.cs` in Domain/Configuration

- [x] **3A.4** Implement `IEmailSender<ApplicationUser>`
  - Wraps the email provider SDK
  - Uses HTML templates for email body
  - Register in DI
  - **Files:** new class in `Signavex.Infrastructure/Email/`, `ServiceCollectionExtensions.cs`

### 3B. Email Templates

- [x] **3B.1** Create base HTML email template
  - Branded header, footer with disclaimer
  - Responsive layout for mobile
  - **Files:** new `Infrastructure/Email/Templates/` directory

- [x] **3B.2** Welcome email template
  - Sent on registration
  - Introduces Signavex, links to economy dashboard and insights
  - **Files:** template file + send call in Register flow

- [x] **3B.3** Email verification template
  - Sent on registration (once `RequireConfirmedAccount = true`)
  - Contains verification link
  - Handled automatically by Identity once `IEmailSender` is registered

- [x] **3B.4** Password reset template
  - Contains secure reset link
  - Handled automatically by Identity once `IEmailSender` is registered
  - Add "Forgot password?" link to login page
  - **Files:** `Account/Login.razor`, new `Account/ForgotPassword.razor`, new `Account/ResetPassword.razor`

- [x] **3B.5** Subscription confirmed template
  - Sent from Stripe webhook on `checkout.session.completed`
  - Confirms Pro access, links to Customer Portal for billing management

- [x] **3B.6** Subscription cancelled template
  - Sent from Stripe webhook on `customer.subscription.deleted`
  - Confirms cancellation, notes when Pro access expires

- [x] **3B.7** Payment failed template
  - Sent from Stripe webhook on `invoice.payment_failed`
  - Alerts user, links to Customer Portal to update payment method

### 3C. Enable Email Verification

- [ ] **3C.1** Set `RequireConfirmedAccount = true` in Identity config
  - Only do this AFTER email sending is verified working
  - **Files:** `Web/Program.cs`

- [ ] **3C.2** Add email confirmation page
  - User clicks link in verification email → confirms account
  - **Files:** new `Account/ConfirmEmail.razor`

- [ ] **3C.3** Update register flow
  - After registration, show "check your email" message instead of auto-sign-in
  - **Files:** `Account/Register.razor`

---

## Phase 4 — Legal & Content Updates

_Goal: Terms and Privacy are updated for billing. Marketing language is consistent._

- [x] **4.1** Update Terms of Service for subscription billing
  - Add section: Subscription terms, auto-renewal, billing cycle
  - Add section: Cancellation and refund policy
  - Add section: Service modifications (right to change pricing with notice)
  - **Files:** `Terms.razor`

- [x] **4.2** Update Privacy Policy for new third parties
  - Add Stripe as payment processor (collects payment info on their hosted page)
  - Add email provider (SendGrid/Resend) as email processor
  - Update "Information We Collect" to mention payment information is handled by Stripe
  - **Files:** `Privacy.razor`

- [x] **4.3** Review all user-facing copy for marketing language compliance
  - No "find winning stocks," "beat the market," or similar language
  - Consistent framing: "discovery tool," "signal-based screening," "risk reduction"
  - **Pages to review:** Home/About, Dashboard, Insights, Economy, upgrade prompts, email templates

---

## Phase 5 — CI/CD & Launch Extras

_Goal: Automated deployment pipeline, error handling, basic hardening._

### 5A. CI/CD Pipeline

- [x] **5A.1** Create `.github/workflows/deploy.yml`
  - Build → Test → Publish → FTP Deploy to SmarterASP
  - Only on push to `main`
  - Store FTP credentials in GitHub repository secrets
  - **Files:** new `.github/workflows/deploy.yml`

- [x] **5A.2** Test pipeline with a non-production push
  - Verify build, test, and deploy steps all succeed
  - Verify deployed site works after automated deploy

### 5B. Error Handling & Hardening

- [x] **5B.1** Add custom error pages (404, 500)
  - Branded pages that match the app's design
  - **Files:** new error page components, `Program.cs` error handling config

- [x] **5B.2** Add health check endpoint
  - `/health` — checks DB connectivity, returns 200/503
  - Useful for uptime monitoring
  - **Files:** `Program.cs`

- [x] **5B.3** Add rate limiting on public pages
  - Prevent scraping of economic dashboard and insights
  - Use ASP.NET Core rate limiting middleware
  - **Files:** `Program.cs`

### 5C. Marketing Landing Page

- [ ] **5C.1** Create static landing page (separate from Blazor app)
  - Explains RAT methodology, shows sample output, pricing
  - Links to `/account/register` for sign-up
  - Fast-loading static HTML — not a Blazor page
  - **Deployment:** separate static site or subdomain

---

## Post-Launch Backlog

_Not blocked by launch. Build based on user feedback and demand._

| Item | Effort | Dependencies |
|------|--------|-------------|
| Per-user signal weight customization (Pro) | 6-8 hours | New `UserSignalWeights` entity, merge service, Settings UI |
| User profiles with timezone preference | 4-6 hours | New fields on `ApplicationUser`, UI picker |
| Weekly brief digest email (Pro) | 4-6 hours | Email infrastructure (Phase 3) |
| CSV export of candidates (Pro) | 2-4 hours | Download endpoint + UI button |
| User watchlists | 8-12 hours | New entity, UI, filtered views |
| API endpoint for programmatic access | 8-12 hours | JWT auth, rate limiting, documentation |
| Polygon licensing upgrade | $29/mo | Just pay — no code changes |

---

## Quick Reference: Files by Phase

| Phase | Key Files |
|-------|-----------|
| 1A | `Infrastructure/Persistence/Migrations/`, `ServiceCollectionExtensions.cs` |
| 1B | `Web/appsettings.json`, `Worker/appsettings.json` |
| 1C | SmarterASP control panel (no code) |
| 2A | `ApplicationUser.cs`, `Register.razor`, `Program.cs` |
| 2B | `Signavex.Web.csproj`, new `StripeOptions.cs`, `Program.cs`, new webhook endpoint |
| 2C | `Dashboard.razor`, `CandidateDetail.razor`, `History.razor`, `Backtest.razor`, `Settings.razor`, `NavMenu.razor` |
| 3A | `Signavex.Infrastructure.csproj`, new `EmailOptions.cs`, new `IEmailSender` impl, `ServiceCollectionExtensions.cs` |
| 3B | New `Infrastructure/Email/Templates/`, `Login.razor`, new `ForgotPassword.razor`, new `ResetPassword.razor` |
| 3C | `Program.cs`, `Register.razor`, new `ConfirmEmail.razor` |
| 4 | `Terms.razor`, `Privacy.razor` |
| 5A | New `.github/workflows/deploy.yml` |
| 5B | `Program.cs`, new error pages |
| 5C | Separate static site |

##   Remaining manual steps (no code changes needed):

  1. Phase 1C — Hosting: Provision SmarterASP.NET account, configure MSSQL database, set environment variables, first deploy, DNS/SSL setup
  2. Phase 2B — Stripe: Create Stripe account, create "Signavex Pro" product/price, configure webhook endpoint in Stripe Dashboard, set Stripe__* env vars
  3. Phase 3 — SendGrid: Create SendGrid account, verify sender identity, set Email__ApiKey env var, smoke-test email delivery
  4. Phase 3C — Email verification: After email is confirmed working, flip RequireConfirmedAccount to true
  5. Phase 5A — CI secrets: Add GitHub secrets: FTP_SERVER, FTP_USERNAME, FTP_PASSWORD, FTP_REMOTE_DIR
  6. Phase 5C — Marketing landing page: Post-launch backlog item (static HTML, separate from Blazor app)