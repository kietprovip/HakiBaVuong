# Shipper API Endpoints Summary

## Base URL
```
http://localhost:7258/api/shipper
```

## Authentication
- **Type**: Bearer Token (JWT)
- **Role**: Shipper
- **Header Example**: `Authorization: Bearer eyJhbGciOiJIUzI1NiIs...`

---

## Endpoints Overview

### 1?? Profile Management (Không ch?nh s?a)

#### Get Profile
```http
GET /profile
```
- L?y thông tin cá nhân c?a shipper hi?n t?i
- Response: ShipperDTO (ID, Name, Email, Phone, VehicleType, LicensePlate, Rating)

#### Update Profile
```http
PUT /profile
Body: ShipperUpdateDTO
```
- C?p nh?t thông tin cá nhân (Name, Phone, VehicleType, LicensePlate)
- Response: { message: "C?p nh?t h? s? thành công!" }

---

### 2?? Order Management for Shipper (CÓ CH?NH S?A)

#### **Tab 1: Danh Sách ??n Ch? Giao**
```http
GET /pending-orders
```
- L?y danh sách ??n hàng ch? shipper nh?n
- Filter: DeliveryStatus = "??i giao hàng" AND ShipperId = null
- Max: 20 ??n, s?p x?p theo CreatedAt
- Response: Array<OrderDTO>

**Thay ??i so v?i c?:**
- ? S?a condition t? "?ã thanh toán" ? "??i giao hàng"
- ? Thêm Include OrderItems, OrderItem.Product
- ? Thêm Status, DeliveryStatus trong response

---

#### **Nh?n Giao ??n Hàng**
```http
POST /accept-order/{orderId}
```
- Shipper nh?n giao 1 ??n t? danh sách ch?
- C?p nh?t:
  - ShipperId = {current_shipper_id}
  - DeliveryStatus = "??i giao hàng" ? "?ang giao hàng"
  - UpdatedAt = NOW()
- Response: { message: "Nh?n giao ??n thành công!", orderId: 1 }

**Thay ??i so v?i c?:**
- ? S?a DeliveryStatus check t? "?ã thanh toán" ? "??i giao hàng"
- ? Set DeliveryStatus thành "?ang giao hàng" (không ph?i "?ang giao")

---

#### **Tab 2: Danh Sách ??n ?ang Giao**
```http
GET /deliveries-in-progress
```
- L?y danh sách ??n hàng hi?n t?i ?ang giao c?a shipper
- Filter: DeliveryStatus = "?ang giao hàng" AND ShipperId = {current_id}
- Response: Array<OrderDTO> (??y ?? thông tin: OrderItems, Payment, v.v)

**Thay ??i so v?i c?:**
- ? ??i tên endpoint t? "my-deliveries" ? "deliveries-in-progress"
- ? Thêm ??y ?? thông tin: Status, Payment, BrandId
- ? Thêm Include o.Payment

---

#### **Hoàn Thành Giao Hàng**
```http
POST /complete-delivery/{orderId}
```
- Shipper xác nh?n ??n hàng ?ã giao thành công
- C?p nh?t:
  - DeliveryStatus = "?ang giao hàng" ? "?ã giao hàng"
  - UpdatedAt = NOW()
- Response: { message: "Giao hàng thành công!", order: OrderDTO }

**Thay ??i so v?i c?:**
- ? S?a DeliveryStatus t? "Hoàn thành" ? "?ã giao hàng"
- ? Return ??y ?? OrderDTO
- ? Check DeliveryStatus = "?ang giao hàng"

---

#### **Tab 3: Danh Sách ??n ?ã Giao (M?I)**
```http
GET /completed-deliveries?page=1&pageSize=20
```
- L?y danh sách ??n hàng ?ã giao xong c?a shipper
- Filter: DeliveryStatus = "?ã giao hàng" AND ShipperId = {current_id}
- Pagination: page (default 1), pageSize (default 20)
- Sort: UpdatedAt descending (m?i nh?t tr??c)
- Response: { data: Array<OrderDTO>, page, pageSize, total }

**M?i:**
- ? Endpoint m?i ?? l?y l?ch s? giao hàng
- ? H? tr? phân trang
- ? S?p x?p theo ngày update m?i nh?t

---

### 3?? Location & Stats

#### Update Location
```http
POST /update-location
Body: LocationDTO { lat: number, long: number }
```
- C?p nh?t v? trí GPS c?a shipper
- Response: { message: "C?p nh?t v? trí thành công!" }

#### Get Stats
```http
GET /stats
```
- L?y th?ng kê: T?ng ??n ?ã giao, ??n hôm nay, rating trung bình
- Response: { totalDelivered, todayDelivered, averageRating }

**Thay ??i so v?i c?:**
- ? S?a filter t? DeliveryStatus = "Hoàn thành" ? "?ã giao hàng"

---

## Request/Response Examples

### ? GET /pending-orders
**Response:**
```json
[
  {
    "orderId": 1,
    "brandId": 5,
    "customerId": 10,
    "fullName": "Nguy?n V?n A",
    "phone": "0987654321",
    "address": "123 ???ng ABC, Hà N?i",
    "status": "?ã thanh toán",
    "deliveryStatus": "??i giao hàng",
    "totalAmount": 150000,
    "createdAt": "2025-01-15T10:30:00Z",
    "estimatedDeliveryDate": "2025-01-16T18:00:00Z",
    "orderItems": [
      {
        "itemId": 1,
        "orderId": 1,
        "productId": 5,
        "productName": "S?n ph?m A",
        "quantity": 2,
        "price": 75000
      }
    ]
  }
]
```

