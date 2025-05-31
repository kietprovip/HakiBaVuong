using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HakiBaVuong.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePayos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentLink",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionId",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentLink",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "Payments");
        }
    }
}
