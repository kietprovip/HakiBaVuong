# H??ng D?n API Quét Barcode & T?o S?n Ph?m (Scan & Create)

## Overview

Endpoint **`POST /api/product/scan-and-create`** cho phép FE Mobile quét mã barcode s?n ph?m, l?y thông tin t? BarcodeLookup API, t?i ?nh v? server, r?i l?u s?n ph?m vào database **1 l?n duy nh?t**. Backend x? lý toàn b? logic t? ??ng, FE ch? c?n g?i barcode và BrandId.

---

## Request

### Endpoint
```
POST /api/product/scan-and-create
```

### Headers
```
Content-Type: application/json
```

### Request Body (JSON)
```json
{
  "barcode": "4006381333931",
  "brandId": 3
}
```

### Parameters Gi?i Thích

| Field | Type | Required | Mô T? |
|-------|------|----------|-------|
| `barcode` | string | **Yes** | Mã barcode s?n ph?m (ví d? EAN-13, ISBN, UPC, v.v.) |
| `brandId` | integer | No (Optional) | ID c?a Brand/Th??ng hi?u mà s?n ph?m s? ???c gán. N?u không g?i, backend s? dùng brand ??u tiên ho?c t?o m?i. |

---

## Response

### Success Response (201 Created)
```json
{
  "message": "?ã thêm s?n ph?m t? barcode.",
  "product": {
    "productId": 1018,
    "brandId": 3,
    "name": "Giá ?? ?i?n tho?i",
    "description": "Danh m?c: Car Phone Holder Mounts\n\nTh??ng hi?u: HOCO\n\nUniversal gravity car phone holder, suitable...",
    "priceSell": 99999999,
    "priceCost": 99999999,
    "rating": 5.0,
    "createdAt": "2025-12-15T19:15:39Z",
    "updatedAt": "2025-12-15T19:15:39Z",
    "image": "/Images/barcode_20251215191539_88705d69-c125-44d1-bc63-3baad4dc1f19.jpg",
    "categoryIds": []
  }
}
```

### Error Response (400 Bad Request)
```json
{
  "message": "Vui lòng cung c?p barcode trong body (Barcode property)."
}
```

### Error Response (400 Bad Request - BrandId không t?n t?i)
```json
{
  "message": "BrandId ???c cung c?p không t?n t?i."
}
```

### Error Response (404 Not Found - Barcode không tìm th?y)
```json
{
  "message": "Không tìm th?y thông tin s?n ph?m!"
}
```

### Error Response (500 Internal Server Error)
```json
{
  "message": "L?i khi import s?n ph?m t? barcode."
}
```

---

## Logic Th?c Hi?n (Backend)

### Step-by-Step Flow

1. **Validate Input**
   - Ki?m tra `barcode` có r?ng không. N?u r?ng tr? 400.

2. **G?i BarcodeLookup API**
   - Backend g?i request ??n `https://api.barcodelookup.com/v3/products?barcode={barcode}&key=...`
   - L?y JSON response ch?a array `products`.
   - N?u API tr? error ho?c không tìm th?y s?n ph?m ? tr? 404.

3. **Trích Xu?t D? Li?u t? Barcode API**
   - `title` ? l?u vào `Product.Name`
   - `category` + `brand/manufacturer` + `description` ? ghép thành `Product.Description`
   - `images[0]` ? l?y URL ?nh

4. **Xác ??nh Brand**
   - N?u FE g?i `brandId`:
     - Tìm Brand trong DB theo `BrandId`.
     - N?u không t?n t?i ? tr? 400 "BrandId ???c cung c?p không t?n t?i."
   - N?u FE **không** g?i `brandId`:
     - Tìm Brand ??u tiên trong DB.
     - N?u không có Brand ? t?o placeholder `User` và `Brand` m?i.

5. **T?i ?nh (Optional)**
   - N?u Barcode API tr? URL ?nh:
     - Backend t?i ?nh v? th? m?c `Images` trên server.
     - ??t tên file: `barcode_{timestamp}_{guid}.{ext}`
     - L?u ???ng d?n vào `Product.Image` (ví d?: `/Images/barcode_20251215191539_xxx.jpg`)
   - N?u t?i ?nh th?t b?i ? v?n ti?p t?c t?o s?n ph?m, ch? `Image` field s? r?ng.

