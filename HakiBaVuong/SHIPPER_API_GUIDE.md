# Shipper API Guide - Qu?n Lý ??n Hàng Giao

## Gi?i Thi?u

H??ng d?n chi ti?t v? c? ch? qu?n lý ??n hàng giao cho Shipper, bao g?m ba tab chính:
- **Tab 1: ??n ch? giao** - Danh sách ??n hàng ch? shipper nh?n
- **Tab 2: ??n ?ang giao** - Danh sách ??n hàng hi?n t?i ?ang giao
- **Tab 3: ??n ?ã giao** - L?ch s? các ??n ?ã giao xong

---

## 1. DANH SÁCH ??N CH? GIAO (Tab 1)

### Endpoint
```
GET /api/shipper/pending-orders
```

### Description
L?y danh sách các ??n hàng ch? shipper nh?n. Nh?ng ??n này có:
- `DeliveryStatus = "??i giao hàng"`
- `ShipperId = null` (ch?a có shipper)
- S?p x?p theo ngày t?o (c? nh?t tr??c)
- T?i ?a 20 ??n

### Authorization
- Yêu c?u token Shipper
- Header: `Authorization: Bearer {token}`

### Response (200 OK)
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

### Logic
- Hi?n th? nh?ng ??n hàng ?ã thanh toán, ch? shipper nh?n giao
- Ch? hi?n th? nh?ng ??n ch?a có shipper (`ShipperId = null`)
- Shipper có th? ch?n b?m "Nh?n giao" ?? nh?n ??n

---

## 2. NH?N GIAO ??N HÀNG

### Endpoint
```
POST /api/shipper/accept-order/{orderId}
```

### Description
Shipper nh?n m?t ??n hàng t? danh sách ch? giao.

**Khi nh?n, h? th?ng s?:**
- Gán `ShipperId = {current_shipper_id}`
- Thay ??i `DeliveryStatus`: "??i giao hàng" ? "?ang giao hàng"
- C?p nh?t `UpdatedAt = NOW()`

### Authorization
- Yêu c?u token Shipper

### Parameters
- `orderId` (int, required): ID c?a ??n hàng mu?n nh?n

### Request
```
POST /api/shipper/accept-order/1
```

### Response (200 OK)
```json
{
  "message": "Nh?n giao ??n thành công!",
  "orderId": 1
}
```

### Error Cases
| Status | Message | Nguyên nhân |
|--------|---------|-----------|
| 404 | ??n hàng không t?n t?i. | OrderId không t?n t?i |
| 400 | ??n hàng ?ã ???c shipper khác nh?n. | ShipperId ? null |
| 400 | ??n hàng ch?a s?n sàng ?? giao. | DeliveryStatus ? "??i giao hàng" |

### Frontend Action
- Sau khi nh?n giao thành công, ??n s? **t? ??ng chuy?n t? Tab 1 ? Tab 2**

---

## 3. DANH SÁCH ??N ?ANG GIAO (Tab 2)

### Endpoint
```
GET /api/shipper/deliveries-in-progress
```

### Description
L?y danh sách các ??n hàng mà shipper hi?n t?i ?ang giao.

**?i?u ki?n:**
- `ShipperId = {current_shipper_id}`
- `DeliveryStatus = "?ang giao hàng"`

### Authorization
- Yêu c?u token Shipper

### Response (200 OK)
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

### Logic
- Hi?n th? t?t c? thông tin chi ti?t c?a ??n hàng
- Bao g?m danh sách s?n ph?m và thông tin thanh toán
- Shipper có th? xem chi ti?t, c?p nh?t GPS, theo dõi ??n
- Khi giao xong, b?m "?ã giao hàng" ?? chuy?n sang Tab 3

---

## 4. HOÀN THÀNH GIAO HÀNG

### Endpoint
```
POST /api/shipper/complete-delivery/{orderId}
```

### Description
Shipper xác nh?n ??n hàng ?ã giao thành công.

**Khi hoàn thành, h? th?ng s?:**
- Thay ??i `DeliveryStatus`: "?ang giao hàng" ? "?ã giao hàng"
- C?p nh?t `UpdatedAt = NOW()`
- ??n s? chuy?n sang Tab 3 (??n ?ã giao)

### Authorization
- Yêu c?u token Shipper

### Parameters
- `orderId` (int, required): ID c?a ??n hàng ?ã giao xong

### Request
```
POST /api/shipper/complete-delivery/1
```

