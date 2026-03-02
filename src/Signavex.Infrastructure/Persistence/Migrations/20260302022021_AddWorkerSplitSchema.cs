using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Signavex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerSplitSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CandidatesFound",
                table: "ScanCheckpoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CurrentTicker",
                table: "ScanCheckpoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ErrorCount",
                table: "ScanCheckpoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Evaluated",
                table: "ScanCheckpoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ScanCheckpoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Total",
                table: "ScanCheckpoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ScanCommands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommandType = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PickedUpAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanCommands", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScanCommands_RequestedAtUtc",
                table: "ScanCommands",
                column: "RequestedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScanCommands");

            migrationBuilder.DropColumn(
                name: "CandidatesFound",
                table: "ScanCheckpoints");

            migrationBuilder.DropColumn(
                name: "CurrentTicker",
                table: "ScanCheckpoints");

            migrationBuilder.DropColumn(
                name: "ErrorCount",
                table: "ScanCheckpoints");

            migrationBuilder.DropColumn(
                name: "Evaluated",
                table: "ScanCheckpoints");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ScanCheckpoints");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "ScanCheckpoints");
        }
    }
}
