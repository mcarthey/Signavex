# Signavex — Implementation Plan
**Version 1.0 | Based on Trade Secrets RAT (Risk Averse Trader) Methodology**

---

## 1. Project Overview

Signavex is a market discovery and signal analysis tool that surfaces stocks worth investigating by stacking multiple confirming signals. It is **not** a prediction engine or automated trading system — it is a **risk reduction tool** that helps the investor make more informed decisions by ensuring multiple independent indicators agree before a candidate is surfaced.

### Philosophy
Directly derived from the Trade Secrets Stock Market Investment Strategies "Risk Averse Trader" (RAT) Level One curriculum (1998/1999):
- RATs are opportunity seekers who look for factors that affect a stock's value
- Nothing is a sure thing — the goal is to reduce risk, not eliminate it
- Signals must be evaluated in context: macro conditions affect the weight of individual stock signals
- News, technical analysis, and fundamentals must all be considered together

### Core Principles
- Stack multiple confirming signals before surfacing a candidate
- Market-level conditions gate or multiply stock-level scores
- Every surfaced candidate shows *why* it appeared — full signal breakdown
- Fully extensible: new signals can be added without modifying existing ones (Open/Closed Principle)
- Configurable weighting: stronger macro indicators carry more weight by default

---

## 2. Stock Universe

| Tier | Index | Status | Notes |
|------|-------|--------|-------|
| 1 | S&P 500 | **Initial implementation** | Large-cap, high data quality |
| 2 | S&P 400 (Mid-Cap) | **Initial implementation** | Strong growth potential, reliable data |
| 3 | S&P 600 (Small-Cap) | Planned — future release | Higher noise, stricter signal threshold required |

**Discovery Model:** Signavex is a *discovery* tool, not a watchlist monitor. It scans the full universe daily and surfaces candidates the investor wouldn't necessarily have thought to look at. A personal watchlist is a secondary/optional feature, not the primary use case.

---

## 3. Signal Architecture

### Two-Tier Signal Model

#### Tier 1: Market-Level Signals (Gates / Multipliers)
These evaluate broad market conditions and act as a context layer. Poor macro conditions discount all stock-level scores. Strong macro conditions can amplify them. These signals do **not** score individual stocks directly.

| Signal | Description | Source |
|--------|-------------|--------|
| Market Trend | Is the S&P 500 in an uptrend (price above 50-day and 200-day MA)? | OHLCV data |
| Interest Rate Environment | Fed rate direction — rising rates are bearish, falling/stable bullish | Economic data API |
| VIX Level | Market fear gauge — high VIX discounts signals, low VIX supports them | Market data API |
| Sector Momentum | Is the stock's sector trending up or down overall? | Sector ETF data |

#### Tier 2: Stock-Level Signals (Individual Scorers)
Each signal evaluates one aspect of a stock and returns a score and a reason. All ten signals are derived directly from the RAT Level One curriculum (pages 161–171).

| # | Signal | RAT Source | Description |
|---|--------|------------|-------------|
| 1 | Volume Threshold | p.169 | Minimum 500,000 shares/day; volume spike confirms momentum |
| 2 | Moving Average Crossover | p.169 | 14-day MA crossing above 30-day MA = bullish signal |
| 3 | Support/Resistance Position | p.169 | Price near support (buying opportunity) or breaking resistance (breakout) |
| 4 | Trend Direction | p.170 | Continuous upward movement from a base forming |
| 5 | Channel Position | p.169-170 | Price at or near bottom of established channel |
| 6 | News Sentiment | p.163-168 | Positive catalyst present; news not already "in the stock" |
| 7 | Analyst Rating | p.162 | Rating of Outperform, Outright Buy, or Buy tier |
| 8 | P/E vs Industry | p.170-171 | Stock P/E below industry average (potential undervaluation) |
| 9 | Debt/Equity Ratio | p.171 | Low D/E ratio preferred; high D/E is a negative signal |
| 10 | Earnings Trend | p.170, 164-165 | Meeting or exceeding expectations; positive earnings surprise |

> **Note:** Additional signals will be added as the RAT workbook is reviewed further, particularly from the entrance criteria checklists in Chapters 3 and 4.

