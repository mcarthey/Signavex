using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Signavex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFundamentalsCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FundamentalsCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ticker = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PeRatio = table.Column<double>(type: "float", nullable: true),
                    IndustryPeRatio = table.Column<double>(type: "float", nullable: true),
                    DebtToEquityRatio = table.Column<double>(type: "float", nullable: true),
                    EpsCurrentQuarter = table.Column<double>(type: "float", nullable: true),
                    EpsEstimateCurrentQuarter = table.Column<double>(type: "float", nullable: true),
                    EpsPreviousYear = table.Column<double>(type: "float", nullable: true),
                    AnalystRating = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetrievedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundamentalsCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FundamentalsCache_RetrievedAtUtc",
                table: "FundamentalsCache",
                column: "RetrievedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_FundamentalsCache_Ticker",
                table: "FundamentalsCache",
                column: "Ticker",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FundamentalsCache");
        }
    }
}
