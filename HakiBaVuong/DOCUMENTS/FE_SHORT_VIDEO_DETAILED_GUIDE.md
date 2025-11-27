# ?? H??NG D?N CHI TI?T - SHORT VIDEO (REELS) FEATURE

---

## ?? T?NG QUAN FEATURE

**Short Video (Reels)** là tính n?ng cho phép:
- ?? **Customer**: Xem video d?c ki?u TikTok, like, comment, chia s?, follow brand
- ?? **Brand**: ??ng video, g?n th? s?n ph?m, qu?n lý video (ch?nh s?a, xóa)

---

## ?? KI?N TRÚC & C? CH?

### **Database Structure (5 b?ng chính)**

```
???????????????????????????????????????????????????????????????
?                     ShortVideos (Video chính)                ?
???????????????????????????????????????????????????????????????
? VideoId (PK)         ? INT           ? Khóa chính          ?
? BrandId (FK)         ? INT           ? Liên k?t Brand      ?
? VideoUrl             ? NVARCHAR(MAX) ? ???ng d?n video     ?
? ThumbnailUrl         ? NVARCHAR(MAX) ? Hình ?nh ??i di?n   ?
? Caption              ? NVARCHAR(MAX) ? Mô t? video         ?
? Views                ? INT           ? S? l??t xem (0-...) ?
? Likes                ? INT           ? S? like (0-...)     ?
? CommentsCount        ? INT           ? S? bình lu?n        ?
? Shares               ? INT           ? S? chia s?          ?
? CreatedAt            ? DATETIME      ? Ngày t?o            ?
???????????????????????????????????????????????????????????????

???????????????????????????????????????????????????????????????
?              VideoProducts (S?n ph?m g?n th?)               ?
???????????????????????????????????????????????????????????????
? Id (PK)              ? INT           ? Khóa chính          ?
? VideoId (FK)         ? INT           ? Liên k?t ShortVideo ?
? ProductId (FK)       ? INT           ? Liên k?t Product    ?
? X                    ? FLOAT         ? V? trí ngang (0-1)  ?
? Y                    ? FLOAT         ? V? trí d?c (0-1)    ?
???????????????????????????????????????????????????????????????

???????????????????????????????????????????????????????????????
?                 VideoLikes (Like video)                      ?
???????????????????????????????????????????????????????????????
? VideoId (PK, FK)     ? INT           ? Liên k?t ShortVideo ?
? CustomerId (PK, FK)  ? INT           ? Liên k?t Customer   ?
? LikedAt              ? DATETIME      ? Th?i gian like      ?
???????????????????????????????????????????????????????????????

???????????????????????????????????????????????????????????????
?               VideoComments (Bình lu?n)                      ?
???????????????????????????????????????????????????????????????
? CommentId (PK)       ? INT           ? Khóa chính          ?
? VideoId (FK)         ? INT           ? Liên k?t ShortVideo ?
? CustomerId (FK)      ? INT (NULL)    ? Liên k?t Customer   ?
? CustomerName         ? NVARCHAR(MAX) ? Tên hi?n th?        ?
? Content              ? NVARCHAR(MAX) ? N?i dung bình lu?n  ?
? CreatedAt            ? DATETIME      ? Th?i gian t?o       ?
???????????????????????????????????????????????????????????????

???????????????????????????????????????????????????????????????
?               BrandFollows (Follow brand)                    ?
???????????????????????????????????????????????????????????????
? BrandId (PK, FK)     ? INT           ? Liên k?t Brand      ?
? CustomerId (PK, FK)  ? INT           ? Liên k?t Customer   ?
? FollowedAt           ? DATETIME      ? Th?i gian follow    ?
???????????????????????????????????????????????????????????????
```

---

## ?? API ENDPOINTS - CHI TI?T

### **A. PUBLIC API (Không c?n ??ng nh?p)**

#### **1. L?y Feed Video - Trang ch?**
```
GET /api/reels/feed?page=1&customerId=123
```

**M?c ?ích:** L?y danh sách video ?? hi?n th? feed, pagination 10 video/trang

