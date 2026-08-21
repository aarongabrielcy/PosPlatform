using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    register_session_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    movement_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    created_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_movements_register_sessions_register_session_id",
                        column: x => x.register_session_id,
                        principalTable: "register_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_movements_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cash_movements_actor_user_id",
                table: "cash_movements",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_movements_created_at_utc_ticks",
                table: "cash_movements",
                column: "created_at_utc_ticks");

            migrationBuilder.CreateIndex(
                name: "IX_cash_movements_register_session_id",
                table: "cash_movements",
                column: "register_session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_movements");
        }
    }
}
