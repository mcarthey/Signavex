using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Signavex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEconomicDataSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EconomicSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeriesId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Frequency = table.Column<string>(type: "TEXT", nullable: false),
                    Units = table.Column<string>(type: "TEXT", nullable: false),
                    SeasonalAdjustment = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicSeries", x => x.Id);
                    table.UniqueConstraint("AK_EconomicSeries_SeriesId", x => x.SeriesId);
                });

            migrationBuilder.CreateTable(
                name: "EconomicSyncTrackers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SeriesId = table.Column<string>(type: "TEXT", nullable: false),
                    LastSyncUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ObservationCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicSyncTrackers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EconomicObservations",
                columns: table => new
                {
                    SeriesId = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomicObservations", x => new { x.SeriesId, x.Date });
                    table.ForeignKey(
                        name: "FK_EconomicObservations_EconomicSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "EconomicSeries",
                        principalColumn: "SeriesId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EconomicObservations_SeriesId",
                table: "EconomicObservations",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_EconomicSeries_SeriesId",
                table: "EconomicSeries",
                column: "SeriesId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EconomicSyncTrackers_SeriesId",
                table: "EconomicSyncTrackers",
                column: "SeriesId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EconomicObservations");

            migrationBuilder.DropTable(
                name: "EconomicSyncTrackers");

            migrationBuilder.DropTable(
                name: "EconomicSeries");
        }
    }
}
