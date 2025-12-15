Tóm t?t ng?n

?ây là h??ng d?n logic + code m?u + endpoint ?? FE mobile tri?n khai các ch?c n?ng liên quan t?i "Brand (User)" (qu?n lý th??ng hi?u, s?n ph?m, kho). L?u ý phân bi?t rõ 2 khái ni?m trong DB:
- `User.BrandId` (trong b?ng `User`) ch? dùng ?? ?ánh d?u r?ng m?t `User` là nhân viên/c?ng tác c?a m?t brand. Không dùng `User.BrandId` ?? xác ??nh ng??i làm ch? brand.
- `Brand.OwnerId` (trong b?ng `Brand`) là cách chính th?c ?? bi?t `User` nào là ch? s? h?u c?a brand.
(?ã thêm comment `// NOTE: ...` vào `Models/User.cs` và `Models/Brand.cs` ?? tránh nh?m l?n trong t??ng lai.)

Yêu c?u chính cho FE mobile

- FE ph?i dùng token JWT c?a "Brand (User)" cho t?t c? endpoint brand-related (KHÔNG dùng token Customer).
- Token ph?i ???c g?i trong header: `Authorization: Bearer {token}`.
- Trên token c?n có ít nh?t các claim: `UserId`, `Role`. T?t nh?t thêm `BrandId` (n?u user là staff) và `BrandOwnerId` (n?u token ???c c?p cho owner ho?c backend resolved tr??c khi tr? v? token).
- FE nên ki?m tra claim `Role` trên token: n?u `Role == "Customer"` thì không g?i endpoint brand.

C?u trúc claims JWT g?i ý

- `UserId` (int)
- `Role` (string) — giá tr? có th? là `Customer` ho?c `User` (User = Brand/Admin)
- `BrandId` (int?) — ch? khi user là nhân viên, ch?a BrandId mà user thu?c v?
- `BrandOwnerId` (int?) — n?u token là c?a owner thì BrandOwnerId = UserId; n?u token là nhân viên, backend nên resolve và thêm BrandOwnerId (owner c?a BrandId ?ó) ?? FE d? hi?u quy?n s? h?u

L?u ý phân quy?n (backend)

- Owner-only actions: ch? cho phép khi `UserId == Brand.OwnerId`.
- Staff actions: cho phép khi `User.BrandId == Brand.BrandId` (User.BrandId nullable) ho?c khi user là owner.
- Tránh dùng `User.BrandId` nh? b?ng ch?ng user "là" brand owner.

Endpoints (g?i ý, RESTful)

Authentication (existing)
- POST `/api/auth/login` -> tr? v? `{ token, expiresIn }`. Token ch?a claims nh? trên.

Brand management
- GET `/api/brands/{brandId}` — L?y chi ti?t brand. [Authorize(Roles = "User")]
- POST `/api/brands` — T?o brand (Owner t?o). [Authorize(Roles = "User")] (owner s? tr? thành Brand.OwnerId)
- PUT `/api/brands/{brandId}` — C?p nh?t brand. Owner-only.
- DELETE `/api/brands/{brandId}` — Xóa brand. Owner-only.

Product management (thu?c brand)
- GET `/api/brands/{brandId}/products` — Danh sách s?n ph?m c?a brand. [Authorize(Roles = "User")]
- POST `/api/brands/{brandId}/products` — T?o s?n ph?m (owner ho?c staff). [Authorize(Roles = "User")] + ki?m tra brand h?p l?
- PUT `/api/brands/{brandId}/products/{productId}` — C?p nh?t s?n ph?m (owner ho?c staff)
- DELETE `/api/brands/{brandId}/products/{productId}` — Xoá (owner only or owner/staff depending business rule)

Inventory management
- GET `/api/brands/{brandId}/inventory` — L?y t?t c? inventory c?a brand
- PUT `/api/brands/{brandId}/inventory/{productId}` — C?p nh?t t?n kho (owner ho?c staff)

