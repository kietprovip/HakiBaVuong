# ?? TÓM T?T NHANH - SHORT VIDEO API

---

## ?? QUICK REFERENCE - 9 C?U TRÚC API

| # | Endpoint | Method | Auth | M?c ?ích | Status |
|---|----------|--------|------|---------|--------|
| **1** | `/api/reels/feed?page=1&customerId=123` | GET | ? | Xem feed video | 200 |
| **2** | `/api/reels/upload` | POST | ?Brand | Upload video | 200 |
| **3** | `/api/reels/{id}/like` | POST | ?Cust | Like/Unlike toggle | 200 |
| **4** | `/api/reels/{id}/comment` | POST | ?Cust | Thêm bình lu?n | 200 |
| **5** | `/api/reels/{id}/comments?page=1` | GET | ? | L?y list comment | 200 |
| **6** | `/api/reels/{id}/share` | POST | ? | Ghi nh?n share | 200 |
| **7** | `/api/reels/brand/{brandId}/products?page=1` | GET | ? | L?y s?n ph?m ?? tag | 200 |
| **8** | `/api/reels/brand/my-videos?page=1` | GET | ?Brand | Xem video c?a brand | 200 |
| **9** | `/api/reels/{id}` | PUT/DELETE | ?Brand | Edit/Delete video | 200 |

---

## ?? CURL EXAMPLES - COPY PASTE LUÔN

### **1. Get Feed (Xem video)**
```bash
curl -X GET "http://localhost:5000/api/reels/feed?page=1&customerId=123"
```

**Response:**
```json
[
  {
    "videoId": 1,
    "videoUrl": "/Uploads/videos/video_123456789.mp4",
    "thumbnailUrl": "/Uploads/thumbnails/thumb_123456789.jpg",
    "caption": "Khuy?n mãi 50%",
    "views": 1500,
    "likes": 320,
    "commentsCount": 45,
    "shares": 12,
    "brand": { "brandId": 5, "name": "Nike" },
    "isLiked": false
  }
]
```

---

### **2. Upload Video (Brand)**
```bash
curl -X POST "http://localhost:5000/api/reels/upload" \
  -H "Authorization: Bearer {BRAND_TOKEN}" \
  -F "videoFile=@video.mp4" \
  -F "thumbnailFile=@thumbnail.jpg" \
  -F "caption=Khuy?n mãi" \
  -F 'productTags=[{"productId":101,"x":0.5,"y":0.6}]'
```

**Response:**
```json
{
  "videoId": 1,
  "message": "??ng video thành công!"
}
```

---

### **3. Like/Unlike Video**
```bash
curl -X POST "http://localhost:5000/api/reels/1/like" \
  -H "Authorization: Bearer {CUSTOMER_TOKEN}"
```

**Response:** 200 OK

**Logic:** G?i l?n th? 2 s? unlike (toggle t? ??ng)

---

### **4. Comment Video**
```bash
curl -X POST "http://localhost:5000/api/reels/1/comment" \
  -H "Authorization: Bearer {CUSTOMER_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"content":"Quá tuy?t v?i!"}'
```

**Response:**
```json
{
  "commentId": 1,
  "videoId": 1,
  "customerId": 123,
  "customerName": "Nguy?n V?n A",
  "content": "Quá tuy?t v?i!",
  "createdAt": "2024-11-27T12:30:00"
}
```

---

### **5. Get Comments**
```bash
curl -X GET "http://localhost:5000/api/reels/1/comments?page=1"
```

---

### **6. Share Video**
```bash
curl -X POST "http://localhost:5000/api/reels/1/share"
```

---

### **7. Get Brand Products (g?n th?)**
```bash
curl -X GET "http://localhost:5000/api/reels/brand/5/products?page=1"
```

**Response:**
```json
[
  {
    "productId": 101,
    "name": "Running Shoes",
    "priceSell": 450000,
    "image": "/images/products/shoe.jpg"
  }
]
```

---

### **8. Get My Videos (Brand)**
```bash
curl -X GET "http://localhost:5000/api/reels/brand/my-videos?page=1" \
  -H "Authorization: Bearer {BRAND_TOKEN}"
```

---