6. **T?o Product**
   - L?u Product vào DB v?i:
     - `BrandId`: t? brand ?ã xác ??nh ? Step 4
     - `Name`: t? barcode API `title`
     - `Description`: t? category + brand + description
     - `PriceSell`: `99999999` (giá t?m, FE/Admin có th? edit sau)
     - `PriceCost`: `99999999` (giá t?m)
     - `Image`: ???ng d?n ?nh ?ã t?i (ho?c r?ng)
     - `Rating`: `5.0` (m?c ??nh)
     - `CreatedAt`, `UpdatedAt`: timestamp hi?n t?i

7. **T?o Inventory**
   - T? ??ng t?o b?n ghi Inventory cho s?n ph?m v?a t?o:
     - `ProductId`: ID s?n ph?m v?a t?o
     - `StockQuantity`: 0 (kho tr?ng, staff/owner s? c?p nh?t sau)
     - `LastUpdated`: timestamp

8. **Response Success**
   - Tr? HTTP 201 (Created)
   - Body ch?a `message` + full `product` DTO

---

## HTTP Status Codes

| Code | Meaning | Khi Nào |
|------|---------|---------|
| **201** | Created | S?n ph?m ?ã ???c t?o thành công |
| **400** | Bad Request | Barcode r?ng, ho?c BrandId không t?n t?i |
| **404** | Not Found | Barcode không tìm th?y trong BarcodeLookup API |
| **500** | Internal Server Error | L?i không xác ??nh (database, network, etc.) |

---

## Cách FE Mobile S? D?ng

### Ví d? Dart / Flutter

```dart
import 'package:http/http.dart' as http;
import 'dart:convert';

Future<Map<String, dynamic>> scanAndCreateProduct({
  required String barcode,
  int? brandId,
}) async {
  final url = Uri.parse('https://your-api-domain.com/api/product/scan-and-create');
  
  final body = {
    'barcode': barcode,
    if (brandId != null) 'brandId': brandId,
  };
  
  try {
    final response = await http.post(
      url,
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode(body),
    );
    
    if (response.statusCode == 201) {
      final data = jsonDecode(response.body);
      print('Success: ${data['message']}');
      print('Product ID: ${data['product']['productId']}');
      return data;
    } else {
      final error = jsonDecode(response.body);
      throw Exception('Error: ${error['message']}');
    }
  } catch (e) {
    print('Exception: $e');
    rethrow;
  }
}

// S? d?ng:
// scanAndCreateProduct(barcode: "4006381333931", brandId: 3);
```

### Ví d? Kotlin / Android

```kotlin
import okhttp3.OkHttpClient
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Request
import org.json.JSONObject

fun scanAndCreateProduct(barcode: String, brandId: Int? = null) {
    val client = OkHttpClient()
    val url = "https://your-api-domain.com/api/product/scan-and-create"
    
    val jsonBody = JSONObject().apply {
        put("barcode", barcode)
        if (brandId != null) put("brandId", brandId)
    }
    
    val request = Request.Builder()
        .url(url)
        .post(jsonBody.toString().toRequestBody("application/json".toMediaType()))
        .build()
    
    client.newCall(request).execute().use { response ->
        if (response.code == 201) {
            val body = JSONObject(response.body?.string() ?: "")
            println("Success: ${body.getString("message")}")
            println("Product ID: ${body.getJSONObject("product").getInt("productId")}")
        } else {
            val error = JSONObject(response.body?.string() ?: "")
            println("Error: ${error.getString("message")}")
        }
    }
}

// S? d?ng:
// scanAndCreateProduct("4006381333931", 3)
```

---

## L?u Ý Quan Tr?ng cho FE Mobile

### 1. **Authorization Không B?t Bu?c** ??
   - Endpoint này **m?** (no `[Authorize]` attribute).
   - B?t k? ai c?ng có th? g?i endpoint này.
   - **T??ng Lai**: Nên thêm `[Authorize(Roles = "User")]` ?? ch? Brand/Admin có quy?n quét barcode.