### Signal Evaluation Frequency

| Type | Signals | Frequency |
|------|---------|-----------|
| Technical | Volume, MA Crossover, Support/Resistance, Trend, Channel | Daily (end-of-day) |
| News/Sentiment | News Sentiment, Analyst Rating | Daily |
| Fundamental | P/E Ratio, D/E Ratio, Earnings Trend | Quarterly refresh, cached between updates |

---

## 4. Scoring Model

### Stock Score Calculation
Each stock-level signal returns a **SignalResult** containing:
- A score (-1.0 to 1.0, where negative = bearish, 0 = neutral, positive = bullish)
- A configurable weight (default weights defined in appsettings)
- A human-readable reason string for dashboard display

**Weighted Score Formula:**
```
StockScore = Sum(SignalResult.Score * SignalResult.Weight) / Sum(SignalResult.Weight)
```

### Market Multiplier
The Tier 1 market-level evaluation produces a **MarketContext** multiplier (0.5 to 1.5):
- Poor macro conditions: multiplier < 1.0 (discounts all scores)
- Neutral conditions: multiplier = 1.0
- Strong macro conditions: multiplier > 1.0 (amplifies scores)

**Final Score:**
```
FinalScore = StockScore * MarketMultiplier
```

### Surfacing Threshold
Only stocks with a FinalScore above a configurable threshold (default: 0.65) appear in the dashboard results. This threshold is tunable per tier — small-caps require a higher threshold than large-caps.

### Score Display
Dashboard uses a **traffic light + numeric** display:
- 🟢 Green: 0.75 and above — Strong candidate
- 🟡 Yellow: 0.65–0.74 — Worth watching
- 🔴 Red: Below threshold — Not surfaced (available in debug/explore mode)

---

## 5. Data Provider Strategy

All data access is abstracted behind interfaces, allowing provider swapping without touching business logic.

### Recommended Starting Providers

| Data Type | Provider | Tier | Notes |
|-----------|----------|------|-------|
| OHLCV (daily) | Polygon.io | Free tier to start | Good S&P 500/400 coverage |
| News/Sentiment | Polygon.io or NewsAPI | Free tier | Headlines + sentiment scoring |
| Fundamentals (P/E, D/E, Earnings) | Alpha Vantage | Free tier | Quarterly data, sufficient for fundamentals |
| Index Composition | SPDR ETF holdings (SPY, MDY) | Free/scrape | S&P 500 and 400 constituent lists |
| Economic Data (rates, VIX) | FRED API (Federal Reserve) | Free | Fed rate data, economic indicators |

### Key Interfaces
```csharp
public interface IMarketDataProvider
{
    Task<IEnumerable<OhlcvRecord>> GetDailyOhlcvAsync(string ticker, int days);
    Task<IEnumerable<string>> GetIndexConstituentsAsync(MarketIndex index);
}

public interface INewsDataProvider
{
    Task<IEnumerable<NewsItem>> GetRecentNewsAsync(string ticker, int days);
}

public interface IFundamentalsProvider
{
    Task<FundamentalsData> GetFundamentalsAsync(string ticker);
}

public interface IEconomicDataProvider
{
    Task<MacroIndicators> GetMacroIndicatorsAsync();
}
```

---

## 6. Core Domain Model

```csharp
// The result of a single signal evaluation
public record SignalResult(
    string SignalName,
    double Score,          // -1.0 to 1.0
    double Weight,         // Configurable
    string Reason,         // Human-readable explanation for dashboard
    bool IsAvailable       // False if data was unavailable
);

// Market context from Tier 1 evaluation
public record MarketContext(
    double Multiplier,     // 0.5 to 1.5
    string Summary,        // e.g. "Bullish: S&P uptrend, VIX low, rates stable"
    IEnumerable<SignalResult> MarketSignals
);

// A candidate stock with all signal results
public record StockCandidate(
    string Ticker,
    string CompanyName,
    MarketTier Tier,
    double RawScore,
    double FinalScore,
    IEnumerable<SignalResult> SignalResults,
    MarketContext MarketContext,
    DateTime EvaluatedAt
);

// The signal interface — every signal implements this
public interface IStockSignal
{
    string Name { get; }
    double DefaultWeight { get; }
    Task<SignalResult> EvaluateAsync(StockData stock);
}

public interface IMarketSignal
{
    string Name { get; }
    Task<SignalResult> EvaluateAsync(MacroIndicators indicators);
}
```

