# Signavex — Public Launch Product Design

**Status:** Phase L1 implementation complete (pending deploy to Azure)
**Date:** April 11, 2026
**Goal:** Transform Signavex from "personal monitoring tool" into a polished, multi-tier product suitable for non-technical users

> **Naming decision (2026-04-11):** We are keeping the existing `Free` / `Pro` role names in code and in Stripe. The `Reader` / `Investor` labels in this document describe the *product model* but are not in-code identifiers. A later rename would touch too many files and Stripe products for too little user-visible benefit. When you see "Reader" in this doc, the in-code equivalent is `Free`; when you see "Investor", it's `Pro`.

---

## 1. Product Vision

Signavex began as a personal stock screening tool. After the R1+R2 refactoring and Azure deployment, it's technically production-ready — but the UX, navigation, content, and access model are still developer-grade.

**The vision:** Two distinct product experiences sharing one codebase, one database, and one account system. Users sign up once and access content based on the role(s) they pay for.

**Two product experiences:**

1. **Macro / Economy / Insights** — A daily-updated economic dashboard with AI-generated narrative briefs, FRED indicator tracking, correlation analysis, and recession probability monitoring. The value is in the **orchestration and aggregation** of many backend data sources into a single, readable view.

2. **Stock Screening** — A multi-signal stock screener with daily candidate scanning, signal breakdowns, candlestick charts, ticker history, and historical backtesting against the S&P 500/400 universe.

The two experiences share the same daily AI brief, market context, and economic data — but the stock screener layers per-stock analysis on top.

---

## 2. Target Users

| Persona | Description | Wants |
|---------|-------------|-------|
| **Reader** | Non-technical, curious about the economy. May or may not be an investor. Examples: dad (86, loves to learn), Kevin (non-technical, gets confused easily). | Plain-English explanation of "what's happening in the economy today" with charts, indicators, and a daily brief. Doesn't want to make trading decisions, just wants to be informed. |
| **Investor** | Active investor or aspiring investor. Comfortable with technical concepts (P/E ratios, RSI, moving averages). | Everything Reader gets, plus the ability to scan for candidates, drill into individual stocks, view chart patterns, and run backtests. |
| **Admin** | Mark (you). The operator. | Everything Investor gets, plus the ability to trigger scans, sync economic data, generate briefs on demand, and manage system settings. |

---

## 3. Access Model

**Core principle:** No public content. Everyone signs up. Content is gated by paid role.

### Tiers

| Tier | Cost (TBD) | Access |
|------|------|--------|
| **Trial** | $0 (7 days) | New signups get full Reader access for 7 days. After expiration, account becomes Lapsed (read-only landing page only) until they subscribe to Reader or Investor. |
| **Reader** | $5/mo (placeholder) | Full economy/insights experience: daily briefs, FRED indicators, correlation analysis, recession watch, market multiplier trend |
| **Investor** | $15/mo (placeholder) | Reader + stock screening: dashboard, candidate detail, history, backtest |
| **Admin** | n/a | Investor + admin controls (scan trigger, sync, brief generation, settings) |

### Why no truly free tier

The economy/insights view is not "free educational content" — it's the result of orchestrating many paid API calls (FRED, Anthropic, Polygon for context), running statistical analysis, generating AI briefs, and storing/serving the result. Each user costs real money in API quota, hosting, and brief generation. **The 7-day trial is the conversion funnel; ongoing access requires payment.**

### Why removing the admin buttons makes the product safe

A key insight: with the four user-triggerable expensive operations removed (Run Scan, Sync Data, Generate Brief, Run Backtest), **users have no way to burn API quota or compute time**. All expensive operations run on the Worker's schedule (daily scans at 10pm UTC, fundamentals backfill, etc.). The existing rate limits in `PolygonRateLimitingHandler` and `FundamentalsBackfillService` are sufficient because they only need to handle the Worker's controlled cadence — not user-driven button mashing.

This means once the admin buttons are gone, the app can scale to many users without per-user rate limiting infrastructure. The only resource that scales with users is page reads (lightweight DB queries) and any future per-user features.