### 2. **BrandId là Tùy Ch?n**
   - N?u FE g?i `brandId`: s?n ph?m s? ???c gán vào brand ?ó.
   - N?u FE không g?i `brandId`: backend s? tìm brand có s?n ho?c t?o m?i.
   - **Khuy?n cáo**: Luôn g?i `brandId` ?? ??m b?o s?n ph?m ???c gán ?úng brand.

### 3. **Giá C? T?m Th?i**
   - S?n ph?m ???c t?o v?i `PriceSell` = `PriceCost` = `99999999` (t?m).
   - FE/Admin ph?i **edit s?n ph?m** sau ?? ??t giá bán th?c t?.
   - Ho?c Backend có th? thêm endpoint `PUT /api/product/{id}/update-price` ?? c?p nh?t giá sau.

### 4. **Kho Hàng (Inventory) Ban ??u = 0**
   - S?n ph?m m?i có `StockQuantity = 0`.
   - Staff/Owner ph?i c?p nh?t kho sau b?ng endpoint `PUT /api/brands/{brandId}/inventory/{productId}`.

### 5. **?nh**
   - ?nh ???c t?i v? t? BarcodeLookup API và l?u trên server.
   - FE có th? l?y ?nh t? tr??ng `product.image` trong response.
   - N?u barcode không có ?nh ? `image` field s? r?ng.

### 6. **Response Format**
   - HTTP 201 v?i body có `message` + `product`.
   - S? d?ng `product.productId` ?? bi?t ID s?n ph?m v?a t?o (n?u c?n hi?n th? chi ti?t sau).

---

## Ví D? Test trong Postman

### Setup Request

**Method**: `POST`  
**URL**: `https://localhost:7258/api/product/scan-and-create` (ho?c domain th?c)

**Headers**:
```
Content-Type: application/json
```

**Body** (raw JSON):
```json
{
  "barcode": "4006381333931",
  "brandId": 3
}
```

### Nh?n Send

Backend s?:
1. G?i BarcodeLookup API v?i barcode `4006381333931`
2. L?y thông tin s?n ph?m (title, brand, description, ?nh)
3. T?i ?nh v? th? m?c `Images` trên server
4. T?o `Product` record trong DB (gán vào Brand ID = 3)
5. T?o `Inventory` record (stock = 0)
6. Tr? response 201 v?i thông tin s?n ph?m v?a t?o

---

## Troubleshooting

### ? "Vui lòng cung c?p barcode..."
- **Nguyên nhân**: Field `barcode` r?ng ho?c không g?i.
- **Fix**: Ki?m tra FE có g?i ?úng `barcode` trong body không.

### ? "BrandId ???c cung c?p không t?n t?i."
- **Nguyên nhân**: `brandId` trong request không t?n t?i trong DB.
- **Fix**: G?i `brandId` h?p l? ho?c không g?i `brandId` ?? dùng default.

### ? "Không tìm th?y thông tin s?n ph?m!"
- **Nguyên nhân**: Barcode không t?n t?i trong BarcodeLookup API.
- **Fix**: Ki?m tra barcode có h?p l? không. Th? test manual qua: `https://api.barcodelookup.com/v3/products?barcode=xxx&formatted=y&key=...`

### ? "L?i khi import s?n ph?m t? barcode."
- **Nguyên nhân**: L?i database ho?c network không xác ??nh.
- **Fix**: Ki?m tra server logs ?? bi?t chi ti?t error. Contact backend developer.

---

## T??ng Lai (Improvements)

1. **Thêm Authorization**: Ch? cho phép Brand/Admin token g?i endpoint này.
2. **API Key Management**: L?u Barcode API key trong Secret Manager thay vì appsettings.json.
3. **Bulk Import**: Support quét nhi?u barcode cùng lúc.
4. **Price Mapping**: FE có th? g?i thêm `desiredPrice` ?? backend set giá th?c t? ngay (không c?n edit sau).
5. **Category Auto-Mapping**: Backend t? ??ng gán s?n ph?m vào Category d?a trên Barcode API `category` field.

---

## Contact & Support

- **Backend Team**: Liên h? n?u có l?i ho?c mu?n customize endpoint.
- **API Documentation**: Tham kh?o thêm t?i `/api/swagger` (Swagger UI).
