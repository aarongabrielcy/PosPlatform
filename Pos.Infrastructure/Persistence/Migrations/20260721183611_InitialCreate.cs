using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.id);
                    table.ForeignKey(
                        name: "FK_branches_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    sku = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    barcode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    sale_price_amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    sale_price_currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    cost_amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    cost_currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    tracks_inventory = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_roles_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    branch_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registers", x => x.id);
                    table.ForeignKey(
                        name: "FK_registers_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    branch_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    reorder_point = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    created_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_items_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    permission = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission });
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    role_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    username = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_users_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "register_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    register_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    opened_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    closed_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    opening_float_amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    opening_float_currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    expected_cash_amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    expected_cash_currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    counted_cash_amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    counted_cash_currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    cash_difference_amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    cash_difference_currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    opened_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false),
                    closed_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_register_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_register_sessions_registers_register_id",
                        column: x => x.register_id,
                        principalTable: "registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_register_sessions_users_closed_by_user_id",
                        column: x => x.closed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_register_sessions_users_opened_by_user_id",
                        column: x => x.opened_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    branch_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    register_session_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    sale_status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    created_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false),
                    completed_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_register_sessions_register_session_id",
                        column: x => x.register_session_id,
                        principalTable: "register_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    sale_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    payment_method = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    paid_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_payments_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sale_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    sale_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_sku = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    product_name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    unit_price_amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_sale_lines_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_lines_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    inventory_item_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    branch_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    product_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    performed_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    movement_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    quantity_before = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    quantity_after = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    sale_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    sale_line_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    occurred_at_utc_ticks = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_movements_inventory_items_inventory_item_id",
                        column: x => x.inventory_item_id,
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_sale_lines_sale_line_id",
                        column: x => x.sale_line_id,
                        principalTable: "sale_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_branches_is_active",
                table: "branches",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_branches_organization_id_name",
                table: "branches",
                columns: new[] { "organization_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_branch_id_product_id",
                table: "inventory_items",
                columns: new[] { "branch_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_product_id",
                table: "inventory_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_branch_id_product_id",
                table: "inventory_movements",
                columns: new[] { "branch_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_inventory_item_id",
                table: "inventory_movements",
                column: "inventory_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_occurred_at_utc_ticks",
                table: "inventory_movements",
                column: "occurred_at_utc_ticks");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_sale_id",
                table: "inventory_movements",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_sale_line_id",
                table: "inventory_movements",
                column: "sale_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_is_active",
                table: "organizations",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_name",
                table: "organizations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_payments_paid_at_utc_ticks",
                table: "payments",
                column: "paid_at_utc_ticks");

            migrationBuilder.CreateIndex(
                name: "IX_payments_payment_method",
                table: "payments",
                column: "payment_method");

            migrationBuilder.CreateIndex(
                name: "IX_payments_sale_id",
                table: "payments",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_is_active",
                table: "products",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_products_name",
                table: "products",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_products_organization_id_barcode",
                table: "products",
                columns: new[] { "organization_id", "barcode" });

            migrationBuilder.CreateIndex(
                name: "IX_products_organization_id_sku",
                table: "products",
                columns: new[] { "organization_id", "sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_register_sessions_closed_at_utc_ticks",
                table: "register_sessions",
                column: "closed_at_utc_ticks");

            migrationBuilder.CreateIndex(
                name: "IX_register_sessions_closed_by_user_id",
                table: "register_sessions",
                column: "closed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_register_sessions_opened_at_utc_ticks",
                table: "register_sessions",
                column: "opened_at_utc_ticks");

            migrationBuilder.CreateIndex(
                name: "IX_register_sessions_opened_by_user_id",
                table: "register_sessions",
                column: "opened_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_register_sessions_register_id",
                table: "register_sessions",
                column: "register_id");

            migrationBuilder.CreateIndex(
                name: "IX_register_sessions_status",
                table: "register_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_registers_branch_id_name",
                table: "registers",
                columns: new[] { "branch_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_registers_is_active",
                table: "registers",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_roles_is_active",
                table: "roles",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_roles_organization_id",
                table: "roles",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_sale_lines_product_id",
                table: "sale_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_sale_lines_sale_id",
                table: "sale_lines",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "IX_sale_lines_sale_id_product_id",
                table: "sale_lines",
                columns: new[] { "sale_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_branch_id",
                table: "sales",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_created_at_utc_ticks",
                table: "sales",
                column: "created_at_utc_ticks");

            migrationBuilder.CreateIndex(
                name: "IX_sales_created_by_user_id",
                table: "sales",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_organization_id",
                table: "sales",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_register_session_id",
                table: "sales",
                column: "register_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_sale_status",
                table: "sales",
                column: "sale_status");

            migrationBuilder.CreateIndex(
                name: "IX_users_is_active",
                table: "users",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_users_organization_id_username",
                table: "users",
                columns: new[] { "organization_id", "username" });

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                table: "users",
                column: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_movements");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "inventory_items");

            migrationBuilder.DropTable(
                name: "sale_lines");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "sales");

            migrationBuilder.DropTable(
                name: "register_sessions");

            migrationBuilder.DropTable(
                name: "registers");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