### Sharing strategy

Users can share **specific page URLs** with non-subscribers (e.g., a particularly compelling daily brief). Non-subscribers visiting that URL see a teaser + signup prompt, not the full content. This is how publications like the New York Times handle "gift articles" — share the link, not the access.

**Implementation note:** This requires a "shareable token" system or similar. Defer to a later phase.

---

## 4. Information Architecture

### Navigation (after sign-in)

The current sidebar has 7+ items in a flat list. New structure:

**For Reader role:**
- 🏠 **Today** (Insights — daily AI brief) ← landing page after login
- 📊 **Economy** (FRED indicator dashboard)
- 📜 **Brief Archive** (past daily briefs)
- ⚙️ **Account** (profile, subscription, sign out)

**For Investor role (adds):**
- 🎯 **Stock Picks** (Dashboard — today's surfaced candidates)
- 🔍 **Stock Search** (History page — search any ticker, see appearances)
- 🧪 **Backtest**

**For Admin role (adds):**
- 🛠 **Admin** (run scan, sync data, generate brief, system settings)

### Page rename / consolidation

| Current | New label | Notes |
|---------|-----------|-------|
| Dashboard | Stock Picks | Clearer for non-technical users |
| Insights | Today | The daily entry point — make this the homepage for logged-in users |
| Economy | Economy | Same |
| History | Stock Search & History | Clarifies what's in there |
| Backtest | Backtest | Same |
| Settings | Admin / Account | Split — system settings go to Admin, user profile goes to Account |

### Routes

| Route | Visibility | Page |
|-------|-----------|------|
| `/` | Anonymous | Landing page (marketing, signup) |
| `/today` | Reader+ | Today's brief (formerly /insights, becomes the post-login default) |
| `/today/{date}` | Reader+ | Specific date's brief |
| `/economy` | Reader+ | Economic overview |
| `/economy/{seriesId}` | Reader+ | Indicator detail |
| `/picks` | Investor+ | Today's stock picks (formerly /dashboard) |
| `/picks/{ticker}` | Investor+ | Candidate detail (formerly /candidate/{ticker}) |
| `/search` | Investor+ | Ticker search & history |
| `/backtest` | Investor+ | Backtest tool |
| `/admin` | Admin only | Admin operations (scan, sync, brief, settings) |
| `/account` | Reader+ | Profile, subscription management, sign out |
| `/login` | Anonymous | Login (already exists) |
| `/register` | Anonymous | Signup |

---

## 5. Authentication & Onboarding

### Sign-in options

Current state: username + password only. For non-technical users, this is a friction point.

**Add:**
- **Sign in with Google** — your dad has Gmail; Kevin probably does. One click, no password to remember. Requires a Google Cloud project (free) and adding `Microsoft.AspNetCore.Authentication.Google` (Microsoft package, ~30 min to wire).
- **Magic link login** — fallback for users without Google. Email them a one-click sign-in link. Requires SendGrid (we have config placeholders for this; needs API key).

### First-time experience

Currently: new user lands on the dashboard, sees data they don't understand, no idea what to do.

**New flow:**
1. User signs up (Google or email)
2. Redirected to a **welcome page**: "Hi, here's what Signavex is and what to do first"
3. After welcome: redirected to the daily brief (`/today`)
4. Welcome page is shown only on first login (track via a `HasCompletedOnboarding` flag on the user)

### Welcome page content

- 1 sentence: "Signavex watches the economy and market every day and tells you what's interesting in plain English."
- 3 things they can do:
  1. **Read today's brief** — "the story of the market today"
  2. **Browse the economy** — "live indicators of where the economy is headed"
  3. (Investor role only) **See today's stock picks** — "stocks our system flagged as interesting"
- Single CTA button: "Start with today's brief"

---

## 6. UX Polish Standards

These apply across all pages.

### Plain English everywhere

Replace developer jargon with everyday language:

| Current | Better |
|---------|--------|
| "Surfacing Threshold: 0.45" | "Showing stocks scoring 45% or higher" |
| "Market Multiplier: 0.85x" | "Today's market mood: Cautious (-15%)" |
| "PeRatioVsIndustry: -0.5" | "Slightly overvalued vs. similar companies" |
| "MACD bearish crossover" | "Recent momentum has turned down" |
| "Candidate" | "Stock pick" |
| "Run Scan" | (hidden from non-admin users) |
| "Insufficient data for 14-day RSI" | "Not enough recent price data to evaluate this signal" |

### Tooltips on technical terms

For any term a non-investor wouldn't know (RSI, MACD, P/E ratio, Beta, Volatility, Bollinger Bands), add an `ⓘ` tooltip with a 1-sentence plain-English explanation.

### Empty states

Currently: "No candidates yet" — neutral but cold.

Better:
- **Dashboard with no candidates:** "The market is quiet today. Our system didn't find any opportunities meeting our criteria. Come back tomorrow."
- **Brief not yet generated:** "Today's brief is being prepared. Check back in a few minutes."
- **No scan history:** "Scan history will appear here once we've completed our first scans."

### Loading states

Every async operation should show:
- A spinner OR a skeleton placeholder
- An ETA if possible ("~30 seconds")
- A friendly message ("Looking at today's market...")

### Error states

Never show stack traces. All errors:
- Friendly message ("Something went wrong. We're looking into it.")
- A "Try again" button if applicable
- A "Contact support" link (mailto: for now)

### Mobile responsive

Both target users (dad, Kevin) might check on a phone. Every page needs to work on a 375px-wide screen. Test:
- Today's brief (text-heavy, should be easy)
- Economy dashboard (grid of cards needs to stack)
- Stock picks (grid of cards needs to stack)
- Candidate detail with charts (charts must scale)

### Sign-out is obvious

Currently might be hidden. Top-right corner, always visible, labeled "Sign out" not "Logout".

---

## 7. Admin Separation

The current admin operations (Run Scan, Sync Data, Generate Now, Backtest) need to be hidden from non-admin users.

### Strategy

1. Create a third role: `Admin` (alongside existing `Free`, `Pro` — though we'll likely rename these to `Reader` and `Investor` to match the new model)
2. Move all admin operations to a dedicated `/admin` page
3. Render the admin nav item only when `User.IsInRole("Admin")`
4. Server-side: `[Authorize(Roles = "Admin")]` on the admin endpoints
5. Seed your account into the Admin role via SQL

### Admin page contents

- **System actions:** Run Scan Now, Sync Economic Data Now, Generate Brief Now, Run Backtest
- **System info:** Last scan time, last sync time, last brief time, current Worker status
- **Settings display:** Read-only view of current configuration
- **User management** (eventually): View users, change roles, see subscription status

---

## 8. Landing Page

The unauthenticated home page (`/`) needs to actually sell the product.

### Sections

1. **Hero**
   - Headline: "Make sense of the market every day"
   - Subhead: "AI-powered daily briefs, economic indicators, and stock screening — all in one place."
   - CTA: "Start your free trial" → `/register`
2. **Three-feature row**
   - Daily AI brief (with screenshot)
   - Economic dashboard (with screenshot)
   - Stock screening (with screenshot, mention Investor tier)
3. **How it works** (3 steps)
   - "Sign up free"
   - "Get daily briefs in your inbox"
   - "Stay informed without spending hours"
4. **Pricing** (TBD, draft):
   - Reader $X/mo — economy + insights
   - Investor $Y/mo — adds stock screening
5. **Footer**: Links to Terms, Privacy, About, Contact

### Open question

Should the landing page show a **sample of today's brief** as a teaser to build trust ("look, here's what you'll get every day")? My instinct: yes, with a "Sign up to see the rest" cutoff after a few paragraphs.

---

## 9. Implementation Phases

Each phase is independently shippable and tested before moving on.

### Phase L1 — Admin Separation (foundation)

_Goal: Hide developer operations from non-admin users so we can safely show the app to anyone._

- [x] **L1.1** Add `Admin` role to the role seeder migration (`20260411120042_SeedAdminRole`)
- [x] **L1.2** Seed your account into Admin via a one-time SQL command (done on LocalDB; Azure SQL pending deploy)
- [x] **L1.3** Create new `/admin` page with the four admin actions (Run Scan, Sync Data, Generate Brief, Run Backtest) — `Components/Pages/Admin.razor`
- [x] **L1.4** Move admin POST endpoints to `/admin/scan`, `/admin/sync-economic`, `/admin/generate-brief`, `/admin/run-backtest` with `RequireRole("Admin")`
- [x] **L1.5** Remove the buttons from Dashboard, Economy, Insights, Backtest pages
- [x] **L1.6** Add Admin nav item in `NavMenu.razor`, gated via `<AuthorizeView Roles="Admin">`
- [x] **L1.7** Build clean (0 warnings, 0 errors) + all 207 tests pass

### Phase L1.8 — CI/CD via GitHub Actions + Azure OIDC (added mid-L1)

_Added during L1 because the previous deploy path was an unused SmarterASP FTP workflow and we needed a safe, repeatable way to ship L1 to Azure. See "Implementation Log" at the bottom of this doc for full context._

- [x] **L1.8a** Transfer GitHub repo from `mcarthey/Signavex` to `LearnedGeek/Signavex` (showcase project, matches existing ghcr.io org + Azure tenant)
- [x] **L1.8b** Update local git remote to the new URL
- [x] **L1.8c** Add `azuread` provider to `learnedgeek-infra/signavex/providers.tf`
- [x] **L1.8d** Add `github_oidc.tf` in `learnedgeek-infra/signavex/` — Azure AD app, service principal, federated credential (subject `repo:LearnedGeek/Signavex:ref:refs/heads/main`), and a `Website Contributor` role assignment scoped to the `signavex-web` App Service only
- [x] **L1.8e** `terraform apply` — 4 resources added, 0 changed, 0 destroyed
- [x] **L1.8f** Set three GitHub repo secrets via `gh secret set`: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
- [x] **L1.8g** Replace `.github/workflows/deploy.yml` with a two-job workflow: `ci` (build + test on every push/PR) and `deploy` (manual `workflow_dispatch` only, gated on `needs: ci`, authenticated via `azure/login@v2` OIDC)
- [ ] **L1.8h** Commit + push Signavex (fires CI, deploy stays dormant)
- [ ] **L1.8i** Commit + push learnedgeek-infra
- [ ] **L1.8j** Watch CI go green on main
- [ ] **L1.8k** Manually trigger deploy from the Actions tab → first real Azure deploy via OIDC

### Phase L1.9 — Post-deploy Admin role assignment on Azure SQL

- [ ] **L1.9a** After deploy completes, the `SeedAdminRole` migration runs automatically on boot and creates the `Admin` role row in Azure SQL
- [ ] **L1.9b** Register `markm@learnedgeek.com` (or chosen admin account) on the deployed app
- [ ] **L1.9c** Connect to Azure SQL via sqlcmd or Azure Data Studio, run `INSERT INTO AspNetUserRoles` to put the admin user in the Admin role
- [ ] **L1.9d** Log in, confirm Admin nav item appears, hit `/admin`, verify all four action cards render
- [ ] **L1.9e** Log in as a test non-admin account (or use incognito), confirm no Admin nav item and `/admin` returns 403

### Phase L2 — Access Matrix (names kept as Free/Pro)

_Goal: Enforce the Reader/Investor product model using the existing `Free` and `Pro` role names._

**Decision (2026-04-11):** We are **not** renaming Free→Reader or Pro→Investor. The rename would touch ~20+ files and require reconfiguring Stripe products for essentially cosmetic benefit. The product calls them Reader / Investor in user-facing copy; the code keeps Free / Pro. A future rename can happen alongside a Stripe product migration if ever needed.

- [ ] **L2.1** Document the mapping in CLAUDE.md: `Free = Reader product tier`, `Pro = Investor product tier`
- [ ] **L2.2** Add `[Authorize(Roles = "Pro,Admin")]` (or equivalent policy) to stock-screening pages: Dashboard, Candidate detail, History, Backtest
- [ ] **L2.3** Confirm Economy, Insights, and economy detail pages are accessible to Free, Pro, and Admin
- [ ] **L2.4** Update `UpgradePrompt` copy to reference "Investor tier" in user-facing text while keeping the `Pro` role internally
- [ ] **L2.5** Verify: Free users see economy + insights only; Pro users see all non-admin pages

### Phase L3 — Navigation & Information Architecture

_Goal: Restructure the sidebar and routes to match the new product model._

- [x] **L3.1** Rename routes per the route table in section 4. No backward-compat aliases retained — soft-launch state means there are no external links, bookmarks, or open tabs to honor, so `/dashboard`, `/insights`, `/history`, `/candidate/{ticker}` and `/dashboard/export.csv` are all gone. Old URLs return 404.
- [x] **L3.2** Restructure NavMenu.razor with the new nav items in the new order (Today → Economy → Stock Picks → Stock Search → Backtest → About → Admin → Settings)
- [x] **L3.3** Make `/today` the post-login landing page — `Program.cs` now has `app.MapGet("/", () => Results.Redirect("/today"))` so the URL bar normalizes correctly
- [x] **L3.4** Update internal links: CandidateDetail breadcrumb + return link, Dashboard candidate cards, Dashboard CSV link, Dashboard BuildHref helper, History BuildHref + form action, Insights archive sidebar
- [x] **L3.5** Page titles + h1 headings updated for renamed pages (Daily Insights → Today, Dashboard → Stock Picks, Scan History → Stock Search)
- [x] **L3.6** CSV export endpoint at `/picks/export.csv` only
- [ ] **L3.7** Smoke test on production: log in, verify nav, click each item, confirm everything renders

**Discovered during L3 (deferred to L4 discussion):** `Insights.razor` and `Economy.razor` have no `[Authorize]` attribute, so anonymous users can currently read daily briefs and the economy dashboard at `/insights` and `/economy`. The product intent (per Section 3) is paid-only access. This is a real security gap relative to the design — fix is one line per file but is a behavioral change worth discussing before applying.

### Phase L4 — Plain English Pass

_Goal: Every user-facing string reviewed for clarity._

- [ ] **L4.1** Audit every `.razor` file for jargon, technical terms, raw numbers
- [ ] **L4.2** Replace per the table in section 6
- [ ] **L4.3** Add `ⓘ` tooltips to technical terms (RSI, MACD, P/E, etc.)
- [ ] **L4.4** Rewrite empty states to be friendly
- [ ] **L4.5** Rewrite error messages — no stack traces
- [ ] **L4.6** Verify: read every page out loud, ask "would my dad understand this?"

### Phase L5 — Onboarding & Welcome

_Goal: First-time users know what to do._

- [ ] **L5.1** Add `HasCompletedOnboarding` field to ApplicationUser (migration)
- [ ] **L5.2** Create welcome page (`/welcome`)
- [ ] **L5.3** Redirect new users to `/welcome` after first login, then to `/today`
- [ ] **L5.4** Add a "Take the tour" button on the welcome page (optional, can defer)
- [ ] **L5.5** Verify: register a new test account, walk through the flow

### Phase L6 — Sign-in Improvements

_Goal: Reduce login friction for non-technical users._

- [ ] **L6.1** Set up Google Cloud project for OAuth
- [ ] **L6.2** Add `Microsoft.AspNetCore.Authentication.Google` package
- [ ] **L6.3** Configure Google OAuth in `Program.cs` (client ID, secret in Azure config)
- [ ] **L6.4** Add "Sign in with Google" button to login page
- [ ] **L6.5** Set up SendGrid (or alternative) and configure transactional email
- [ ] **L6.6** Implement magic link login flow (optional, defer if Google is sufficient)
- [ ] **L6.7** Verify: log in as your dad's Gmail, confirm account auto-created

### Phase L7 — Landing Page

_Goal: Convert anonymous visitors into signups._

- [ ] **L7.1** Build the new landing page at `/`
- [ ] **L7.2** Hero, features, how-it-works, pricing, footer (per section 8)
- [ ] **L7.3** Add screenshots of the actual product
- [ ] **L7.4** Decide on pricing (placeholder: Reader $5/mo, Investor $15/mo)
- [ ] **L7.5** Verify: visit incognito, click through the signup flow
- [ ] **L7.6** Optional: add a sample brief teaser

### Phase L8 — Mobile Responsive

_Goal: Every page works on a 375px-wide phone._

- [ ] **L8.1** Test every page on Chrome dev tools mobile view (iPhone SE size)
- [ ] **L8.2** Fix any horizontal scrolling
- [ ] **L8.3** Verify charts (PriceChart, sparklines) scale correctly
- [ ] **L8.4** Verify navigation works (hamburger menu? side drawer?)
- [ ] **L8.5** Real-device test on a phone

### Phase L9 — Polish: Empty / Loading / Error States

_Goal: No raw error messages or confusing blank pages._

- [ ] **L9.1** Audit every page for empty state, loading state, error state
- [ ] **L9.2** Add friendly messages per section 6
- [ ] **L9.3** Remove any remaining stack trace exposure
- [ ] **L9.4** Add a global error page that's friendly

### Phase L10 — Soft Launch

_Goal: First real users._

- [ ] **L10.1** Show the polished app to your dad in person, watch him use it
- [ ] **L10.2** Iterate based on what confused him
- [ ] **L10.3** Show to Kevin, repeat
- [ ] **L10.4** Expand to 5-10 friends as a beta
- [ ] **L10.5** Set up basic analytics (Application Insights or similar) to see what people actually use
- [ ] **L10.6** Decide: is this worth more investment or keep as a personal tool?

---

## 10. Open Questions

Things to decide as we go:

1. **Pricing.** What are Reader and Investor tier prices? Need to cover hosting (~$25/mo) + API costs (~$5/mo Anthropic + Polygon scaling). Even at $5 Reader / $15 Investor, you need ~5-10 paying users to break even.

2. **Free trial length?** 7 days? 14 days? 30 days?

3. **Email opt-in for daily brief?** Could send the brief to Reader subscribers as an email, not just on the website. Higher engagement, but more SendGrid cost.

4. **Sample teaser on landing page?** Show today's brief headline + first paragraph publicly to build trust?

5. **Shareable URL strategy.** Token-based (signed URLs) or session-based ("here's a free read")?

6. **Admin user management UI.** When you have 50 users, you'll want to see their subscription status, upgrade/downgrade them, etc. Defer or build now?

7. **Should we eventually email a "weekly recap" instead of daily?** For users who don't want daily emails.

8. **Compliance/legal review.** Even with disclaimers, this borders on financial advice. Talk to a lawyer before charging money.

---

## 11. Effort Estimate

Rough total: **40-60 hours** spread across phases. Realistic timeline: **2-4 weeks of focused work** (a few hours per evening), or **a few weekends** of focused work.

| Phase | Effort | Cumulative |
|-------|--------|------------|
| L1 — Admin separation | 3h | 3h |
| L2 — Role restructure | 4h | 7h |
| L3 — Navigation/IA | 4h | 11h |
| L4 — Plain English pass | 6h | 17h |
| L5 — Onboarding | 4h | 21h |
| L6 — Sign-in improvements | 6h | 27h |
| L7 — Landing page | 5h | 32h |
| L8 — Mobile responsive | 4h | 36h |
| L9 — Polish (empty/loading/error) | 4h | 40h |
| L10 — Soft launch + iteration | 5h+ | 45h+ |

L1 is the highest priority and most foundational. Even if you do nothing else, L1 makes the app safe to show anyone.

---

## 12. Implementation Log

### 2026-04-11 — L1 session

**What was planned:** Items L1.1 through L1.7 — create Admin role, move admin ops under `/admin`, hide the buttons from user pages.

**What actually shipped:**

1. **L1.1–L1.7 as planned.** New seed migration `20260411120042_SeedAdminRole` creates the role row. `Components/Pages/Admin.razor` holds the four admin actions. `Program.cs` routes `/admin/scan`, `/admin/sync-economic`, `/admin/generate-brief`, `/admin/run-backtest` with `RequireRole("Admin")`. Dashboard, Economy, Insights, and Backtest pages had their admin forms removed. `NavMenu.razor` gates the Admin nav item via `<AuthorizeView Roles="Admin">`. Build is clean (0 warnings, 0 errors); all 207 tests pass.

2. **Unplanned detour: CI/CD via OIDC (L1.8).** When we went to deploy L1 to Azure, the existing `.github/workflows/deploy.yml` was a leftover SmarterASP FTP pipeline from before the Azure pivot — unusable. Rather than do a manual `az webapp deploy` one-off, we set up a proper GitHub Actions workflow with Azure OIDC federated credentials. Rationale: matches the "everything through Terraform" posture in user memory, no long-lived credentials stored anywhere, and every future deploy is one click.

3. **GitHub repo transfer.** Moved `mcarthey/Signavex` → `LearnedGeek/Signavex` before setting up OIDC, so the federated credential subject string could be locked to the final URL from day one. The LearnedGeek org already owned the ghcr.io namespace for `signavex-worker` and matches the Azure tenant (`learnedgeek.com`), so this was aligning code with everything else.

4. **Terraform changes in `learnedgeek-infra/signavex/`:**
   - `providers.tf` — added `hashicorp/azuread ~> 3.0` alongside existing azurerm
   - `github_oidc.tf` — new file: `azuread_application` + `azuread_service_principal` + `azuread_application_federated_identity_credential` (subject `repo:LearnedGeek/Signavex:ref:refs/heads/main`) + `azurerm_role_assignment` (role: `Website Contributor`, scope: `signavex-web` App Service only — *not* the resource group, *not* the subscription)
   - Three outputs: `github_oidc_client_id`, `github_oidc_tenant_id`, `github_oidc_subscription_id`
   - `terraform apply`: 4 to add, 0 to change, 0 to destroy

5. **GitHub repo secrets** set via `gh secret set` against `LearnedGeek/Signavex`: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`. These are not cryptographic secrets (all three are identifiers, not passwords), but GitHub stores them as secrets to keep them out of workflow logs by default. The real security of the setup is the short-lived OIDC token exchange, not the confidentiality of these three IDs.

6. **New workflow (`.github/workflows/deploy.yml`)** has two jobs:
   - `ci` — runs on every push and PR to `main`. Restores, builds Release, runs all tests.
   - `deploy` — runs only on `workflow_dispatch` (manual trigger from the Actions tab). `needs: ci` so it cannot start unless CI passed. Authenticates to Azure via `azure/login@v2` using the OIDC token (requires `permissions: id-token: write` — without that line, GitHub silently refuses to mint the token and the login step fails with an opaque error). Then `dotnet publish src/Signavex.Web` and `azure/webapps-deploy@v3` against `signavex-web`.

**Key lesson for future phases:** Security posture decisions made in Terraform (like `ftp_publish_basic_authentication_enabled = false`) propagate into what CI/CD options remain available. Turning off basic auth on the SCM endpoint means publish profiles are off the table and OIDC is the only clean path forward. This is a good constraint — it forces the right answer — but it's worth knowing that every "convenience" we disable in Terraform eliminates a corresponding "easy" path elsewhere.

**Remaining L1 work:**

- Commit + push both repos
- Watch CI go green
- Manually trigger the deploy workflow
- Register admin account on the deployed app (Azure SQL is empty of users)
- Run `INSERT INTO AspNetUserRoles` via sqlcmd to put admin account in Admin role
- Smoke test: log in, verify Admin nav + `/admin` page work; log in as a fresh non-admin test account, verify no Admin UI appears anywhere
