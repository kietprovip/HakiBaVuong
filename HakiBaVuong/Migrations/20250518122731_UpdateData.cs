using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HakiBaVuong.Migrations
{
    /// <inheritdoc />
    public partial class UpdateData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
            table: "Permissions",
            columns: new[] { "Name", "Description" },
            values: new object[,]
            {
                { "AddProduct", "Thêm sản phẩm mới vào brand" },
                { "ManageOrders", "Quản lý đơn hàng của brand" },
                { "UpdateInventory", "Cập nhật số lượng tồn kho" },
                { "UploadProductImage", "Tải ảnh lên cho sản phẩm" },
                { "ManageStaff", "Quản lý nhân viên trong brand" },
                { "ManageBrand", "Quản lý thông tin brand" }
        });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
            table: "Permissions",
            keyColumn: "Name",
            keyValues: new object[] { "AddProduct", "ManageOrders", "UpdateInventory", "UploadProductImage", "ManageStaff", "ManageBrand" });
        }
    }
}