**Parameters:**
- `page` (query): S? trang (b?t ??u t? 1)
- `customerId` (query, optional): ID customer ?? check tr?ng thái like

**Response 200 OK:**
```json
[
  {
    "videoId": 1,
    "videoUrl": "/Uploads/videos/video_123456789.mp4",
    "thumbnailUrl": "/Uploads/thumbnails/thumb_123456789.jpg",
    "caption": "S?n ph?m hot tháng 12",
    "views": 1500,
    "likes": 320,
    "commentsCount": 45,
    "shares": 12,
    "createdAt": "2024-11-27T10:00:00",
    "brand": {
      "brandId": 5,
      "name": "Nike Store"
    },
    "products": [
      {
        "productId": 101,
        "name": "Running Shoes Pro",
        "priceSell": 450000,
        "image": "/images/products/shoe.jpg",
        "x": 0.45,    // 45% t? trái sang
        "y": 0.55     // 55% t? trên xu?ng
      }
    ],
    "isLiked": false  // true n?u customerId ?ã like video này
  }
]
```

**Logic phía BE:**
```
1. L?y video t? DB, sort theo ngày m?i nh?t
2. Phân trang: Skip((page-1)*10), Take(10)
3. Include Brand info + VideoProducts + Product info
4. Check xem customerId ?ã like video ch?a (isLiked field)
5. Tr? v? list video
```

---

#### **2. Xem Chi Ti?t Video**
```
GET /api/reels/{videoId}/details?customerId=123
```

**M?c ?ích:** Xem toàn b? thông tin video (dùng khi click vào video)

**Response:** Gi?ng feed nh?ng ch? 1 video

---

#### **3. L?y Danh Sách Bình Lu?n**
```
GET /api/reels/{videoId}/comments?page=1
```

**M?c ?ích:** L?y danh sách bình lu?n c?a video, 10 comment/trang

**Response 200 OK:**
```json
[
  {
    "commentId": 1,
    "videoId": 1,
    "customerId": 123,
    "customerName": "Nguy?n V?n A",
    "content": "Quá tuy?t v?i! S?n ph?m ch?t l??ng!",
    "createdAt": "2024-11-27T12:30:00"
  },
  {
    "commentId": 2,
    "videoId": 1,
    "customerId": 124,
    "customerName": "Tr?n Th? B",
    "content": "?ã order r?i, ch? ship",
    "createdAt": "2024-11-27T12:25:00"
  }
]
```

---

#### **4. L?y S?n Ph?m c?a Brand (?? g?n th?)**
```
GET /api/reels/brand/{brandId}/products?page=1
```

**M?c ?ích:** L?y list s?n ph?m c?a brand, dùng khi brand upload video (g?n th? s?n ph?m)

**Response 200 OK:**
```json
[
  {
    "productId": 101,
    "name": "Running Shoes Pro",
    "priceSell": 450000,
    "image": "/images/products/shoe.jpg"
  },
  {
    "productId": 102,
    "name": "Sports Shirt",
    "priceSell": 250000,
    "image": "/images/products/shirt.jpg"
  }
]
```

**Phân trang:** 20 s?n ph?m/trang

---

#### **5. Ghi Nh?n L??t Share**
```
POST /api/reels/{videoId}/share
```

**M?c ?ích:** Khi user click nút "Share", t?ng counter Shares lên 1

**Body:** (tr?ng)

**Response 200 OK:**
```json
{
  "message": "Chia s? video thành công!"
}
```

**Logic:** `Shares += 1`

---

### **B. CUSTOMER API (C?n ??ng nh?p, Role=Customer)**

#### **1. Like/Unlike Video (Toggle)**
```
POST /api/reels/{videoId}/like
Headers: Authorization: Bearer {customer_token}
```

**M?c ?ích:** Like ho?c unlike video (g?i API l?n 2 s? unlike)

**Body:** (tr?ng)

**Response 200 OK:** (không tr? v? gì, ch? thành công/th?t b?i)

