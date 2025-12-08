## ?? SHORT VIDEO API ENDPOINTS - Tóm T?t Hoàn Ch?nh

### ?? **LIKE & UNLIKE**

#### POST `/api/reels/{videoId}/like` - Like/Unlike Video (Toggle)
**Auth:** `[Authorize(Roles = "Customer")]`
- **Params:** `videoId` (int)
- **Body:** Empty
- **Response:**
  ```json
  {
    "message": "Liked!",
    "isLiked": true
  }
  ```
- **CustomerId:** L?y t? ??ng t? token
- **C? ch?:** 
  - L?n ??u: Thêm vào VideoLikes table, t?ng Likes counter
  - L?n th? 2: Xóa kh?i VideoLikes, gi?m Likes counter

#### DELETE `/api/reels/{videoId}/like` - Unlike Video (Direct)
**Auth:** `[Authorize(Roles = "Customer")]`
- **Params:** `videoId` (int)
- **Response:** `{ "message": "Unliked!", "isLiked": false }`
- **?i?u ki?n:** Ph?i ?ã like tr??c ?ó

---

### ?? **COMMENTS**

#### POST `/api/reels/{videoId}/comment` - Add Comment
**Auth:** `[Authorize(Roles = "Customer")]`
- **Params:** `videoId` (int)
- **Body:** 
  ```json
  {
    "content": "Video này hay quá!"
  }
  ```
- **Response:**
  ```json
  {
    "videoId": 5,
    "customerId": 10,
    "customerName": "Tên Customer",
    "content": "Video này hay quá!",
    "createdAt": "2025-01-08T10:30:00Z",
    "message": "Bình lu?n thành công!"
  }
  ```
- **Composite Key:** `(VideoId, CustomerId)` - m?i customer ch? comment 1 l?n/video

#### GET `/api/reels/{videoId}/comments?page=1` - Get Comments (Paginated)
**Auth:** Public
- **Params:** `videoId`, `page` (int, default=1)
- **Response:**
  ```json
  [
    {
      "videoId": 5,
      "customerId": 10,
      "customerName": "Tên Customer",
      "content": "N?i dung comment",
      "createdAt": "2025-01-08T10:30:00Z"
    }
  ]
  ```

#### DELETE `/api/reels/{videoId}/comment/{customerId}` - Delete Comment
**Auth:** `[Authorize(Roles = "Customer")]`
- **Params:** `videoId`, `customerId` (int)
- **?i?u ki?n:** 
  - Ph?i là CustomerId trong token (không th? xóa comment ng??i khác)
  - Gi?m CommentsCount t? video

---

### ??? **VIEWS (L??t Xem)**

#### POST `/api/reels/{videoId}/view` - Record Video View
**Auth:** Optional
- **Params:** `videoId` (int)
- **Body:** Empty
- **Response:** 
  ```json
  {
    "message": "?ã ghi nh?n l??t xem.",
    "isNewView": true
  }
  ```
- **C? ch?:**
  - **N?u ?ã login:** L?u vào b?ng VideoViews v?i composite key `(VideoId, CustomerId)`
    - L?n ??u: T?ng Views counter
    - L?n sau: Ch? update `ViewedAt`, không t?ng counter
  - **N?u ch?a login:** Luôn t?ng Views counter
  - **Không yêu c?u login** nh?ng tracking t?t h?n v?i login

---

### ?? **SHARES (Chia S?)**

#### POST `/api/reels/{videoId}/share` - Share Video
**Auth:** Optional
- **Params:** `videoId` (int)
- **Response:** `{ "message": "Chia s? video thành công!" }`
- **C? ch?:**
  - **N?u ?ã login:** L?u vào b?ng VideoShares v?i composite key `(VideoId, CustomerId)`
    - L?n ??u: T?o record m?i
    - L?n sau: Update `SharedAt`
  - **Luôn t?ng Shares counter** (không quan tr?ng login hay không)

---

### ? **FOLLOW/UNFOLLOW BRAND**

#### POST `/api/reels/brand/{brandId}/follow` - Follow Brand
**Auth:** `[Authorize(Roles = "Customer")]`
- **Params:** `brandId` (int)
- **Response:** `{ "message": "Theo dõi brand thành công!" }`
- **?i?u ki?n:** Không th? follow 2 l?n cùng brand

#### DELETE `/api/reels/brand/{brandId}/unfollow` - Unfollow Brand
**Auth:** `[Authorize(Roles = "Customer")]`
- **Params:** `brandId` (int)
- **Response:** `{ "message": "H?y theo dõi thành công!" }`
- **?i?u ki?n:** Ph?i ?ã follow tr??c ?ó

---

### ?? **BRAND STATS & PROFILE**

#### GET `/api/reels/brand/{brandId}/stats` - Get Brand Video Stats
**Auth:** Optional
- **Params:** `brandId` (int)
- **Response:**
  ```json
  {
    "brandId": 1,
    "brandName": "Brand ABC",
    "totalVideos": 10,
    "totalViews": 5000,
    "totalLikes": 250,
    "totalComments": 45,
    "totalShares": 15
  }
  ```
