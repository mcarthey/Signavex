using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Signavex.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedIdentityRoles : Migration
    {
        private static readonly string FreeRoleId = "a1b2c3d4-free-role-0001-000000000001";
        private static readonly string ProRoleId = "a1b2c3d4-pro0-role-0002-000000000002";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
                values: new object[] { FreeRoleId, "Free", "FREE", Guid.NewGuid().ToString() });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
                values: new object[] { ProRoleId, "Pro", "PRO", Guid.NewGuid().ToString() });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: FreeRoleId);

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: ProRoleId);
        }
    }
}
