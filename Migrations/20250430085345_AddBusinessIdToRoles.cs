using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InvoiceScanner.Migrations
{
    public partial class AddBusinessIdToRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BusinessId",
                table: "Roles",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemRole",
                table: "Roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BusinessRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    BusinessId = table.Column<long>(type: "bigint", nullable: false),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessRoles_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BusinessRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_BusinessId",
                table: "Roles",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRoles_BusinessId",
                table: "BusinessRoles",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessRoles_RoleId",
                table: "BusinessRoles",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Businesses_BusinessId",
                table: "Roles",
                column: "BusinessId",
                principalTable: "Businesses",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Businesses_BusinessId",
                table: "Roles");

            migrationBuilder.DropTable(
                name: "BusinessRoles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_BusinessId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "IsSystemRole",
                table: "Roles");
        }
    }
}
