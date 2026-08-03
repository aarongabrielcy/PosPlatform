using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    actor_username_snapshot = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    actor_display_name_snapshot = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    product_sku_snapshot = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    product_name_snapshot = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    action = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    occurred_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_audit_events_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_audit_events_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_audit_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_audit_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_audit_event_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    field_name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    old_value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    new_value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_audit_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_audit_changes_product_audit_events_product_audit_event_id",
                        column: x => x.product_audit_event_id,
                        principalTable: "product_audit_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_audit_changes_product_audit_event_id",
                table: "product_audit_changes",
                column: "product_audit_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_audit_events_action_occurred_at_utc_ticks",
                table: "product_audit_events",
                columns: new[] { "action", "occurred_at_utc_ticks" });

            migrationBuilder.CreateIndex(
                name: "IX_product_audit_events_actor_user_id_occurred_at_utc_ticks",
                table: "product_audit_events",
                columns: new[] { "actor_user_id", "occurred_at_utc_ticks" });

            migrationBuilder.CreateIndex(
                name: "IX_product_audit_events_organization_id_occurred_at_utc_ticks",
                table: "product_audit_events",
                columns: new[] { "organization_id", "occurred_at_utc_ticks" });

            migrationBuilder.CreateIndex(
                name: "IX_product_audit_events_product_id_occurred_at_utc_ticks",
                table: "product_audit_events",
                columns: new[] { "product_id", "occurred_at_utc_ticks" });

            migrationBuilder.Sql(@"
INSERT OR IGNORE INTO role_permissions (role_id, permission)
SELECT rp.role_id, 'ViewProductAudit'
FROM role_permissions rp
WHERE rp.permission IN (
        'ProcessSale',
        'ApplyDiscount',
        'CancelSale',
        'ProcessReturn',
        'OpenCashDrawer',
        'OpenRegisterSession',
        'CloseRegisterSession',
        'ViewCashTotals',
        'ManageProducts',
        'AdjustInventory',
        'ViewReports',
        'ManageUsers',
        'ManageRoles'
  )
GROUP BY rp.role_id
HAVING COUNT(DISTINCT rp.permission) = 13;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM role_permissions
WHERE permission = 'ViewProductAudit';
");

            migrationBuilder.DropTable(
                name: "product_audit_changes");

            migrationBuilder.DropTable(
                name: "product_audit_events");
        }
    }
}
