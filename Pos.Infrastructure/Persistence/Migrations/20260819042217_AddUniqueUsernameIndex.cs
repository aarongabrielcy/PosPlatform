using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueUsernameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_organization_id_username",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_users_organization_id_username",
                table: "users",
                columns: new[] { "organization_id", "username" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_organization_id_username",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_users_organization_id_username",
                table: "users",
                columns: new[] { "organization_id", "username" });
        }
    }
}