**Logic phía BE:**
```csharp
var existing = DB.VideoLikes.Find(videoId, customerId);

if (existing == null) {
  // Ch?a like ? Thêm like
  ADD VideoLike(videoId, customerId)
  Likes += 1
} else {
  // ?ã like ? Xóa like
  DELETE VideoLike(videoId, customerId)
  Likes -= 1
}
```

**FE Implementation:**
```javascript
let isLiked = false; // State

function toggleLike(videoId) {
  fetch(`/api/reels/${videoId}/like`, {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${customerToken}`
    }
  })
  .then(() => {
    isLiked = !isLiked;  // Toggle state
    updateLikeButton();   // C?p nh?t UI
  })
  .catch(err => {
    if (err.status === 401) window.location.href = '/login';
  });
}
```

---

#### **2. Bình Lu?n Video**
```
POST /api/reels/{videoId}/comment
Headers: Authorization: Bearer {customer_token}
Content-Type: application/json

Body:
{
  "content": "Quá tuy?t v?i! S?n ph?m ch?t l??ng!"
}
```

**M?c ?ích:** Thêm bình lu?n vào video

**Response 200 OK:**
```json
{
  "commentId": 45,
  "videoId": 1,
  "customerId": 123,
  "customerName": "Nguy?n V?n A",
  "content": "Quá tuy?t v?i! S?n ph?m ch?t l??ng!",
  "createdAt": "2024-11-27T14:30:00"
}
```

**Logic phía BE:**
```csharp
// 1. L?y customer ID t? token
var customerId = User.FindFirst("CustomerId")!.Value;

// 2. L?y tên customer t? DB
var customer = DB.Customers.Find(customerId);

// 3. T?o comment m?i
var comment = new VideoComment {
  VideoId = videoId,
  CustomerId = customerId,
  CustomerName = customer.Name,
  Content = dto.Content,
  CreatedAt = DateTime.UtcNow
};

// 4. L?u comment
DB.VideoComments.Add(comment);

// 5. T?ng CommentsCount c?a video
ShortVideo.CommentsCount += 1;

// 6. Tr? v? comment m?i (FE hi?n th? ngay mà không c?n refetch)
return comment;
```

---

#### **3. Follow Brand**
```
POST /api/reels/brand/{brandId}/follow
Headers: Authorization: Bearer {customer_token}
```

**M?c ?ích:** Customer follow brand ?? nh?n thông báo video m?i

**Body:** (tr?ng)

**Response 200 OK:**
```json
{
  "message": "Theo dõi brand thành công!"
}
```

**Logic phía BE:**
```csharp
var existing = DB.BrandFollows.Find(brandId, customerId);

if (existing != null)
  return BadRequest("?ã follow r?i");

DB.BrandFollows.Add(new BrandFollow {
  BrandId = brandId,
  CustomerId = customerId,
  FollowedAt = DateTime.UtcNow
});
DB.SaveChanges();
```

---

#### **4. Unfollow Brand**
```
DELETE /api/reels/brand/{brandId}/unfollow
Headers: Authorization: Bearer {customer_token}
```

**M?c ?ích:** H?y follow brand

**Response 200 OK:**
```json
{
  "message": "H?y theo dõi thành công!"
}
```

---

### **C. BRAND API (C?n ??ng nh?p, Role=Brand)**

#### **1. ??ng Video (Upload)**
```
POST /api/reels/upload
Headers: Authorization: Bearer {brand_token}
Content-Type: multipart/form-data

Form Data:
- videoFile: File (video.mp4)
- thumbnailFile: File (thumbnail.jpg)
- caption: "Khuy?n mãi tháng 12"
- productTags: JSON string
  [
    { "productId": 101, "x": 0.45, "y": 0.55 },
    { "productId": 102, "x": 0.60, "y": 0.70 }
  ]
