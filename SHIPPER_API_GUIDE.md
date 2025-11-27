# ?? SHIPPER API - H??NG D?N ??Y ?? CHO FRONTEND

---

## ?? GI?I THI?U
- **Base URL:** `http://localhost:7258`
- **Framework:** ASP.NET Core 8
- **Authentication:** JWT Bearer Token
- **Token Expiry:** 3 gi?
- **OTP Timeout:** 30 phút

---

## ?? AUTHENTICATION ENDPOINTS
**Route:** `POST /api/shipper-auth/*`

### 1?? ??NG KÝ SHIPPER
**Endpoint:** `POST /register`

**Request:**
```json
{
  "name": "Nguy?n V?n A",
  "email": "shipper@example.com",
  "password": "123456",
  "confirmPassword": "123456",
  "phone": "0912345678",
  "vehicleType": "Xe máy",
  "licensePlate": "29X-12345"
}
```

**Response (200 OK):**
```json
{
  "message": "??ng ký thành công. Vui lòng ki?m tra email ?? xác th?c."
}
```

**Errors:**
```json
{
  "message": "Email ?ã t?n t?i."
}
```
```json
{
  "message": "M?t kh?u xác nh?n không kh?p."
}
```
```json
{
  "message": "M?t kh?u ph?i có ít nh?t 6 ký t?."
}
```

**Validation Rules:**
- ? Email ch?a t?n t?i
- ? Password = ConfirmPassword
- ? Password >= 6 ký t?
- ? OTP s? g?i email (h?t h?n 30 phút)

**Similar to:** `customerAuth/registerCustomer`  
**Difference:** Thêm `phone`, `vehicleType`, `licensePlate`

---

### 2?? XÁC TH?C EMAIL B?NG OTP
**Endpoint:** `POST /verify-email`

**Request:**
```json
{
  "email": "shipper@example.com",
  "otp": "123456"
}
```

**Response (200 OK):**
```json
{
  "message": "Xác th?c email thành công. B?n có th? ??ng nh?p."
}
```

**Errors:**
```json
{
  "message": "Email không t?n t?i ho?c ?ã ???c xác th?c."
}
```
```json
{
  "message": "Mã OTP không ?úng ho?c ?ã h?t h?n."
}
```

**Logic:**
- Email ph?i ch?a verified
- OTP ph?i ?úng và còn hi?u l?c
- Sau verify thành công ? có th? login

---

### 3?? ??NG NH?P
**Endpoint:** `POST /login`

**Request:**
```json
{
  "email": "shipper@example.com",
  "password": "123456"
}
```

**Response (200 OK):**
```json
{
  "message": "??ng nh?p thành công.",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "shipperId": 1,
  "name": "Nguy?n V?n A",
  "rating": 4.5
}
```

**Errors:**
```json
{
  "message": "Sai email ho?c m?t kh?u."
}
```
```json
{
  "message": "Email ch?a ???c xác th?c."
}
```

**L?u Token:**
```javascript
// React/JavaScript
localStorage.setItem('shipperToken', response.token);
localStorage.setItem('shipperId', response.shipperId);
```

---

### 4?? QUÊN M?T KH?U
**Endpoint:** `POST /forgot-password`

**Request:**
```json
{
  "email": "shipper@example.com"
}
```

**Response (200 OK):**
```json
{
  "message": "Mã OTP ?ã ???c g?i ??n email c?a b?n."
}
```

**Errors:**
```json
{
  "message": "Email không t?n t?i."
}
```

**Logic:**
- G?i OTP qua email
- OTP h?t h?n 30 phút
- N?u email không t?n t?i ? v?n return success (b?o v? thông tin)

---

### 5?? ??T L?I M?T KH?U
**Endpoint:** `POST /reset-password`

**Request:**
```json
{
  "email": "shipper@example.com",
  "otp": "123456",
  "newPassword": "newpass123",
  "confirmPassword": "newpass123"
}
```

**Response (200 OK):**
```json
{
  "message": "??t l?i m?t kh?u thành công."
}
```

**Errors:**
```json
{
  "message": "Mã OTP không ?úng ho?c ?ã h?t h?n."
}
```
```json
{
  "message": "M?t kh?u xác nh?n không kh?p."
}
```
```json
{
  "message": "M?t kh?u ph?i có ít nh?t 6 ký t?."
}
```

---

### 6?? L?Y THÔNG TIN SHIPPER (Có Token)
**Endpoint:** `GET /user-info`

**Headers:**
```
Authorization: Bearer <token>
```

