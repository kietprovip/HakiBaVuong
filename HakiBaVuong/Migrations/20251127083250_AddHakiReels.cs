using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HakiBaVuong.Migrations
{
    /// <inheritdoc />
    public partial class AddHakiReels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrandFollows",
                columns: table => new
                {
                    BrandId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Id = table.Column<int>(type: "int", nullable: false),
                    FollowedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandFollows", x => new { x.BrandId, x.CustomerId });
                });

            migrationBuilder.CreateTable(
                name: "ShortVideos",
                columns: table => new
                {
                    VideoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BrandId = table.Column<int>(type: "int", nullable: false),
                    VideoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Views = table.Column<int>(type: "int", nullable: false),
                    Likes = table.Column<int>(type: "int", nullable: false),
                    CommentsCount = table.Column<int>(type: "int", nullable: false),
                    Shares = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShortVideos", x => x.VideoId);
                    table.ForeignKey(
                        name: "FK_ShortVideos_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "BrandId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoComments",
                columns: table => new
                {
                    CommentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoComments", x => x.CommentId);
                    table.ForeignKey(
                        name: "FK_VideoComments_ShortVideos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "ShortVideos",
                        principalColumn: "VideoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoLikes",
                columns: table => new
                {
                    VideoId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Id = table.Column<int>(type: "int", nullable: false),
                    LikedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoLikes", x => new { x.VideoId, x.CustomerId });
                    table.ForeignKey(
                        name: "FK_VideoLikes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoLikes_ShortVideos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "ShortVideos",
                        principalColumn: "VideoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    X = table.Column<double>(type: "float", nullable: false),
                    Y = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId");
                    table.ForeignKey(
                        name: "FK_VideoProducts_ShortVideos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "ShortVideos",
                        principalColumn: "VideoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShortVideos_BrandId",
                table: "ShortVideos",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoComments_VideoId",
                table: "VideoComments",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoLikes_CustomerId",
                table: "VideoLikes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoProducts_ProductId",
                table: "VideoProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoProducts_VideoId",
                table: "VideoProducts",
                column: "VideoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrandFollows");

            migrationBuilder.DropTable(
                name: "VideoComments");

            migrationBuilder.DropTable(
                name: "VideoLikes");

            migrationBuilder.DropTable(
                name: "VideoProducts");

            migrationBuilder.DropTable(
                name: "ShortVideos");
        }
    }
}
