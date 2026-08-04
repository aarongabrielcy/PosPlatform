using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministrativeNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "administrative_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_audit_event_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_administrative_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_administrative_notifications_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_administrative_notifications_product_audit_events_product_audit_event_id",
                        column: x => x.product_audit_event_id,
                        principalTable: "product_audit_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "administrative_notification_recipients",
                columns: table => new
                {
                    notification_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    read_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_administrative_notification_recipients", x => new { x.notification_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_administrative_notification_recipients_administrative_notifications_notification_id",
                        column: x => x.notification_id,
                        principalTable: "administrative_notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_administrative_notification_recipients_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_administrative_notification_recipients_user_id_read_at_utc_ticks",
                table: "administrative_notification_recipients",
                columns: new[] { "user_id", "read_at_utc_ticks" });

            migrationBuilder.CreateIndex(
                name: "IX_administrative_notifications_organization_id_created_at_utc_ticks",
                table: "administrative_notifications",
                columns: new[] { "organization_id", "created_at_utc_ticks" });

            migrationBuilder.CreateIndex(
                name: "IX_administrative_notifications_product_audit_event_id",
                table: "administrative_notifications",
                column: "product_audit_event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "administrative_notification_recipients");

            migrationBuilder.DropTable(
                name: "administrative_notifications");
        }
    }
}
