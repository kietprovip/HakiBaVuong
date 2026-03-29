# HakiBaVuong - Backend Ecommerce API (ASP.NET Core)

## Giới thiệu

`HakiBaVuong` là backend của hệ thống thương mại điện tử được xây dựng bằng:
- `ASP.NET Core` (Web API)
- `Entity Framework Core` (Code first)
- `C#`

Ứng dụng hỗ trợ:
- Quản lý sản phẩm / danh mục / thương hiệu
- Giỏ hàng, đơn hàng, thanh toán VNPay
- Quản lý người dùng, shippers, quyền hạn
- Lọc dữ liệu, thống kê doanh thu, đề xuất thông minh
- API chatbot, video ngắn, wishlist, sản phẩm đã xem


## Cấu trúc dự án

- `HakiBaVuong/Program.cs` : Thiết lập app và middleware
- `HakiBaVuong/Data` : `DataContext`, migration, seed
- `HakiBaVuong/Controllers` : API controller
- `HakiBaVuong/DTOs` : Data transfer object
- `HakiBaVuong/Models` : Entity model
- `HakiBaVuong/Mappers` : AutoMapper profile
- `Uploads/`, `trainmodel/` : File lưu trữ ảnh và dữ liệu gợi ý


## Yêu cầu môi trường

- .NET 7 hoặc .NET 8
- SQL Server (hoặc DB tương thích trong `appsettings.json`)
- Visual Studio / VS Code


## Cài đặt & chạy

1. Clone repo:
   ```bash
   git clone https://github.com/KingAzgad/HakiBaVuong.git
   cd HakiBaVuong
   ```
2. Restore dependencies:
   ```bash
   dotnet restore
   ```
3. Build:
   ```bash
   dotnet build
   ```
4. Cập nhật database (migration):
   ```bash
   dotnet ef database update --project HakiBaVuong\HakiBaVuong.csproj
   ```
5. Chạy ứng dụng:
   ```bash
   dotnet run --project HakiBaVuong\HakiBaVuong.csproj
   ```
6. Mở Swagger UI:
   - `http://localhost:5000/swagger`


## Cấu hình quan trọng

Sửa `appsettings.json` hoặc `appsettings.Development.json`:
- `ConnectionStrings:DefaultConnection`
- `JwtSettings` (SecretKey, ExpiresIn)
- `VNPay` (Url, MerchantCode, HashSecret)
- Thông tin email và logs nếu cần


## API chính

- `/api/Auth` : đăng ký, đăng nhập, đổi mật khẩu, OTP
- `/api/Product` : CRUD sản phẩm + tìm kiếm + lọc
- `/api/Category`, `/api/Brand` : quản lý danh mục/ thương hiệu
- `/api/Cart` : giỏ hàng
- `/api/Order` : tạo, cập nhật trạng thái, thanh toán
- `/api/Shipper` : quản lý shipper, xem đơn hàng
- `/api/Revenue` : báo cáo doanh thu
- `/api/Recommendation` : gợi ý sản phẩm

> Đầy đủ action trong từng controller, test nhanh bằng Swagger.


## Kiểm thử

- Dùng Postman / Swagger UI.
- Chuẩn bị dữ liệu seed ban đầu.
- Kiểm tra role `Admin`, `Customer`, `Shipper` để hợp lệ.


## Triển khai & mở rộng

- Thêm `Dockerfile` + `docker-compose.yml` (database + app).
- Thêm unit tests (xUnit), integration tests.
- Cache, pagination, tối ưu query, bảo mật OWASP.


## Đóng góp

1. Fork repo
2. Tạo branch: `feature/<tên>`
3. Commit + push
4. Tạo PR với mô tả chi tiết


## Liên kết
- Frontend: https://github.com/KingAzgad/doancosohutech.git


## License
- Thêm file `LICENSE` phù hợp (`MIT`, `Apache`, v.v.)