**Response (200 OK):**
```json
{
  "shipperId": 1,
  "email": "shipper@example.com",
  "name": "Nguy?n V?n A",
  "phone": "0912345678",
  "vehicleType": "Xe máy",
  "licensePlate": "29X-12345",
  "rating": 4.5
}
```

**Errors:**
```json
{
  "message": "Token không h?p l?."
}
```
```json
{
  "message": "Không tìm th?y shipper."
}
```

---

### 7?? GOOGLE LOGIN
**Endpoint:** `GET /login-google`

**Flow:**
1. User click "Login with Google"
2. Redirect ??n Google OAuth
3. Google redirect l?i: `http://localhost:5174/shipper-dashboard?token=<TOKEN>&shipperId=<ID>`
4. Parse query params ? save token

**Auto-create:** N?u email Google ch?a t?n t?i ? t? t?o account m?i

---

### 8?? FACEBOOK LOGIN
**Endpoint:** `GET /login-facebook`

**Flow:** Gi?ng Google

---

## ?? SHIPPER FEATURES ENDPOINTS
**Route:** `GET/POST /api/shipper/*`

**Yêu c?u:** T?t c? endpoint c?n JWT Token trong header
```
Authorization: Bearer <token>
```

---

### 1?? L?Y PROFILE SHIPPER
**Endpoint:** `GET /profile`

**Headers:**
```
Authorization: Bearer <token>
```

**Response (200 OK):**
```json
{
  "shipperId": 1,
  "name": "Nguy?n V?n A",
  "email": "shipper@example.com",
  "phone": "0912345678",
  "vehicleType": "Xe máy",
  "licensePlate": "29X-12345",
  "rating": 4.5,
  "createdAt": "2025-11-26T10:00:00Z"
}
```

**Errors:**
```json
{
  "message": "Không tìm th?y shipper."
}
```

---

### 2?? C?P NH?T PROFILE
**Endpoint:** `PUT /profile`

**Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request (Các field optional):**
```json
{
  "name": "Nguy?n V?n A M?i",
  "phone": "0987654321",
  "vehicleType": "Ô tô",
  "licensePlate": "29X-99999"
}
```

**Response (200 OK):**
```json
{
  "message": "C?p nh?t h? s? thành công!"
}
```

---

### 3?? DANH SÁCH ??N HÀNG CH?A NH?N
**Endpoint:** `GET /pending-orders`

**Headers:**
```
Authorization: Bearer <token>
```

**Response (200 OK):**
```json
[
  {
    "orderId": 1,
    "fullName": "Tr?n Th? B",
    "phone": "0987654321",
    "address": "123 ???ng Lý Th??ng Ki?t, Qu?n 1, TP.HCM",
    "totalAmount": 500000,
    "createdAt": "2025-11-26T09:00:00Z",
    "estimatedDeliveryDate": "2025-11-27T00:00:00Z"
  },
  {
    "orderId": 2,
    "fullName": "Lê V?n C",
    "phone": "0912345678",
    "address": "456 ???ng Nguy?n Hue, Qu?n 1, TP.HCM",
    "totalAmount": 750000,
    "createdAt": "2025-11-26T08:30:00Z",
    "estimatedDeliveryDate": "2025-11-27T00:00:00Z"
  }
]
```

**Logic:**
- L?c: `DeliveryStatus = "?ã thanh toán"` (?ã thanh toán, ch?a có shipper)
- S?p x?p: Theo `createdAt` (??n c? nh?t ?u tiên - FIFO)
- Limit: Top 20 ??n
- Shipper có th? click ?? nh?n giao

---

### 4?? NH?N GIAO ??N HÀNG
**Endpoint:** `POST /accept-order/{orderId}`

**Headers:**
```
Authorization: Bearer <token>
```

**Example:** `/accept-order/1`

**Response (200 OK):**
```json
{
  "message": "Nh?n giao ??n thành công!",
  "orderId": 1
}
```

**Errors:**
```json
{
  "message": "??n hàng không t?n t?i."
}
```
```json
{
  "message": "??n hàng ?ã ???c shipper khác nh?n."
}
```
```json
{
  "message": "??n hàng ch?a s?n sàng ?? giao."
}
```

**Logic:**
- Order ph?i ch?a có shipper (`ShipperId = null`)
- Order status ph?i = `"?ã thanh toán"`
- Sau accept:
  - `ShipperId = <current shipper id>`
  - `DeliveryStatus = "?ang giao"`
  - `UpdatedAt = <current time>`

---

### 5?? DANH SÁCH ??N ?ANG GIAO C?A TÔI
**Endpoint:** `GET /my-deliveries`

