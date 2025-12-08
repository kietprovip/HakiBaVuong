## ?? **QUICK TEST GUIDE - SHORT VIDEO API**

### Step 1: Login Customer Account
```
POST http://localhost:7258/api/auth/loginCustomer
Content-Type: application/json

{
  "email": "customer@example.com",
  "password": "password123"
}

Response:
{
  "message": "??ng nh?p thành công.",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "customerId": 1
}
```
? Copy `token` value vào Swagger Authorization (Bearer)

---

### Step 2: Test Like/Unlike
```
POST http://localhost:7258/api/reels/5/like
Authorization: Bearer {token}

Response (1st call):
{
  "message": "Liked!",
  "isLiked": true
}

Response (2nd call - same video):
{
  "message": "Unliked!",
  "isLiked": false
}
```

---

### Step 3: Test Comment
```
POST http://localhost:7258/api/reels/5/comment
Authorization: Bearer {token}
Content-Type: application/json

{
  "content": "Video này hay quá!"
}

Response:
{
  "videoId": 5,
  "customerId": 1,
  "customerName": "Tên Customer",
  "content": "Video này hay quá!",
  "createdAt": "2025-01-08T10:30:00Z",
  "message": "Bình lu?n thành công!"
}
```

---

### Step 4: Test Get Comments
```
GET http://localhost:7258/api/reels/5/comments?page=1
Authorization: Bearer {token} (optional)

Response:
[
  {
    "videoId": 5,
    "customerId": 1,
    "customerName": "Tên Customer",
    "content": "Video này hay quá!",
    "createdAt": "2025-01-08T10:30:00Z"
  }
]
```

---

### Step 5: Test Delete Comment
```
DELETE http://localhost:7258/api/reels/5/comment/1
Authorization: Bearer {token}

Note: customerId (1) ph?i = ng??i comment
Response: { "message": "Xóa bình lu?n thành công!" }
```

---

### Step 6: Test View Video
```
POST http://localhost:7258/api/reels/5/view
Authorization: Bearer {token} (optional)

Response (1st call):
{
  "message": "?ã ghi nh?n l??t xem.",
  "isNewView": true
}

Response (2nd call - same customer + video):
{
  "message": "?ã ghi nh?n l??t xem.",
  "isNewView": false
}
```

---

### Step 7: Test Share Video
```
POST http://localhost:7258/api/reels/5/share
Authorization: Bearer {token} (optional)

Response:
{
  "message": "Chia s? video thành công!"
}

Note: Shares counter luôn t?ng (dù call bao l?n)
```

---

### Step 8: Test Follow/Unfollow Brand
```
POST http://localhost:7258/api/reels/brand/2/follow
Authorization: Bearer {token}

Response: { "message": "Theo dõi brand thành công!" }

---

DELETE http://localhost:7258/api/reels/brand/2/unfollow
Authorization: Bearer {token}

Response: { "message": "H?y theo dõi thành công!" }
```

---

### Step 9: Test Brand Stats
```
GET http://localhost:7258/api/reels/brand/2/stats

Response:
{
  "brandId": 2,
  "brandName": "Brand ABC",
  "totalVideos": 5,
  "totalViews": 250,
  "totalLikes": 50,
  "totalComments": 20,
  "totalShares": 10
}
```

---

### Step 10: Test Brand Profile (Detailed)
```
GET http://localhost:7258/api/reels/brand/2/profile

Response:
{
  "brandId": 2,
  "brandName": "Brand ABC",
  "ownerName": "Ch? Brand XYZ",
  "followers": 15,
  "statistics": {
    "totalVideos": 5,
    "totalViews": 250,
    "totalLikes": 50,
    "totalComments": 20,
    "totalShares": 10,
    "averageViews": 50.0,
    "averageLikes": 10.0
  },
  "recentVideos": [
    {
      "videoId": 5,
      "caption": "Video m?i nh?t",
      "views": 100,
      "likes": 20,
      "commentsCount": 5,
      "shares": 3,
      "createdAt": "2025-01-08T12:00:00Z",
      "thumbnailUrl": "/Uploads/thumbnails/thumb_....jpg"
    }
  ]
}
```

---

## ?? **COMMON ISSUES & FIXES**

### Issue 1: 403 Forbidden on Like/Comment
**Cause:** Token không có role "Customer"
**Fix:** 
- Login b?ng `POST /api/auth/loginCustomer`
- Không ???c dùng Admin token
- Copy token vào Bearer authorization

### Issue 2: Duplicate Comment/Like
**Cause:** Composite key `(VideoId, CustomerId)` không cho phép duplicate
**Fix:** M?i customer ch? comment 1 l?n/video, like t? ??ng toggle

### Issue 3: View Count Not Increasing on 2nd Call
**Expected Behavior!**
- L?n ??u: isNewView=true, Views t?ng
- L?n 2+: isNewView=false, Views không t?ng
- ?ây là tính n?ng ?? tránh spam view

### Issue 4: Comment With CustomerId=null
**Cause:** VideoComment composite key b?t bu?c CustomerId
**Fix:** Ch? authenticated customers m?i có th? comment
- Public users không th? comment (nh?ng có th? view, share)

---

## ?? **DATABASE VERIFICATION**

### Check VideoLikes
```sql
SELECT * FROM VideoLikes
WHERE VideoId = 5 AND CustomerId = 1
```
Should return 1 row if liked, 0 rows if not liked

### Check VideoComments
```sql
SELECT * FROM VideoComments
WHERE VideoId = 5 AND CustomerId = 1
```
Should return 1 row max (composite key)

### Check VideoViews
```sql
SELECT * FROM VideoViews
WHERE VideoId = 5 AND CustomerId = 1
ORDER BY ViewedAt DESC
```
Shows last view time

### Check VideoShares
```sql
SELECT * FROM VideoShares
WHERE VideoId = 5 AND CustomerId = 1
ORDER BY SharedAt DESC
```
Shows last share time

### Check Brand Stats
```sql
SELECT 
  BrandId,
  COUNT(*) as TotalVideos,
  SUM(Views) as TotalViews,
  SUM(Likes) as TotalLikes,
  SUM(CommentsCount) as TotalComments,
  SUM(Shares) as TotalShares
FROM ShortVideos
WHERE BrandId = 2
GROUP BY BrandId
```

---

## ? **PERFORMANCE TIPS**

1. **Cache Brand Stats** - ???c g?i th??ng xuyên
2. **Index on (VideoId, CustomerId)** - t?t c? tables dùng composite key
3. **Avoid N+1 Queries** - endpoint profile ?ã Include owners? t?i ?u
4. **Batch Updates** - executeSqlRaw tránh N updates

---

## ?? **CHEAT SHEET - AUTHORIZATION MATRIX**

| Endpoint | Public | Login Required | Role |
|----------|--------|---|---|
| POST like | ? | ? | Customer |
| DELETE like | ? | ? | Customer |
| POST comment | ? | ? | Customer |
| DELETE comment | ? | ? | Customer (own only) |
| GET comments | ? | ? | - |
| POST view | ? | ? | - |
| POST share | ? | ? | - |
| POST follow | ? | ? | Customer |
| DELETE unfollow | ? | ? | Customer |
| GET stats | ? | ? | - |
| GET profile | ? | ? | - |
| GET feed | ? | ? | - |

---

**Last Updated:** 2025-01-08
**Status:** ? All endpoints tested & working