```

**M?c ?ích:** Brand upload video m?i

**Validation:**
- Video: .mp4, .webm, .mov, .avi | Max 500MB
- Thumbnail: .jpg, .jpeg, .png | Max 10MB

**Response 200 OK:**
```json
{
  "videoId": 1,
  "message": "??ng video thành công!"
}
```

**Logic phía BE:**
```csharp
// 1. Validate file
if (videoFile.Length > 500MB || !isValidFormat)
  return BadRequest("File không h?p l?");

// 2. L?u video file vào disk
var videoFileName = $"video_{DateTime.UtcNow.Ticks}.mp4";
SaveFile(videoFile, $"/Uploads/videos/{videoFileName}");

// 3. L?u thumbnail file vào disk
var thumbnailFileName = $"thumb_{DateTime.UtcNow.Ticks}.jpg";
SaveFile(thumbnailFile, $"/Uploads/thumbnails/{thumbnailFileName}");

// 4. T?o record ShortVideo
var video = new ShortVideo {
  BrandId = brandId,
  VideoUrl = $"/Uploads/videos/{videoFileName}",
  ThumbnailUrl = $"/Uploads/thumbnails/{thumbnailFileName}",
  Caption = caption,
  CreatedAt = DateTime.UtcNow,
  Views = 0,
  Likes = 0,
  CommentsCount = 0,
  Shares = 0
};
DB.ShortVideos.Add(video);
DB.SaveChanges();

// 5. Thêm product tags (n?u có)
foreach (var tag in productTags) {
  DB.VideoProducts.Add(new VideoProduct {
    VideoId = video.VideoId,
    ProductId = tag.ProductId,
    X = tag.X,
    Y = tag.Y
  });
}
DB.SaveChanges();