### **9. Edit Video Caption**
```bash
curl -X PUT "http://localhost:5000/api/reels/1" \
  -H "Authorization: Bearer {BRAND_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"caption":"Caption m?i"}'
```

---

### **10. Delete Video**
```bash
curl -X DELETE "http://localhost:5000/api/reels/1" \
  -H "Authorization: Bearer {BRAND_TOKEN}"
```

---

## ?? JAVASCRIPT FETCH EXAMPLES

### **Get Feed**
```javascript
async function getFeed(page = 1, customerId = null) {
  const url = new URL('http://localhost:5000/api/reels/feed');
  url.searchParams.set('page', page);
  if (customerId) url.searchParams.set('customerId', customerId);

  const response = await fetch(url);
  return response.json();
}

// Usage
const videos = await getFeed(1, 123);
```

---

### **Upload Video**
```javascript
async function uploadVideo(videoFile, thumbnailFile, caption, productTags) {
  const formData = new FormData();
  formData.append('videoFile', videoFile);
  formData.append('thumbnailFile', thumbnailFile);
  formData.append('caption', caption);
  formData.append('productTags', JSON.stringify(productTags));

  const response = await fetch('http://localhost:5000/api/reels/upload', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${brandToken}`
    },
    body: formData
  });

  return response.json();
}

// Usage
const result = await uploadVideo(
  videoFile,
  thumbnailFile,
  'Khuy?n mãi',
  [{ productId: 101, x: 0.5, y: 0.6 }]
);
console.log('Video ID:', result.videoId);
```

---

### **Like/Unlike**
```javascript
async function toggleLike(videoId) {
  const response = await fetch(`http://localhost:5000/api/reels/${videoId}/like`, {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${customerToken}`
    }
  });

  if (response.status === 401) {
    // Redirect login
    window.location.href = '/login';
  }

  return response.ok;
}

// Usage
await toggleLike(1);
```

---

### **Comment**
```javascript
async function addComment(videoId, content) {
  const response = await fetch(`http://localhost:5000/api/reels/${videoId}/comment`, {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${customerToken}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ content })
  });

  return response.json();
}

// Usage
const comment = await addComment(1, 'Tuy?t v?i!');
console.log('Comment:', comment.commentId);
```

---

### **Get Comments**
```javascript
async function getComments(videoId, page = 1) {
  const response = await fetch(
    `http://localhost:5000/api/reels/${videoId}/comments?page=${page}`
  );
  return response.json();
}

// Usage
const comments = await getComments(1, 1);
```

---

### **Share**
```javascript
async function shareVideo(videoId) {
  const response = await fetch(`http://localhost:5000/api/reels/${videoId}/share`, {
    method: 'POST'
  });

  return response.json();
}

// Usage
await shareVideo(1);
```

---

### **Get Brand Products**
```javascript
async function getBrandProducts(brandId, page = 1) {
  const response = await fetch(
    `http://localhost:5000/api/reels/brand/${brandId}/products?page=${page}`
  );
  return response.json();
}

// Usage
const products = await getBrandProducts(5, 1);
```

---

### **Get My Videos (Brand)**
```javascript
async function getMyVideos(page = 1) {
  const response = await fetch(
    `http://localhost:5000/api/reels/brand/my-videos?page=${page}`,
    {
      headers: {
        'Authorization': `Bearer ${brandToken}`
      }
    }
  );
  return response.json();
}

