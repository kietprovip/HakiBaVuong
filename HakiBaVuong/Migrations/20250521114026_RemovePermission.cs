using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HakiBaVuong.Migrations
{
    /// <inheritdoc />
    public partial class RemovePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StaffPermissions_Permissions_PermissionId",
                table: "StaffPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffPermissions_Permissions_PermissionId1",
                table: "StaffPermissions");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StaffPermissions",
                table: "StaffPermissions");

            migrationBuilder.DropIndex(
                name: "IX_StaffPermissions_PermissionId",
                table: "StaffPermissions");

            migrationBuilder.DropIndex(
                name: "IX_StaffPermissions_PermissionId1",
                table: "StaffPermissions");

            migrationBuilder.DropColumn(
                name: "PermissionId",
                table: "StaffPermissions");

            migrationBuilder.DropColumn(
                name: "PermissionId1",
                table: "StaffPermissions");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "StaffPermissions",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "")
                .Annotation("Relational:ColumnOrder", 2);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaffPermissions",
                table: "StaffPermissions",
                columns: new[] { "StaffId", "Role" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StaffPermissions",
                table: "StaffPermissions");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "StaffPermissions");

            migrationBuilder.AddColumn<int>(
                name: "PermissionId",
                table: "StaffPermissions",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("Relational:ColumnOrder", 2);

            migrationBuilder.AddColumn<int>(
                name: "PermissionId1",
                table: "StaffPermissions",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StaffPermissions",
                table: "StaffPermissions",
                columns: new[] { "StaffId", "PermissionId" });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.PermissionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffPermissions_PermissionId",
                table: "StaffPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPermissions_PermissionId1",
                table: "StaffPermissions",
                column: "PermissionId1");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffPermissions_Permissions_PermissionId",
                table: "StaffPermissions",
                column: "PermissionId",
                principalTable: "Permissions",
                principalColumn: "PermissionId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffPermissions_Permissions_PermissionId1",
                table: "StaffPermissions",
                column: "PermissionId1",
                principalTable: "Permissions",
                principalColumn: "PermissionId");
        }
    }
}
