using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HakiBaVuong.Migrations
{
    /// <inheritdoc />
    public partial class Addrevenue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RevenueHistories",
                columns: table => new
                {
                    RevenueHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrandId = table.Column<int>(type: "int", nullable: false),
                    DailyRevenue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MonthlyRevenue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    YearlyRevenue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodType = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueHistories", x => x.RevenueHistoryId);
                    table.ForeignKey(
                        name: "FK_RevenueHistories_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "BrandId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Revenues",
                columns: table => new
                {
                    RevenueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    DailyRevenue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MonthlyRevenue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    YearlyRevenue = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Revenues", x => x.RevenueId);
                    table.ForeignKey(
                        name: "FK_Revenues_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RevenueHistories_BrandId",
                table: "RevenueHistories",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Revenues_OrderId",
                table: "Revenues",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RevenueHistories");

            migrationBuilder.DropTable(
                name: "Revenues");
        }
    }
}