// Usage
const myVideos = await getMyVideos(1);
```

---

### **Edit Video**
```javascript
async function editVideo(videoId, caption) {
  const response = await fetch(`http://localhost:5000/api/reels/${videoId}`, {
    method: 'PUT',
    headers: {
      'Authorization': `Bearer ${brandToken}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({ caption })
  });

  return response.json();
}

// Usage
await editVideo(1, 'Caption m?i');
```

---

### **Delete Video**
```javascript
async function deleteVideo(videoId) {
  const response = await fetch(`http://localhost:5000/api/reels/${videoId}`, {
    method: 'DELETE',
    headers: {
      'Authorization': `Bearer ${brandToken}`
    }
  });

  return response.json();
}

// Usage
await deleteVideo(1);
```

---

## ?? FE COMPONENT STRUCTURE

### **Customer View Structure:**
```
ShortVideoFeed
??? VideoInfiniteScroll
?   ??? VideoCard
?   ?   ??? VideoPlayer (9:16)
?   ?   ??? BrandHeader
?   ?   ?   ??? BrandLogo
?   ?   ?   ??? BrandName
?   ?   ?   ??? FollowButton
?   ?   ??? VideoInfo
?   ?   ?   ??? Caption
?   ?   ?   ??? ProductTags
?   ?   ??? ActionBar
?   ?       ??? LikeButton
?   ?       ??? CommentButton
?   ?       ??? ShareButton
?   ?       ??? Counters (views, likes, shares)
?   ??? CommentSection (expandable)
?   ?   ??? CommentList
?   ?   ??? CommentForm
?   ??? LoadingIndicator
```

### **Brand Upload Structure:**
```
UploadVideoPage
??? FileUploadZone
?   ??? VideoFileInput
?   ??? ThumbnailFileInput
??? VideoPreview
??? CaptionInput
??? ProductSelector
?   ??? ProductList
?   ??? ProductTagger (click to tag)
??? TagPreview (hi?n th? tags trên video)
??? SubmitButton
```

---

## ?? COMMON ERRORS & FIX

| L?i | Nguyên nhân | Fix |
|-----|-----------|-----|
| 401 Unauthorized | Ch?a login | Redirect login page |
| 403 Forbidden | Không có role | Check user role |
| 400 Bad Request | File sai format | Validate tr??c upload |
| 404 Not Found | VideoId không t?n t?i | Ki?m tra ID |
| 500 Internal Server Error | L?i BE | Retry, log error |

**FE Error Handler:**
```javascript
async function handleAPIError(response) {
  switch (response.status) {
    case 401:
      window.location.href = '/login';
      break;
    case 403:
      alert('B?n không có quy?n');
      break;
    case 400:
      const error = await response.json();
      alert(error.message);
      break;
    case 404:
      alert('Không tìm th?y');
      break;
    default:
      alert('Có l?i x?y ra');
  }
}
```

---

## ?? TOKEN SETUP

**Token format:**
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Claims inside token:**
```
// Customer
{
  "CustomerId": 123,
  "Email": "customer@example.com",
  "Role": "Customer"
}

// Brand
{
  "BrandId": 5,
  "Email": "brand@example.com",
  "Role": "Brand"
}
```

**FE l?u token:**
```javascript
// Login success
localStorage.setItem('token', response.token);

// Use token
const token = localStorage.getItem('token');
headers: { 'Authorization': `Bearer ${token}` }

// Logout
localStorage.removeItem('token');
```

---

## ?? FILE STRUCTURE - SERVER

```
HakiBaVuong/
??? Controllers/
?   ??? ShortVideoController.cs ?
??? Models/
?   ??? ShortVideo.cs ?
?   ??? VideoProduct.cs ?
?   ??? VideoLike.cs ?
?   ??? VideoComment.cs ?
?   ??? BrandFollow.cs ?
??? Migrations/
?   ??? 20251127111910_video.cs ?
?   ??? 20251127114108_updatevideoshort.cs (empty)
??? Uploads/
?   ??? videos/ (t?o auto khi upload)
?   ??? thumbnails/ (t?o auto khi upload)
??? appsettings.json (config)
```

**Run migration:**
```bash
dotnet ef database update
```

---

## ?? QUICK START - FE

### **Step 1: Setup**
```javascript
const API_BASE_URL = 'http://localhost:5000';
const customerToken = localStorage.getItem('token');
const brandToken = localStorage.getItem('token');
```

### **Step 2: Fetch Feed**
```javascript
const videos = await getFeed(1, customerId);
videos.forEach(video => renderVideoCard(video));
```

### **Step 3: Handle Like**
```javascript
likeButton.onclick = () => toggleLike(videoId);
```

### **Step 4: Handle Comment**
```javascript
commentForm.onsubmit = (e) => {
  e.preventDefault();
  const content = commentInput.value;
  addComment(videoId, content);
  commentInput.value = '';
};
```

### **Step 5: Upload (Brand)**
```javascript
const result = await uploadVideo(videoFile, thumbnailFile, caption, tags);
redirectToVideo(result.videoId);
```

---

**Ready to code! ??**