---

## 7. Project Structure

```
Signavex/
├── Signavex.sln
│
├── src/
│   ├── Signavex.Domain/               # Core domain models, interfaces
│   │   ├── Models/
│   │   │   ├── StockCandidate.cs
│   │   │   ├── SignalResult.cs
│   │   │   ├── MarketContext.cs
│   │   │   ├── OhlcvRecord.cs
│   │   │   └── FundamentalsData.cs
│   │   ├── Interfaces/
│   │   │   ├── IStockSignal.cs
│   │   │   ├── IMarketSignal.cs
│   │   │   ├── IMarketDataProvider.cs
│   │   │   ├── INewsDataProvider.cs
│   │   │   ├── IFundamentalsProvider.cs
│   │   │   └── IEconomicDataProvider.cs
│   │   └── Enums/
│   │       ├── MarketTier.cs
│   │       └── SignalStrength.cs
│   │
│   ├── Signavex.Signals/              # All signal implementations
│   │   ├── Technical/
│   │   │   ├── VolumeThresholdSignal.cs
│   │   │   ├── MovingAverageCrossoverSignal.cs
│   │   │   ├── SupportResistanceSignal.cs
│   │   │   ├── TrendDirectionSignal.cs
│   │   │   └── ChannelPositionSignal.cs
│   │   ├── Fundamental/
│   │   │   ├── PeRatioSignal.cs
│   │   │   ├── DebtEquitySignal.cs
│   │   │   └── EarningsTrendSignal.cs
│   │   ├── Sentiment/
│   │   │   ├── NewsSentimentSignal.cs
│   │   │   └── AnalystRatingSignal.cs
│   │   └── Market/
│   │       ├── MarketTrendSignal.cs
│   │       ├── InterestRateSignal.cs
│   │       ├── VixLevelSignal.cs
│   │       └── SectorMomentumSignal.cs
│   │
│   ├── Signavex.Engine/               # Scanning pipeline and orchestration
│   │   ├── ScanEngine.cs              # Main orchestrator
│   │   ├── MarketEvaluator.cs         # Tier 1 market context evaluation
│   │   ├── StockEvaluator.cs          # Tier 2 per-stock evaluation
│   │   ├── ScoreCalculator.cs         # Weighted scoring logic
│   │   └── UniverseProvider.cs        # Gets S&P 500/400 constituent lists
│   │
│   ├── Signavex.Infrastructure/       # Data provider implementations
│   │   ├── Polygon/
│   │   │   ├── PolygonMarketDataProvider.cs
│   │   │   └── PolygonNewsProvider.cs
│   │   ├── AlphaVantage/
│   │   │   └── AlphaVantageFundamentalsProvider.cs
│   │   └── Fred/
│   │       └── FredEconomicDataProvider.cs
│   │
│   └── Signavex.Web/                  # Blazor Server dashboard
│       ├── Pages/
│       │   ├── Dashboard.razor        # Main candidates view
│       │   ├── CandidateDetail.razor  # Per-stock signal breakdown
│       │   └── Settings.razor         # Signal weights configuration
│       ├── Components/
│       │   ├── SignalBreakdown.razor   # Visual signal score display
│       │   ├── MarketContextBar.razor  # Macro conditions summary
│       │   └── ScoreBadge.razor        # Traffic light score display
│       └── Services/
│           └── ScanResultsService.cs   # Bridges engine results to UI
│
└── tests/
    ├── Signavex.Signals.Tests/        # Unit tests per signal
    ├── Signavex.Engine.Tests/         # Pipeline integration tests
    └── Signavex.Infrastructure.Tests/ # Provider tests with mocked HTTP
```

---