### Response (200 OK)
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
}
```

### Error Cases
| Status | Message | Nguyên nhân |
|--------|---------|-----------|
| 404 | Không tìm th?y ??n hàng. | OrderId không t?n t?i ho?c shipper không ph?i ng??i nh?n |
| 400 | ??n hàng ch?a ? tr?ng thái ?ang giao. | DeliveryStatus ? "?ang giao hàng" |

### Frontend Action
- Sau khi hoàn thành giao thành công, ??n s? **t? ??ng chuy?n t? Tab 2 ? Tab 3**

---

## 5. DANH SÁCH ??N ?Ã GIAO (Tab 3)

### Endpoint
```
GET /api/shipper/completed-deliveries?page=1&pageSize=20
```

### Description
L?y danh sách các ??n hàng mà shipper ?ã giao xong.

**?i?u ki?n:**
- `ShipperId = {current_shipper_id}`
- `DeliveryStatus = "?ã giao hàng"`
- S?p x?p theo `UpdatedAt` (m?i nh?t tr??c)

### Authorization
- Yêu c?u token Shipper

### Query Parameters
| Param | Type | Default | Description |
|-------|------|---------|-----------|
| page | int | 1 | Trang hi?n t?i |
| pageSize | int | 20 | S? l??ng ??n trên m?i trang |

### Response (200 OK)
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
  ],
  "page": 1,
  "pageSize": 20,
  "total": 45
}
```

### Logic
- Hi?n th? l?ch s? các ??n ?ã giao thành công
- H? tr? phân trang ?? d? dàng tìm ki?m
- M?i nh?t xu?t hi?n tr??c

---

## Flow Diagram - Chu K? Giao Hàng

```
????????????????????????????????????????????
? TAB 1: ??N CH? GIAO                     ?
? DeliveryStatus = "??i giao hàng"       ?
? ShipperId = null                        ?
? GET /api/shipper/pending-orders        ?
????????????????????????????????????????????
                ?
       Shipper b?m "Nh?n Giao"
                ?
        POST /api/shipper/accept-order/{id}
                ?
        C?p nh?t database:
        - ShipperId = {shipper_id}
        - DeliveryStatus = "?ang giao hàng"
                ?
????????????????????????????????????????????
? TAB 2: ??N ?ANG GIAO                    ?
? DeliveryStatus = "?ang giao hàng"      ?
? ShipperId = {shipper_id}               ?
? GET /api/shipper/deliveries-in-progress?
?                                         ?
? (Shipper chu?n b? giao hàng)           ?
????????????????????????????????????????????
                ?
       Shipper b?m "?ã Giao Hàng"
                ?
    POST /api/shipper/complete-delivery/{id}
                ?
        C?p nh?t database:
        - DeliveryStatus = "?ã giao hàng"
        - UpdatedAt = NOW()
                ?
????????????????????????????????????????????
? TAB 3: ??N ?Ã GIAO                     ?
? DeliveryStatus = "?ã giao hàng"        ?
? ShipperId = {shipper_id}               ?
? GET /api/shipper/completed-deliveries  ?
?                                         ?
? (L?ch s? giao hàng)                    ?
????????????????????????????????????????????
```

---

## DeliveryStatus Values (Tr?ng Thái Giao Hàng)

| Status | Ý Ngh?a | Th?i ?i?m | Shipper |
|--------|---------|----------|---------|
| `"??i giao hàng"` | ??n thanh toán, ch? shipper nh?n | T?o order | Ch?a có |
| `"?ang giao hàng"` | Shipper ?ã nh?n, ?ang giao | Accept order | Có |
| `"?ã giao hàng"` | Giao xong, khách nh?n r?i | Complete delivery | Có |

---

## Status vs DeliveryStatus (Khác bi?t)

- **Status**: Tr?ng thái thanh toán (Ch?a thanh toán, ?ã thanh toán, Hu? ??n)
- **DeliveryStatus**: Tr?ng thái giao hàng (??i giao, ?ang giao, ?ã giao)

### M?i Quan H?:
```
Order Status = "?ã thanh toán" ? DeliveryStatus = "??i giao hàng"
                                    ?
                            ShipperId = gán ID
                                    ?
                          DeliveryStatus = "?ang giao hàng"
                                    ?
                          DeliveryStatus = "?ã giao hàng"
```

---

## Authentication & Security

### Token Requirements
- **Type**: JWT Bearer Token
- **Role**: Shipper
- **Header**: `Authorization: Bearer {token}`

### Token Generation
- ???c t?o t? **ShipperAuthController**
- Endpoint: `/api/shipper-auth/login`
- Ch?a claim `NameIdentifier = ShipperId`

### Token Expiry
- M?c ??nh: 3 gi?
- Sau h?t h?n, shipper c?n login l?i

---

## Data Models

