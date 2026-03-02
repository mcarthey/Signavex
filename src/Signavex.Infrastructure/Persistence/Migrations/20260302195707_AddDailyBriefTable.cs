using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Signavex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyBriefTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyBriefs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScanId = table.Column<string>(type: "TEXT", nullable: true),
                    EconomicHealthScore = table.Column<int>(type: "INTEGER", nullable: true),
                    MarketOutlook = table.Column<string>(type: "TEXT", nullable: true),
                    CandidateCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyBriefs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyBriefs_Date",
                table: "DailyBriefs",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyBriefs");
        }
    }
}
