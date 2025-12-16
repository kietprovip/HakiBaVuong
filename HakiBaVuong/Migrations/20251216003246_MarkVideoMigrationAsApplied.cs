using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HakiBaVuong.Migrations
{
    /// <inheritdoc />
    public partial class MarkVideoMigrationAsApplied : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Id",
                table: "VideoLikes");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "VideoLikes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("Relational:ColumnOrder", 1);

            migrationBuilder.AlterColumn<int>(
                name: "VideoId",
                table: "VideoLikes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("Relational:ColumnOrder", 0);

            migrationBuilder.CreateTable(
                name: "VideoShares",
                columns: table => new
                {
                    VideoId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoShares", x => new { x.VideoId, x.CustomerId });
                    table.ForeignKey(
                        name: "FK_VideoShares_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoShares_ShortVideos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "ShortVideos",
                        principalColumn: "VideoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoViews",
                columns: table => new
                {
                    VideoId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoViews", x => new { x.VideoId, x.CustomerId });
                    table.ForeignKey(
                        name: "FK_VideoViews_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoViews_ShortVideos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "ShortVideos",
                        principalColumn: "VideoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoShares_CustomerId",
                table: "VideoShares",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoViews_CustomerId",
                table: "VideoViews",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoShares");

            migrationBuilder.DropTable(
                name: "VideoViews");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "VideoLikes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("Relational:ColumnOrder", 1);

            migrationBuilder.AlterColumn<int>(
                name: "VideoId",
                table: "VideoLikes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("Relational:ColumnOrder", 0);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "VideoLikes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
