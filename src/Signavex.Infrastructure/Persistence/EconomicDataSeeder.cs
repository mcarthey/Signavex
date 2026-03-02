using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Signavex.Domain.Models.Economic;
using Signavex.Infrastructure.Persistence.Entities;

namespace Signavex.Infrastructure.Persistence;

public static class EconomicDataSeeder
{
    public static async Task SeedAsync(IDbContextFactory<SignavexDbContext> dbFactory, ILogger? logger = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        if (await db.EconomicSeries.AnyAsync())
            return;

        var series = GetSeedData();
        db.EconomicSeries.AddRange(series);
        await db.SaveChangesAsync();

        logger?.LogInformation("Seeded {Count} economic series", series.Count);
    }

    private static List<EconomicSeriesEntity> GetSeedData() =>
    [
        // Employment & Labor (enabled)
        new()
        {
            SeriesId = "UNRATE", Name = "Civilian Unemployment Rate",
            Description = "Civilian Unemployment Rate",
            Frequency = "Monthly", Units = "Percent", SeasonalAdjustment = "Seasonally Adjusted",
            IsEnabled = true, Category = (int)EconomicCategory.Employment
        },
        new()
        {
            SeriesId = "PAYEMS", Name = "Total Nonfarm Payrolls",
            Description = "All Employees: Total Nonfarm Payrolls, Thousands of Persons",
            Frequency = "Monthly", Units = "Thousands of Persons", SeasonalAdjustment = "Seasonally Adjusted",
            IsEnabled = true, Category = (int)EconomicCategory.Employment
        },

        // Inflation & Prices (enabled)
        new()
        {
            SeriesId = "CPIAUCSL", Name = "Consumer Price Index",
            Description = "Consumer Price Index for All Urban Consumers: All Items in U.S. City Average",
            Frequency = "Monthly", Units = "Index 1982-1984=100", SeasonalAdjustment = "Seasonally Adjusted",
            IsEnabled = true, Category = (int)EconomicCategory.Inflation
        },
        new()
        {
            SeriesId = "PPIACO", Name = "Producer Price Index",
            Description = "Producer Price Index by Commodity: All Commodities",
            Frequency = "Monthly", Units = "Index 1982=100", SeasonalAdjustment = "Not Seasonally Adjusted",
            IsEnabled = true, Category = (int)EconomicCategory.Inflation
        },

        // Economic Growth (enabled)
        new()
        {
            SeriesId = "GDPC1", Name = "Real GDP",
            Description = "Real Gross Domestic Product, Billions of Chained 2017 Dollars",
            Frequency = "Quarterly", Units = "Billions of Chained 2017 Dollars", SeasonalAdjustment = "Seasonally Adjusted Annual Rate",
            IsEnabled = true, Category = (int)EconomicCategory.Growth
        },
        new()
        {
            SeriesId = "INDPRO", Name = "Industrial Production Index",
            Description = "Industrial Production Index",
            Frequency = "Monthly", Units = "Index 2017=100", SeasonalAdjustment = "Seasonally Adjusted",
            IsEnabled = true, Category = (int)EconomicCategory.Growth
        },

        // Markets & Rates (enabled)
        new()
        {
            SeriesId = "FEDFUNDS", Name = "Federal Funds Rate",
            Description = "Effective Federal Funds Rate",
            Frequency = "Monthly", Units = "Percent", SeasonalAdjustment = "Not Seasonally Adjusted",
            IsEnabled = true, Category = (int)EconomicCategory.InterestRates
        },
        new()
        {
            SeriesId = "GS10", Name = "10-Year Treasury Yield",
            Description = "10-Year Treasury Constant Maturity Rate",
            Frequency = "Monthly", Units = "Percent", SeasonalAdjustment = "Not Seasonally Adjusted",
            IsEnabled = true, Category = (int)EconomicCategory.InterestRates
        },
        new()
        {
            SeriesId = "SP500", Name = "S&P 500",
            Description = "S&P 500 Index",
            Frequency = "Monthly", Units = "Index", SeasonalAdjustment = "Not Seasonally Adjusted",
            IsEnabled = true, Category = (int)EconomicCategory.Market
        },
        new()
        {
            SeriesId = "RECPROUSM156N", Name = "Recession Probability",
            Description = "Smoothed U.S. Recession Probabilities",
            Frequency = "Monthly", Units = "Percent", SeasonalAdjustment = "Not Seasonally Adjusted",
            IsEnabled = true, Category = (int)EconomicCategory.Market
        },

        // Housing (enabled)
        new()
        {
            SeriesId = "MORTGAGE30US", Name = "30-Year Mortgage Rate",
            Description = "30-Year Fixed Rate Mortgage Average in the United States",
            Frequency = "Weekly", Units = "Percent", SeasonalAdjustment = "Not Seasonally Adjusted",
            IsEnabled = true, Category = (int)EconomicCategory.Housing
        },

        // Consumer (enabled)
        new()
        {
            SeriesId = "PCE", Name = "Personal Consumption Expenditures",
            Description = "Personal Consumption Expenditures, Billions of Dollars",
            Frequency = "Monthly", Units = "Billions of Dollars", SeasonalAdjustment = "Seasonally Adjusted Annual Rate",
            IsEnabled = true, Category = (int)EconomicCategory.Consumer
        },

        // === Disabled series (from EDT) ===
        new()
        {
            SeriesId = "DGS10", Name = "10-Year Treasury Daily",
            Description = "Market Yield on U.S. Treasury Securities at 10-Year Constant Maturity",
            Frequency = "Daily", Units = "Percent", SeasonalAdjustment = "Not Seasonally Adjusted",
            IsEnabled = false, Category = (int)EconomicCategory.InterestRates
        },
        new()
        {
            SeriesId = "M2SL", Name = "M2 Money Supply",
            Description = "M2 Money Stock, Seasonally Adjusted",
            Frequency = "Monthly", Units = "Billions of Dollars", SeasonalAdjustment = "Seasonally Adjusted",
            IsEnabled = false, Category = (int)EconomicCategory.Growth
        },
        new()
        {
            SeriesId = "UMCSENT", Name = "Consumer Sentiment",
            Description = "University of Michigan: Consumer Sentiment Index",
            Frequency = "Monthly", Units = "Index 1966:Q1=100", SeasonalAdjustment = "Not Seasonally Adjusted",
            IsEnabled = false, Category = (int)EconomicCategory.Consumer
        },
        new()
        {
            SeriesId = "HOUST", Name = "Housing Starts",
            Description = "Housing Starts: Total New Privately Owned Housing Units Started",
            Frequency = "Monthly", Units = "Thousands of Units", SeasonalAdjustment = "Seasonally Adjusted Annual Rate",
            IsEnabled = false, Category = (int)EconomicCategory.Housing
        },
        new()
        {
            SeriesId = "CSUSHPISA", Name = "Case-Shiller Home Price Index",
            Description = "S&P/Case-Shiller U.S. National Home Price Index",
            Frequency = "Monthly", Units = "Index Jan 2000=100", SeasonalAdjustment = "Seasonally Adjusted",
            IsEnabled = false, Category = (int)EconomicCategory.Housing
        },
        new()
        {
            SeriesId = "EXUSUK", Name = "USD/GBP Exchange Rate",
            Description = "U.S. / U.K. Foreign Exchange Rate",
            Frequency = "Monthly", Units = "U.S. Dollars to One British Pound", SeasonalAdjustment = "Not Seasonally Adjusted",
            IsEnabled = false, Category = (int)EconomicCategory.Market
        },
        new()
        {
            SeriesId = "WALCL", Name = "Fed Total Assets",
            Description = "Federal Reserve Total Assets",
            Frequency = "Weekly", Units = "Millions of Dollars", SeasonalAdjustment = "Not Seasonally Adjusted",
            IsEnabled = false, Category = (int)EconomicCategory.InterestRates
        },
        new()
        {
            SeriesId = "GS1", Name = "1-Year Treasury Yield",
            Description = "1-Year Treasury Constant Maturity Rate",
            Frequency = "Monthly", Units = "Percent", SeasonalAdjustment = "Not Seasonally Adjusted",
            IsEnabled = false, Category = (int)EconomicCategory.InterestRates
        },
        new()
        {
            SeriesId = "GS5", Name = "5-Year Treasury Yield",
            Description = "5-Year Treasury Constant Maturity Rate",
            Frequency = "Monthly", Units = "Percent", SeasonalAdjustment = "Not Seasonally Adjusted",
            IsEnabled = false, Category = (int)EconomicCategory.InterestRates
        }
    ];
}
