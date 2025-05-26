using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HakiBaVuong.Migrations
{
    /// <inheritdoc />
    public partial class View : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId1",
                table: "Comments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ViewedProducts",
                columns: table => new
                {
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ViewedProductId = table.Column<int>(type: "int", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewedProducts", x => new { x.CustomerId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_ViewedProducts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ViewedProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_CustomerId1",
                table: "Comments",
                column: "CustomerId1");

            migrationBuilder.CreateIndex(
                name: "IX_ViewedProducts_ProductId",
                table: "ViewedProducts",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Customers_CustomerId1",
                table: "Comments",
                column: "CustomerId1",
                principalTable: "Customers",
                principalColumn: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Customers_CustomerId1",
                table: "Comments");

            migrationBuilder.DropTable(
                name: "ViewedProducts");

            migrationBuilder.DropIndex(
                name: "IX_Comments_CustomerId1",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "CustomerId1",
                table: "Comments");
        }
    }
}