## 8. Technical Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 8 |
| Frontend | Blazor Server |
| Signal Library | Skender.Stock.Indicators (NuGet) |
| HTTP Client | Typed HttpClient with Polly resilience |
| Configuration | appsettings.json with strongly-typed options |
| Dependency Injection | Microsoft.Extensions.DependencyInjection |
| Testing | xUnit + Moq |
| Scheduling | .NET BackgroundService (daily end-of-day trigger) |
| Data Caching | IMemoryCache for fundamentals (quarterly refresh) |

---

## 9. Configuration (appsettings.json sketch)

```json
{
  "Signavex": {
    "SurfacingThreshold": 0.65,
    "Universe": ["SP500", "SP400"],
    "SignalWeights": {
      "VolumeThreshold": 1.0,
      "MovingAverageCrossover": 1.5,
      "SupportResistance": 1.2,
      "TrendDirection": 1.5,
      "ChannelPosition": 1.0,
      "NewsSentiment": 1.3,
      "AnalystRating": 1.2,
      "PeRatioVsIndustry": 1.0,
      "DebtEquityRatio": 0.8,
      "EarningsTrend": 1.3
    },
    "MarketSignalWeights": {
      "MarketTrend": 2.0,
      "InterestRateEnvironment": 1.5,
      "VixLevel": 1.5,
      "SectorMomentum": 1.0
    }
  },
  "DataProviders": {
    "Polygon": {
      "ApiKey": "",
      "BaseUrl": "https://api.polygon.io"
    },
    "AlphaVantage": {
      "ApiKey": "",
      "BaseUrl": "https://www.alphavantage.co"
    },
    "Fred": {
      "ApiKey": "",
      "BaseUrl": "https://api.stlouisfed.org"
    }
  }
}
```

---

## 10. Implementation Phases

### Phase 1 — Foundation
- Solution structure and all projects
- Domain models and interfaces
- Configuration setup
- DI registration

### Phase 2 — Data Infrastructure
- Polygon OHLCV provider
- Alpha Vantage fundamentals provider
- FRED economic data provider
- Index constituent list provider
- Unit tests with mocked HTTP responses

### Phase 3 — Signals (Technical first)
- Volume, MA Crossover, Support/Resistance, Trend, Channel
- Full unit test coverage per signal
- Use Skender.Stock.Indicators for calculations

### Phase 4 — Signals (Fundamental + Sentiment)
- P/E, D/E, Earnings Trend
- News Sentiment, Analyst Rating
- Caching layer for fundamentals

### Phase 5 — Scan Engine
- Market evaluator (Tier 1)
- Stock evaluator (Tier 2)
- Score calculator with weighting
- Background service for daily scheduling

### Phase 6 — Blazor Dashboard
- Candidates list with sorting/filtering
- Signal breakdown detail view
- Market context summary bar
- Settings page for weight configuration

### Phase 7 — Refinement
- Add S&P 600 small-cap tier (higher threshold)
- Additional signals from RAT workbook Chapters 3 & 4
- Backtesting/historical validation mode
- Export candidates to CSV

---

## 11. Key Design Decisions

- **Open/Closed for signals:** New signals implement `IStockSignal` and register in DI — no existing code changes required
- **Provider abstraction:** Switching from Polygon to another data source requires only a new infrastructure implementation
- **Fundamental caching:** P/E, D/E, earnings data updates quarterly — cache aggressively, refresh on earnings calendar events
- **News "already in the stock" check:** Per RAT methodology (p.163-168), check for recent price run-up before surfacing news-driven candidates. If stock has already moved significantly toward the news, discount the news signal score.
- **Small-cap gating:** S&P 600 tier uses a higher surfacing threshold (default 0.75 vs 0.65) to account for higher noise

---

## 12. Source Methodology Reference

All signal definitions trace back to:
> *Trade Secrets Stock Market Investment Strategies, Inc.*
> *Stock & Option Strategies Workshop — Level One Workbook*
> *Copyright 1998, 1999*
> *RAT (Risk Averse Trader) Level One Certification*

Key pages: 161–171 (Factors Influencing Prices, Technical Analysis, Fundamentals)
Additional criteria to be added from: Chapters 3 & 4 entrance/exit checklists

---

*Document prepared for handoff to Claude Code. All architectural decisions and signal definitions finalized in planning conversation. Refer back to this document for design rationale.*
