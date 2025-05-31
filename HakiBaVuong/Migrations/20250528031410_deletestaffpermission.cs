using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HakiBaVuong.Migrations
{
    /// <inheritdoc />
    public partial class deletestaffpermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffPermissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffPermissions",
                columns: table => new
                {
                    StaffId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffPermissions", x => new { x.StaffId, x.Role });
                    table.ForeignKey(
                        name: "FK_StaffPermissions_Users_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