### Order Model
```csharp
public class Order
{
    public int OrderId { get; set; }
    public int? ShipperId { get; set; }        // ID shipper giao
    public string DeliveryStatus { get; set; } // ??i giao/?ang giao/?ã giao
    public string FullName { get; set; }       // Tên khách
    public string Phone { get; set; }          // S?T khách
    public string Address { get; set; }        // ??a ch? giao
    public decimal TotalAmount { get; set; }   // T?ng ti?n
    public DateTime CreatedAt { get; set; }    // Ngày t?o
    public DateTime UpdatedAt { get; set; }    // C?p nh?t l?n cu?i
}
```

### OrderItem Model
```csharp
public class OrderItem
{
    public int ItemId { get; set; }
    public int OrderId { get; set; }
    public string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
```

### Payment Model
```csharp
public class Payment
{
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; }          // COD, BankCard, etc
    public string Status { get; set; }          // Pending, Completed, etc
    public int OrderId { get; set; }
}
```

---

## Frontend Implementation Guide

### 1. Tab Structure
```javascript
// Tab 1: ??n ch? giao
GET /api/shipper/pending-orders
? Hi?n th? danh sách + nút "Nh?n Giao"

// Tab 2: ??n ?ang giao
GET /api/shipper/deliveries-in-progress
? Hi?n th? danh sách + nút "?ã Giao Hàng"

// Tab 3: ??n ?ã giao
GET /api/shipper/completed-deliveries
? Hi?n th? l?ch s?
```

### 2. Main Actions
```javascript
// Action 1: Accept Order
POST /api/shipper/accept-order/{orderId}
? Refresh Tab 1 & Tab 2

// Action 2: Complete Delivery
POST /api/shipper/complete-delivery/{orderId}
? Refresh Tab 2 & Tab 3
```

### 3. Display Fields
```javascript
// B?t bu?c hi?n th?:
- FullName (Tên khách)
- Phone (S?T)
- Address (??a ch? giao)
- TotalAmount (T?ng ti?n)
- EstimatedDeliveryDate (Ngày d? ki?n)
- OrderItems (Danh sách s?n ph?m)

// Optional:
- Payment.Status (Tr?ng thái thanh toán)
- CreatedAt (Ngày t?o)
```

### 4. Auto-Refresh Strategy
```javascript
// Tab 1 & 2:
- Sau accept: G?i l?i pending-orders & deliveries-in-progress
- Delay: 500ms-1s

// Tab 2 & 3:
- Sau complete: G?i l?i deliveries-in-progress & completed-deliveries
- Delay: 500ms-1s
```

### 5. Error Handling
```javascript
// Error 404: ??n không t?n t?i
? Refresh danh sách, xóa ??n kh?i UI

// Error 400: ??n ?ã có shipper
? Hi?n th? thông báo, refresh danh sách

// Error 400: DeliveryStatus không h?p l?
? Hi?n th? thông báo, ki?m tra logic
```

---

## Caching & Performance

### Recommended:
- **Pending Orders**: Refresh m?i 1-2 phút (ho?c real-time socket)
- **Deliveries In Progress**: Refresh m?i 30 giây (vì shipper ?ang giao)
- **Completed Deliveries**: Cache 5 phút

---

## Features NOT Implemented Yet

- ? **Shipper Rating**: T?m b?, s? thêm sau
- ? **Real-time Tracking**: C?n GPS integration
- ? **Photo Delivery Proof**: C?n storage (S3/Azure)
- ? **Communication**: Chat v?i customer

---

## Versioning & Support

- **API Version**: v1
- **Last Updated**: 27/11/2025
- **Status**: Production Ready
- **Contact**: BE Team

---

## Quick Reference - cURL Examples

### 1. L?y danh sách ??n ch? giao
```bash
curl -X GET "http://localhost:7258/api/shipper/pending-orders" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json"
```

### 2. Nh?n giao ??n hàng
```bash
curl -X POST "http://localhost:7258/api/shipper/accept-order/1" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json"
```

### 3. L?y danh sách ??n ?ang giao
```bash
curl -X GET "http://localhost:7258/api/shipper/deliveries-in-progress" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json"
```

### 4. Hoàn thành giao hàng
```bash
curl -X POST "http://localhost:7258/api/shipper/complete-delivery/1" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json"
```

### 5. L?y danh sách ??n ?ã giao
```bash
curl -X GET "http://localhost:7258/api/shipper/completed-deliveries?page=1&pageSize=20" \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json"
```

---

## Notes

- T?t c? timestamp s? d?ng **UTC** format
- T?t c? request/response là **JSON**
- ShipperId ???c l?y t? **JWT Token Claims** t? ??ng
- Không c?n truy?n ShipperId trong request body