---

### ? POST /accept-order/1
**Response:**
```json
{
  "message": "Nh?n giao ??n thành công!",
  "orderId": 1
}
```

---

### ? GET /deliveries-in-progress
**Response:**
```json
[
  {
    "orderId": 1,
    "brandId": 5,
    "customerId": 10,
    "fullName": "Nguy?n V?n A",
    "phone": "0987654321",
    "address": "123 ???ng ABC, Hà N?i",
    "status": "?ã thanh toán",
    "deliveryStatus": "?ang giao hàng",
    "totalAmount": 150000,
    "createdAt": "2025-01-15T10:30:00Z",
    "estimatedDeliveryDate": "2025-01-16T18:00:00Z",
    "orderItems": [
      {
        "itemId": 1,
        "orderId": 1,
        "productId": 5,
        "productName": "S?n ph?m A",
        "quantity": 2,
        "price": 75000
      }
    ],
    "payment": {
      "paymentId": 1,
      "amount": 150000,
      "method": "COD",
      "status": "Pending"
    }
  }
]
```

---

### ? POST /complete-delivery/1
**Response:**
```json
{
  "message": "Giao hàng thành công!",
  "order": {
    "orderId": 1,
    "brandId": 5,
    "customerId": 10,
    "fullName": "Nguy?n V?n A",
    "phone": "0987654321",
    "address": "123 ???ng ABC, Hà N?i",
    "status": "?ã thanh toán",
    "deliveryStatus": "?ã giao hàng",
    "totalAmount": 150000,
    "createdAt": "2025-01-15T10:30:00Z",
    "estimatedDeliveryDate": "2025-01-16T18:00:00Z",
    "orderItems": [...],
    "payment": {...}
  }
}
```

---

### ? GET /completed-deliveries?page=1&pageSize=20
**Response:**
```json
{
  "data": [
    {
      "orderId": 1,
      "brandId": 5,
      "customerId": 10,
      "fullName": "Nguy?n V?n A",
      "phone": "0987654321",
      "address": "123 ???ng ABC, Hà N?i",
      "status": "?ã thanh toán",
      "deliveryStatus": "?ã giao hàng",
      "totalAmount": 150000,
      "createdAt": "2025-01-15T10:30:00Z",
      "estimatedDeliveryDate": "2025-01-16T18:00:00Z",
      "orderItems": [...],
      "payment": {...}
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 45
}
```

---

## DeliveryStatus State Machine

```
????????????????????????????????????????????????????????
? Initial Order Created (Order Controller)             ?
? DeliveryStatus = "??i giao hàng"                    ?
? ShipperId = null                                      ?
????????????????????????????????????????????????????????
                      ?
          GET /pending-orders (Tab 1)
                      ?
                POST /accept-order
                      ?
????????????????????????????????????????????????????????
? After Accept                                          ?
? DeliveryStatus = "?ang giao hàng"                   ?
? ShipperId = {shipper_id}                            ?
? UpdatedAt = NOW()                                    ?
????????????????????????????????????????????????????????
                      ?
          GET /deliveries-in-progress (Tab 2)
                      ?
              POST /complete-delivery
                      ?
????????????????????????????????????????????????????????
? After Complete                                        ?
? DeliveryStatus = "?ã giao hàng"                     ?
? ShipperId = {shipper_id}                            ?
? UpdatedAt = NOW()                                    ?
????????????????????????????????????????????????????????
                      ?
          GET /completed-deliveries (Tab 3)
```

---

## Key Changes Summary

| Item | C? | M?i | Reason |
|------|-----|-----|--------|
| pending-orders filter | "?ã thanh toán" | "??i giao hàng" | ?úng theo logic giao hàng |
| pending-orders includes | Minimal | OrderItems + Product | Frontend c?n d? li?u ??y ?? |
| accept-order DeliveryStatus | "?ang giao" | "?ang giao hàng" | Consistency |
| my-deliveries ? deliveries-in-progress | Có | Có (renamed) | Clarity |
| deliveries-in-progress includes | Minimal | Payment + OrderItems | Frontend c?n Payment info |
| complete-delivery DeliveryStatus | "Hoàn thành" | "?ã giao hàng" | Consistency v?i Order flow |
| completed-deliveries | ? Không có | ? M?i | L?ch s? giao hàng |
| stats filter | "Hoàn thành" | "?ã giao hàng" | Consistency |

---

## Frontend Implementation Checklist

- [ ] Tab 1: Call GET /pending-orders, display with "Nh?n Giao" button
- [ ] Tab 1: POST /accept-order/{id} on button click
- [ ] Tab 1: Auto-refresh after accept
- [ ] Tab 2: Call GET /deliveries-in-progress, display with "?ã Giao Hàng" button
- [ ] Tab 2: Show full order details including payment
- [ ] Tab 2: POST /complete-delivery/{id} on button click
- [ ] Tab 2: Auto-refresh after complete
- [ ] Tab 3: Call GET /completed-deliveries?page={page}&pageSize={size}
- [ ] Tab 3: Pagination support
- [ ] Tab 3: Show full order history
- [ ] Error handling for all endpoints
- [ ] Loading states for all API calls

---

## Notes

- T?t c? timestamp: **UTC** format
- T?t c? request/response: **JSON**
- ShipperId: L?y t? token claims **t? ??ng**
- Không c?n truy?n ShipperId trong body
- Rating shipper: **T?m b?** (s? thêm sau)