**Headers:**
```
Authorization: Bearer <token>
```

**Response (200 OK):**
```json
[
  {
    "orderId": 1,
    "fullName": "Tr?n Th? B",
    "phone": "0987654321",
    "address": "123 ???ng Lý Th??ng Ki?t, Qu?n 1, TP.HCM",
    "totalAmount": 500000,
    "estimatedDeliveryDate": "2025-11-27T00:00:00Z",
    "orderItems": [
      {
        "productName": "Áo thun nam size M",
        "quantity": 2
      },
      {
        "productName": "Qu?n Jean nam",
        "quantity": 1
      }
    ]
  }
]
```

**Logic:**
- L?y t?t c? order c?a shipper hi?n t?i
- V?i `DeliveryStatus = "?ang giao"`
- Include chi ti?t s?n ph?m (tên + s? l??ng)

---

### 6?? HOÀN THÀNH GIAO HÀNG
**Endpoint:** `POST /complete-delivery/{orderId}`

**Headers:**
```
Authorization: Bearer <token>
```

**Example:** `/complete-delivery/1`

**Response (200 OK):**
```json
{
  "message": "Giao hàng thành công! C?m ?n b?n!"
}
```

**Errors:**
```json
{
  "message": "Không tìm th?y ??n hàng."
}
```
```json
{
  "message": "??n hàng ch?a ? tr?ng thái ?ang giao."
}
```

**Logic:**
- Order ph?i c?a shipper hi?n t?i
- Order status ph?i = `"?ang giao"`
- Sau complete:
  - `DeliveryStatus = "Hoàn thành"`
  - `UpdatedAt = <current time>`
  - Khách có th? ?ánh giá shipper

---

### 7?? TH?NG KÊ & HI?U SU?T
**Endpoint:** `GET /stats`

**Headers:**
```
Authorization: Bearer <token>
```

**Response (200 OK):**
```json
{
  "totalDelivered": 50,
  "todayDelivered": 5,
  "averageRating": 4.7
}
```

**Gi?i thích:**
- `totalDelivered`: T?ng s? ??n hoàn thành (all-time)
- `todayDelivered`: S? ??n hoàn thành hôm nay
- `averageRating`: ?ánh giá trung bình t? khách hàng (1-5 stars)

**Dùng cho:** Dashboard stats, profile display

---

## ?? DATA STRUCTURES

### ShipperRegisterDTO
```typescript
{
  name: string;              // Tên shipper
  email: string;             // Email (unique)
  password: string;          // Min 6 ký t?
  confirmPassword: string;   // Ph?i = password
  phone: string;             // S? ?i?n tho?i
  vehicleType: string;       // "Xe máy", "Ô tô", etc
  licensePlate: string;      // Bi?n s? xe
}
```

### ShipperLoginDTO
```typescript
{
  email: string;
  password: string;
}
```

### VerifyOtpDTO
```typescript
{
  email: string;
  otp: string;  // 6 s?
}
```

### ForgotPasswordDTO
```typescript
{
  email: string;
}
```

### ResetPasswordDTO
```typescript
{
  email: string;
  otp: string;
  newPassword: string;
  confirmPassword: string;
}
```

### ShipperUpdateDTO
```typescript
{
  name?: string;
  phone?: string;
  vehicleType?: string;
  licensePlate?: string;
}
```

### JWT Token Payload
```typescript
{
  NameIdentifier: string;  // shipperId
  Email: string;
  Role: "Shipper"
}
```

---

## ?? USER FLOWS

### ? REGISTER & LOGIN FLOW
```
1. User nh?p thông tin ? POST /register
2. Email nh?n OTP ? User nh?p OTP
3. POST /verify-email
4. POST /login
5. ? Có token, l?u localStorage
```

### ? DELIVERY WORKFLOW
```
1. GET /pending-orders (danh sách ??n ch?)
2. User click order ? POST /accept-order/{id}
3. GET /my-deliveries (xem ??n c?a tôi)
4. Giao xong ? POST /complete-delivery/{id}
5. ? Hoàn thành
```

### ? PASSWORD RESET
```
1. POST /forgot-password ? Email nh?n OTP
2. POST /reset-password (v?i OTP)
3. ? M?t kh?u ??i, có th? login l?i
```

---

## ?? TEST TRONG SWAGGER

1. **Open Swagger UI:** `http://localhost:7258/swagger/ui`

2. **??ng ký & Verify:**
   - POST `/api/shipper-auth/register` ? Note email & check inbox
   - POST `/api/shipper-auth/verify-email` ? Nh?p OTP t? email
   - POST `/api/shipper-auth/login` ? Copy token

