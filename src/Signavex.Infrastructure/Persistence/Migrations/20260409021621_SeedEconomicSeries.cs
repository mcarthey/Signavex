using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Signavex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedEconomicSeries : Migration
    {
        // EconomicCategory enum: Employment=0, Inflation=1, Growth=2, InterestRates=3, Market=4, Housing=5, Consumer=6

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var columns = new[] { "SeriesId", "Name", "Description", "Frequency", "Units", "SeasonalAdjustment", "IsEnabled", "Category" };

            // Employment & Labor (enabled)
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "UNRATE", "Civilian Unemployment Rate", "Civilian Unemployment Rate", "Monthly", "Percent", "Seasonally Adjusted", true, 0 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "PAYEMS", "Total Nonfarm Payrolls", "All Employees: Total Nonfarm Payrolls, Thousands of Persons", "Monthly", "Thousands of Persons", "Seasonally Adjusted", true, 0 });

            // Inflation & Prices (enabled)
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "CPIAUCSL", "Consumer Price Index", "Consumer Price Index for All Urban Consumers: All Items in U.S. City Average", "Monthly", "Index 1982-1984=100", "Seasonally Adjusted", true, 1 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "PPIACO", "Producer Price Index", "Producer Price Index by Commodity: All Commodities", "Monthly", "Index 1982=100", "Not Seasonally Adjusted", true, 1 });

            // Economic Growth (enabled)
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "GDPC1", "Real GDP", "Real Gross Domestic Product, Billions of Chained 2017 Dollars", "Quarterly", "Billions of Chained 2017 Dollars", "Seasonally Adjusted Annual Rate", true, 2 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "INDPRO", "Industrial Production Index", "Industrial Production Index", "Monthly", "Index 2017=100", "Seasonally Adjusted", true, 2 });

            // Markets & Rates (enabled)
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "FEDFUNDS", "Federal Funds Rate", "Effective Federal Funds Rate", "Monthly", "Percent", "Not Seasonally Adjusted", true, 3 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "GS10", "10-Year Treasury Yield", "10-Year Treasury Constant Maturity Rate", "Monthly", "Percent", "Not Seasonally Adjusted", true, 3 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "SP500", "S&P 500", "S&P 500 Index", "Monthly", "Index", "Not Seasonally Adjusted", true, 4 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "RECPROUSM156N", "Recession Probability", "Smoothed U.S. Recession Probabilities", "Monthly", "Percent", "Not Seasonally Adjusted", true, 4 });

            // Housing (enabled)
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "MORTGAGE30US", "30-Year Mortgage Rate", "30-Year Fixed Rate Mortgage Average in the United States", "Weekly", "Percent", "Not Seasonally Adjusted", true, 5 });

            // Consumer (enabled)
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "PCE", "Personal Consumption Expenditures", "Personal Consumption Expenditures, Billions of Dollars", "Monthly", "Billions of Dollars", "Seasonally Adjusted Annual Rate", true, 6 });

            // Disabled series (from legacy EDT system)
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "DGS10", "10-Year Treasury Daily", "Market Yield on U.S. Treasury Securities at 10-Year Constant Maturity", "Daily", "Percent", "Not Seasonally Adjusted", false, 3 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "M2SL", "M2 Money Supply", "M2 Money Stock, Seasonally Adjusted", "Monthly", "Billions of Dollars", "Seasonally Adjusted", false, 2 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "UMCSENT", "Consumer Sentiment", "University of Michigan: Consumer Sentiment Index", "Monthly", "Index 1966:Q1=100", "Not Seasonally Adjusted", false, 6 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "HOUST", "Housing Starts", "Housing Starts: Total New Privately Owned Housing Units Started", "Monthly", "Thousands of Units", "Seasonally Adjusted Annual Rate", false, 5 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "CSUSHPISA", "Case-Shiller Home Price Index", "S&P/Case-Shiller U.S. National Home Price Index", "Monthly", "Index Jan 2000=100", "Seasonally Adjusted", false, 5 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "EXUSUK", "USD/GBP Exchange Rate", "U.S. / U.K. Foreign Exchange Rate", "Monthly", "U.S. Dollars to One British Pound", "Not Seasonally Adjusted", false, 4 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "WALCL", "Fed Total Assets", "Federal Reserve Total Assets", "Weekly", "Millions of Dollars", "Not Seasonally Adjusted", false, 3 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "GS1", "1-Year Treasury Yield", "1-Year Treasury Constant Maturity Rate", "Monthly", "Percent", "Not Seasonally Adjusted", false, 3 });
            migrationBuilder.InsertData(table: "EconomicSeries", columns: columns,
                values: new object[] { "GS5", "5-Year Treasury Yield", "5-Year Treasury Constant Maturity Rate", "Monthly", "Percent", "Not Seasonally Adjusted", false, 3 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var seriesIds = new[]
            {
                "UNRATE", "PAYEMS", "CPIAUCSL", "PPIACO", "GDPC1", "INDPRO",
                "FEDFUNDS", "GS10", "SP500", "RECPROUSM156N", "MORTGAGE30US", "PCE",
                "DGS10", "M2SL", "UMCSENT", "HOUST", "CSUSHPISA", "EXUSUK", "WALCL", "GS1", "GS5"
            };

            foreach (var id in seriesIds)
            {
                migrationBuilder.DeleteData(
                    table: "EconomicSeries",
                    keyColumn: "SeriesId",
                    keyValue: id);
            }
        }
    }
}