return video.VideoId;
```

**FE Implementation:**
```javascript
async function uploadVideo() {
  // 1. Ch?n file video
  const videoInput = document.getElementById('videoInput');
  const videoFile = videoInput.files[0];

  // 2. T?o thumbnail (c?t frame ??u tiên)
  const thumbnailFile = await generateThumbnail(videoFile);

  // 3. L?y list s?n ph?m c?a brand
  const products = await fetch(`/api/reels/brand/${brandId}/products`)
    .then(r => r.json());

  // 4. Brand ch?n s?n ph?m + click v? trí trên video
  const selectedProducts = [];
  video.addEventListener('click', (e) => {
    const rect = video.getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width;  // 0-1
    const y = (e.clientY - rect.top) / rect.height;  // 0-1
    
    selectedProducts.push({
      productId: selectedProductId,
      x: x,
      y: y
    });
    drawTag(x, y); // V? tag trên video
  });

  // 5. G?i upload request
  const formData = new FormData();
  formData.append('videoFile', videoFile);
  formData.append('thumbnailFile', thumbnailFile);
  formData.append('caption', 'Khuy?n mãi');
  formData.append('productTags', JSON.stringify(selectedProducts));

  const response = await fetch('/api/reels/upload', {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${brandToken}` },
    body: formData
  });

  const result = await response.json();
  console.log('Video ID:', result.videoId);
}
```

---

#### **2. L?y Danh Sách Video c?a Brand**
```
GET /api/reels/brand/my-videos?page=1
Headers: Authorization: Bearer {brand_token}
```

**M?c ?ích:** Brand xem video c?a mình (?? ch?nh s?a, xóa)

**Response 200 OK:**
```json
[
  {
    "videoId": 1,
    "videoUrl": "/Uploads/videos/video_123456789.mp4",
    "thumbnailUrl": "/Uploads/thumbnails/thumb_123456789.jpg",
    "caption": "Khuy?n mãi tháng 12",
    "views": 1500,
    "likes": 320,
    "commentsCount": 45,
    "shares": 12,
    "createdAt": "2024-11-27T10:00:00",
    "products": [
      {
        "productId": 101,
        "name": "Running Shoes",
        "priceSell": 450000,
        "x": 0.45,
        "y": 0.55
      }
    ]
  }
]
```

---

#### **3. Ch?nh S?a Caption**
```
PUT /api/reels/{videoId}
Headers: Authorization: Bearer {brand_token}
Content-Type: application/json

Body:
{
  "caption": "Caption m?i - Khuy?n mãi m?i"
}
```

**M?c ?ích:** S?a mô t? video

**Response 200 OK:**
```json
{
  "message": "C?p nh?t video thành công!"
}
```

---

#### **4. Xóa Video**
```
DELETE /api/reels/{videoId}
Headers: Authorization: Bearer {brand_token}
```

**M?c ?ích:** Xóa video (xóa file, xóa DB records)

**Response 200 OK:**
```json
{
  "message": "Xóa video thành công!"
}
```

**Logic phía BE:**
```csharp
var video = DB.ShortVideos
  .Include(v => v.VideoProducts)
  .Include(v => v.VideoLikes)
  .Include(v => v.VideoComments)
  .Find(videoId);

// Ki?m tra quy?n ownership
if (video.BrandId != brandId)
  return Forbid("Không ph?i video c?a b?n");

// Xóa file t? disk
DeleteFile(video.VideoUrl);
DeleteFile(video.ThumbnailUrl);

// Xóa DB records
DB.VideoProducts.RemoveRange(video.VideoProducts);
DB.VideoLikes.RemoveRange(video.VideoLikes);
DB.VideoComments.RemoveRange(video.VideoComments);
DB.ShortVideos.Remove(video);
DB.SaveChanges();
```

---

## ?? FE FLOW - CUSTOMER VIEW

### **Flow: Xem Video Feed**
```
1. FE Load ? GET /api/reels/feed?page=1
   ?
2. BE tr? v? list 10 videos + isLiked status
   ?
3. FE render video player (vertical 9:16)
   ?
4. FE hi?n th?:
   - Thumbnail (poster)
   - Brand logo
   - Caption
   - Like button (heart icon)
   - Comment button
   - Share button
   - Follow button
   ?
5. User scroll ? Load trang ti?p theo
   GET /api/reels/feed?page=2
```

### **Flow: Like/Unlike Video**
```
1. User click heart icon
   ?
2. FE POST /api/reels/{id}/like (v?i token)
   ?
3. BE check:
   - User ?ã like ch?a?
   - N?u ch?a: ADD like, Likes += 1
   - N?u r?i: DELETE like, Likes -= 1
   ?
4. FE nh?n response ? Toggle heart (filled/unfilled)
   ?
5. FE update likes counter
```

### **Flow: Comment Video**
```
1. User click comment button ? M? comment form
   ?
2. User nh?p text ? Submit
   ?
3. FE POST /api/reels/{id}/comment { content }
   ?
4. BE t?o comment ? Tr? v? comment m?i
   ?
5. FE thêm comment vào list (optimistic)
   ?
6. (Optional) FE GET /api/reels/{id}/comments ?? load h?t
```

### **Flow: Follow Brand**
```
1. User click "Follow" button trên brand
   ?
2. FE POST /api/reels/brand/{brandId}/follow
   ?
3. BE add BrandFollow record
   ?
4. FE button thay ??i thành "Unfollow" + disable follow
```

---

## ?? FE FLOW - BRAND UPLOAD

### **Flow: Upload Video**
```
1. Brand vào trang upload
   ?
2. Upload video file
   ?
3. BE t?o thumbnail t? frame ??u
   (ho?c FE upload thumbnail riêng)
   ?
4. FE hi?n th? video preview
   ?
5. Brand nh?p caption
   ?
6. FE GET /api/reels/brand/{brandId}/products
   ? Hi?n th? list s?n ph?m
   ?
7. Brand ch?n s?n ph?m + click v? trí trên video
   ? V? tag trên video
   ?
8. Brand submit
   ?
9. FE POST /api/reels/upload (multipart/form-data)
   ? Upload videoFile + thumbnailFile + caption + productTags
   ?
10. BE validate + l?u files + t?o DB records
    ?
11. FE nh?n videoId ? Redirect trang video
```

---

## ?? ERROR HANDLING

| Status | L?i | Nguyên nhân | Gi?i pháp FE |
|--------|-----|-----------|-------------|
| 401 | Unauthorized | Ch?a login/token h?t h?n | Redirect /login |
| 403 | Forbidden | Không có role/không ph?i owner | Hi?n th? error message |
| 400 | BadRequest | D? li?u không h?p l? | Validate tr??c khi g?i |
| 404 | NotFound | Video/brand không t?n t?i | Hi?n th? "Not found" page |
| 500 | Internal Error | L?i server | Retry ho?c liên h? support |

**FE x? lý:**
```javascript
.catch(err => {
  if (err.status === 401) {
    window.location.href = '/login';
  } else if (err.status === 403) {
    alert('B?n không có quy?n');
  } else if (err.status === 400) {
    alert('D? li?u không h?p l?');
  } else {
    alert('Có l?i x?y ra');
  }
});
```

---

## ?? LIBRARIES C?A FE C?N

```bash
# Video player
npm install react-player video-react

# Infinite scroll
npm install react-infinite-scroll-component

# Form handling
npm install react-hook-form

# HTTP client
npm install axios
# ho?c dùng Fetch API built-in

# State management
npm install zustand
# ho?c Redux

# File upload
npm install react-dropzone
```

---

## ?? TEST API - POSTMAN

**Collection URL:** Download `SHORT_VIDEO_API.postman_collection.json`

**Test flow:**
1. POST /api/reels/upload (brand upload video)
2. GET /api/reels/feed (xem feed)
3. POST /api/reels/{id}/like (like video)
4. POST /api/reels/{id}/comment (comment)
5. GET /api/reels/{id}/comments (xem comments)

---

## ?? FE CHECKLIST

### **Customer UI:**
- [ ] Video feed component (infinite scroll)
- [ ] Video player (vertical 9:16)
- [ ] Like button + counter
- [ ] Comment section
- [ ] Share button
- [ ] Follow brand button
- [ ] Product tags (clickable)
- [ ] Loading states
- [ ] Error messages
- [ ] Handle 401 ? login

### **Brand UI:**
- [ ] Upload form
- [ ] File validation
- [ ] Video preview
- [ ] Product selector
- [ ] Tag drawing (click/drag)
- [ ] Caption editor
- [ ] Video list
- [ ] Edit/delete modal
- [ ] Analytics dashboard
- [ ] Error handling

### **Server Files:**
- [ ] `/Uploads/videos/` (l?u video)
- [ ] `/Uploads/thumbnails/` (l?u thumbnail)
- [ ] `.gitignore` thêm `/Uploads/`

---

## ?? TIPS & BEST PRACTICES

### **FE Performance:**
- Lazy load video (ch? load khi scroll g?n)
- Cache API responses (pagination)
- Throttle like/comment requests
- Preload next page (prefetch)

### **Optimization:**
```javascript
// Lazy load video
<video key={video.videoId}>
  <source src={video.videoUrl} loading="lazy" />
</video>

// Prefetch next page
useEffect(() => {
  const nextPage = page + 1;
  fetch(`/api/reels/feed?page=${nextPage}`);
}, [page]);

// Debounce comment input
const [comment, setComment] = useState('');
const debouncedComment = useCallback(
  debounce((text) => setComment(text), 300),
  []
);
```

### **Video Format Validation FE:**
```javascript
function validateVideo(file) {
  const validFormats = ['video/mp4', 'video/webm'];
  if (!validFormats.includes(file.type)) {
    return '??nh d?ng video không h? tr?';
  }
  if (file.size > 500 * 1024 * 1024) {
    return 'Video quá l?n (max 500MB)';
  }
  return null;
}
```

---

## ?? MONITORING & LOGGING

**BE logs:**
- Upload video: `_logger.LogInformation("Video uploaded: {VideoId}")`
- Delete video: `_logger.LogInformation("Video deleted: {VideoId}")`
- Errors: `_logger.LogError(ex, "Error message")`

**FE logs:**
```javascript
console.log('API call:', response.status);
console.error('Error:', error);
```

---

**Bây gi? FE team có ?? thông tin ?? code nhé! ??**