Các response l?i chu?n (dùng cho FE x? lý)
- 401 Unauthorized: token không h?p l? / không g?i token
- 403 Forbidden: token h?p l? nh?ng không có quy?n (ví d? token là Customer ho?c user không ph?i owner/staff)
- 404 NotFound: brand/product không t?n t?i ho?c không thu?c quy?n s? h?u

Ví d? middleware / helper ki?m tra quy?n (C#) — pattern g?i ý

S? d?ng policies + helper trong controller. Ví d? helper method dùng trong controller:

`private bool IsOwnerOrStaff(HttpContext httpContext, Brand brand, out int userId)`
- L?y `userId` t? claim `UserId`.
- L?y `role` t? claim `Role`.
- N?u `role == "Customer"` => false.
- N?u `brand.OwnerId == userId` => true.
- N?u user có claim `BrandId` != null && claim.BrandId == brand.BrandId => true (staff).
- Ng??c l?i false.

Code ví d? controller (rút g?n)

- `BrandController` (ch? demo các action chính)

`[ApiController]
[Route("api/brands")]
public class BrandController : ControllerBase
{
    private readonly AppDbContext _db;
    public BrandController(AppDbContext db) { _db = db; }

    [HttpGet("{id}")]
    [Authorize(Roles = "User")]
    public IActionResult GetBrand(int id)
    {
        var brand = _db.Brands.Find(id);
        if (brand == null) return NotFound();
        return Ok(brand);
    }

    [HttpPost]
    [Authorize(Roles = "User")]
    public IActionResult CreateBrand([FromBody] Brand model)
    {
        // L?y userId t? token
        var userId = int.Parse(User.FindFirst("UserId").Value);
        model.OwnerId = userId; // ch? brand là user t?o
        _db.Brands.Add(model);
        _db.SaveChanges();
        return CreatedAtAction(nameof(GetBrand), new { id = model.BrandId }, model);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "User")]
    public IActionResult UpdateBrand(int id, [FromBody] Brand update)
    {
        var brand = _db.Brands.Find(id);
        if (brand == null) return NotFound();
        // ki?m tra owner
        var userId = int.Parse(User.FindFirst("UserId").Value);
        if (brand.OwnerId != userId) return Forbid();

        brand.Name = update.Name;
        brand.BackgroundColor = update.BackgroundColor;
        brand.BackgroundImageUrl = update.BackgroundImageUrl;
        _db.SaveChanges();
        return NoContent();
    }
}
`

- `ProductsController` (rút g?n)

`[ApiController]
[Route("api/brands/{brandId}/products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) { _db = db; }

    private bool IsOwnerOrStaff(int brandId)
    {
        var userId = int.Parse(User.FindFirst("UserId").Value);
        var role = User.FindFirst("Role")?.Value;
        if (role == "Customer") return false;

        var brand = _db.Brands.Find(brandId);
        if (brand == null) return false;
        if (brand.OwnerId == userId) return true;

        var claimBrandId = User.FindFirst("BrandId")?.Value;
        if (!string.IsNullOrEmpty(claimBrandId) && int.Parse(claimBrandId) == brandId) return true;

        return false;
    }

    [HttpPost]
    [Authorize(Roles = "User")]
    public IActionResult CreateProduct(int brandId, [FromBody] Product p)
    {
        if (!IsOwnerOrStaff(brandId)) return Forbid();
        p.BrandId = brandId;
        _db.Products.Add(p);
        _db.SaveChanges();
        return CreatedAtAction("GetProduct", new { brandId = brandId, productId = p.ProductId }, p);
    }
}
`

Inventory update example

`[HttpPut("{brandId}/inventory/{productId}")]
[Authorize(Roles = "User")]
public IActionResult UpdateInventory(int brandId, int productId, [FromBody] InventoryUpdateDto dto)
{
    if (!IsOwnerOrStaff(brandId)) return Forbid();
    var inv = _db.Inventories.SingleOrDefault(i => i.ProductId == productId && i.BrandId == brandId);
    if (inv == null) return NotFound();
    inv.Quantity = dto.Quantity;
    _db.SaveChanges();
    return NoContent();
}
`

Cách backend t?o token (AuthService) — g?i ý

- Khi user login, backend xác ??nh role và xác ??nh là owner hay staff:
  - N?u user.Role == "User" và user.BrandId == null: ?ây có th? là owner (t?t nh?t check Brand table xem brand.OwnerId == user.UserId)
  - N?u user.Role == "User" và user.BrandId != null: ?ây là staff; resolve brand = db.Brands.Find(user.BrandId); brandOwnerId = brand.OwnerId
- Thêm các claim phù h?p: `UserId`, `Role`, `BrandId` (n?u staff), `BrandOwnerId` (n?u resolved)

Ví d? code t?o claims (rút g?n):

`var claims = new List<Claim>
{
    new Claim("UserId", user.UserId.ToString()),
    new Claim(ClaimTypes.Role, user.Role),
};
if (user.BrandId.HasValue)
    claims.Add(new Claim("BrandId", user.BrandId.Value.ToString()));
// n?u có owner resolved
claims.Add(new Claim("BrandOwnerId", resolvedOwnerId.ToString()));
`

FE mobile: lu?ng ?? ngh?

1. Ng??i dùng brand ??ng nh?p vào màn hình Brand Login -> g?i `/api/auth/login`.
2. L?u token an toàn (Android Keystore / iOS Keychain).
3. Khi truy c?p màn Brand Management / Product / Inventory, g?i header `Authorization: Bearer {token}`.
4. FE ki?m tra claim `Role` trên token (ho?c backend tr? 403) — n?u `Role == "Customer"` thì hi?n th? thông báo không ???c phép.
5. ??i v?i các ch?c n?ng owner-only (ví d? xóa brand, c?p nh?t c?u hình tr? phí...), FE có th? g?i endpoint và x? lý 403 n?u không ?? quy?n.

M?t vài l?u ý implement

- Luôn ki?m tra `BrandId` path param kh?p v?i brand trong DB và quy?n user.
- Tr??ng h?p user là staff (`User.BrandId` có giá tr?) và mu?n hành ??ng v?i brand khác => tr? 403.
- ??i v?i staff, backend nên resolve và tr? `BrandOwnerId` khi login ?? FE có th? hi?n th? thông tin "B?n thu?c Brand: X (owner: Y)".
- N?u FE c?n phân bi?t UI gi?a Owner và Staff: backend nên tr? claim `IsOwner=true` ho?c `RoleDetail=Owner/Staff` trong payload token ho?c riêng endpoint `/api/auth/me` tr? JSON chi ti?t user+brand info.

Ví d? tr? `me` endpoint (FE dùng ?? hi?n UI)

GET `/api/auth/me` -> tr?
{
  "userId": 123,
  "role": "User",
  "isOwner": true,
  "brandId": 5, // n?u staff
  "brandOwnerId": 5 // owner id
}

K?t lu?n ng?n

- FE mobile luôn dùng token c?a "User" (brand) cho m?i thao tác brand.
- Backend ph?i ki?m tra claim `Role` và so kh?p `UserId` vs `Brand.OwnerId` ho?c `User.BrandId` vs `Brand.BrandId`.
- ?ã thêm comment vào `Models/User.cs` và `Models/Brand.cs` ?? nh?c r?ng `User.BrandId` khác `Brand.OwnerId`.

N?u mu?n tôi có th?:
- B? sung file code ??y ?? `BrandController`, `ProductsController`, `InventoryController` (v?i DI, DTOs) theo project hi?n t?i.
- Thêm policy-based `AuthorizationHandler` ?? th?ng nh?t ki?m tra owner/staff.

C?n ti?p theo (cho tôi bi?t):
- Mu?n code chi ti?t controller/service nào tr??c? (Brand / Product / Inventory / Auth me endpoint / Policy handler)

