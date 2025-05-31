using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HakiBaVuong.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundToBrands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackgroundColor",
                table: "Brands",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackgroundImageUrl",
                table: "Brands",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackgroundColor",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "BackgroundImageUrl",
                table: "Brands");
        }
    }
}