- **Tính toán:** Sum t?t c? các video c?a brand

#### GET `/api/reels/brand/{brandId}/profile` - Get Detailed Brand Profile
**Auth:** Optional
- **Params:** `brandId` (int)
- **Response:**
  ```json
  {
    "brandId": 1,
    "brandName": "Brand ABC",
    "ownerName": "Tên Ch? Brand",
    "followers": 120,
    "statistics": {
      "totalVideos": 10,
      "totalViews": 5000,
      "totalLikes": 250,
      "totalComments": 45,
      "totalShares": 15,
      "averageViews": 500.0,
      "averageLikes": 25.0
    },
    "recentVideos": [
      {
        "videoId": 1,
        "caption": "Video caption",
        "views": 100,
        "likes": 10,
        "commentsCount": 5,
        "shares": 2,
        "createdAt": "2025-01-08T10:30:00Z",
        "thumbnailUrl": "/Uploads/thumbnails/thumb_...jpg"
      }
    ]
  }
  ```
- **Chi ti?t:** 
  - Brand info + Owner name
  - T?ng followers (BrandFollows)
  - Th?ng kê chi ti?t videos
  - Top 5 video g?n nh?t

---

### ?? **VIDEO MANAGEMENT**

#### GET `/api/reels/feed?page=1&customerId=123` - Get Video Feed
**Auth:** Public
- **Features:**
  - Pagination (pageSize=10)
  - Filter by customerId (IsLiked flag)
  - Include Brand info + Products

#### GET `/api/reels/videos?page=1&pageSize=10` - Get All Videos (Admin/Staff)
**Auth:** Optional (v?i access control)

#### GET `/api/reels/videos/{videoId}` - Get Single Video
**Auth:** Optional (v?i access control)

#### POST `/api/reels/upload` - Upload Video
**Auth:** Public (d?a vào BrandId)
- **Params:** BrandId, VideoFile, ThumbnailFile, Caption
- **Constraints:** 
  - Video: .mp4, .webm, .mov, .avi (max 500MB)
  - Thumbnail: .jpg, .jpeg, .png (max 10MB)

#### PUT `/api/reels/{videoId}` - Update Video
**Auth:** `[Authorize(Roles = "Brand")]`
- **Ch? update Caption**

#### DELETE `/api/reels/{videoId}` - Delete Video
**Auth:** `[Authorize(Roles = "Brand")]`
- **Xóa:** Files + VideoLikes + VideoComments + VideoProducts

#### GET `/api/reels/brand/my-videos?page=1` - Get My Videos (Brand)
**Auth:** `[Authorize(Roles = "Brand")]`

---

## ??? **DATABASE TABLES**

### VideoLike
```
PrimaryKey: (VideoId, CustomerId)
- VideoId: int (FK -> ShortVideo)
- CustomerId: int (FK -> Customer)
- LikedAt: DateTime
```

### VideoComment
```
PrimaryKey: (VideoId, CustomerId)
- VideoId: int (FK -> ShortVideo)
- CustomerId: int (FK -> Customer)
- CustomerName: string
- Content: string
- CreatedAt: DateTime
```

### VideoView
```
PrimaryKey: (VideoId, CustomerId)
- VideoId: int (FK -> ShortVideo)
- CustomerId: int (FK -> Customer)
- ViewedAt: DateTime
```

### VideoShare
```
PrimaryKey: (VideoId, CustomerId)
- VideoId: int (FK -> ShortVideo)
- CustomerId: int (FK -> Customer)
- SharedAt: DateTime
```

### BrandFollow
```
PrimaryKey: (BrandId, CustomerId)
- BrandId: int (FK -> Brand)
- CustomerId: int (FK -> Customer)
```

---

## ? **TÍNH N?NG HOÀN THÀNH**

?? Like/Unlike videos (toggle)
?? Comment videos (composite key tránh duplicate)
?? Delete comments (authorization)
?? View videos (tracking per customer)
?? Share videos (tracking per customer)
?? Follow/Unfollow brands
?? Brand stats & detailed profile
?? All with proper authorization & validation

---

## ?? **TESTING STEPS**

1. **Login Customer:** `POST /api/auth/loginCustomer`
   - L?y token có role "Customer"

2. **Test Like:** `POST /api/reels/{videoId}/like`
   - L?p l?i: s? unlike (toggle)

3. **Test Comment:** `POST /api/reels/{videoId}/comment`
   - Body: `{ "content": "Comment text" }`

4. **Test View:** `POST /api/reels/{videoId}/view`
   - L?n ??u: isNewView=true, Views t?ng
   - L?n 2: isNewView=false, Views không t?ng

5. **Test Share:** `POST /api/reels/{videoId}/share`
   - Shares luôn t?ng

6. **Test Profile:** `GET /api/reels/brand/{brandId}/profile`
   - Xem stats chi ti?t + recent videos