3. **Authorize:**
   - Click nút "Authorize" (góc trên ph?i)
   - Paste: `<token>` (ch? token, không c?n "Bearer ")
   - Click "Authorize"

4. **Test Protected Endpoints:**
   - GET `/api/shipper/profile`
   - GET `/api/shipper/pending-orders`
   - POST `/api/shipper/accept-order/1`
   - etc.

---

## ??? REACT IMPLEMENTATION EXAMPLES

### Setup axios
```javascript
// api.js
import axios from 'axios';

const API_BASE = 'http://localhost:7258/api';

const api = axios.create({
  baseURL: API_BASE,
});

// Add token to headers
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('shipperToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export default api;
```

### Register
```javascript
// Register.jsx
const handleRegister = async (formData) => {
  try {
    const res = await api.post('/shipper-auth/register', formData);
    console.log('Register success:', res.data);
    // Redirect to verify email page
  } catch (error) {
    console.error('Register failed:', error.response.data);
  }
};
```

### Login
```javascript
// Login.jsx
const handleLogin = async (email, password) => {
  try {
    const res = await api.post('/shipper-auth/login', { email, password });
    localStorage.setItem('shipperToken', res.data.token);
    localStorage.setItem('shipperId', res.data.shipperId);
    // Redirect to dashboard
  } catch (error) {
    console.error('Login failed:', error.response.data.message);
  }
};
```

### Get Pending Orders
```javascript
// PendingOrders.jsx
const fetchPendingOrders = async () => {
  try {
    const res = await api.get('/shipper/pending-orders');
    setOrders(res.data);
  } catch (error) {
    console.error('Failed:', error.response.data);
  }
};
```

### Accept Order
```javascript
const handleAcceptOrder = async (orderId) => {
  try {
    const res = await api.post(`/shipper/accept-order/${orderId}`);
    console.log('Order accepted:', res.data.message);
    // Refresh my-deliveries list
  } catch (error) {
    console.error('Failed:', error.response.data.message);
  }
};
```

---

## ?? COMPARISON: SHIPPER vs CUSTOMER AUTH

| Feature | ShipperAuth | CustomerAuth |
|---------|-------------|--------------|
| Register | ? + vehicle info | ? |
| Verify Email | ? | ? |
| Login | ? | ? |
| Forgot Password | ? | ? |
| Reset Password | ? | ? |
| Social Login | Google, Facebook | Google, Facebook |
| **Difference** | `phone`, `vehicleType`, `licensePlate`, `rating` | - |

---

## ?? ERROR CODES & MESSAGES

| Scenario | HTTP Code | Message |
|----------|-----------|---------|
| Email exists | 400 | "Email ?ã t?n t?i." |
| Invalid password | 400 | "M?t kh?u xác nh?n không kh?p." |
| Password too short | 400 | "M?t kh?u ph?i có ít nh?t 6 ký t?." |
| Invalid credentials | 401 | "Sai email ho?c m?t kh?u." |
| Email not verified | 400 | "Email ch?a ???c xác th?c." |
| Invalid OTP | 400 | "Mã OTP không ?úng ho?c ?ã h?t h?n." |
| Order not found | 404 | "??n hàng không t?n t?i." |
| Token invalid | 401 | "Token không h?p l?." |
| Server error | 500 | "L?i server..." |

---

## ?? SECURITY NOTES

? Password hashed v?i BCrypt (Enhanced)  
? JWT token h?t h?n 3 gi?  
? OTP h?t h?n 30 phút  
? OTP không l?u DB, ch? cache (b?o v?)  
? CORS configured (localhost:5173, 5174)  
? Email verification required before login  

---

## ?? NOTES FOR FRONTEND TEAM

1. **Token Storage:** L?u ? localStorage ho?c sessionStorage
2. **Auto Logout:** N?u token h?t h?n ? redirect login
3. **Order Status:** 
   - `"?ã thanh toán"` = ready to pick
   - `"?ang giao"` = in delivery
   - `"Hoàn thành"` = completed
4. **Rating:** Sau giao xong, khách có th? ?ánh giá (t? 1-5 stars)
5. **GPS/Location:** ???c implement riêng (không c?n web version)

---

## ?? SUPPORT

- **Base URL:** http://localhost:7258
- **Swagger:** http://localhost:7258/swagger/ui
- **Email Test:** Check inbox khi register/forgot password
- **Token Error:** Có th? token h?t h?n ? login l?i

---

**? Ready to code! Happy coding! ??**
