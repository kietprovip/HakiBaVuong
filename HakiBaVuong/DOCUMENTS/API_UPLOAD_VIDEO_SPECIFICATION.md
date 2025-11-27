# ?? API Upload Video - Frontend Implementation Guide

## ?? T?ng Quan

**Endpoint**: `POST /api/reels/upload`  
**Authentication**: Required (JWT Token - Brand Role)  
**Content-Type**: `multipart/form-data`

---

## ?? Request Specification

### Headers
```
Authorization: Bearer {jwt_token}
Content-Type: multipart/form-data
```

### Form Data Parameters

| Parameter | Type | Required | Description | Constraints |
|-----------|------|----------|-------------|------------|
| **BrandId** | integer | ? YES | ID c?a brand ???c ch?n | Ph?i thu?c quy?n s? h?u c?a user |
| **VideoFile** | file | ? YES | File video ?? upload | `.mp4`, `.webm`, `.mov`, `.avi` - Max 500MB |
| **ThumbnailFile** | file | ? YES | ?nh thumbnail | `.jpg`, `.jpeg`, `.png` - Max 10MB |
| **Caption** | string | ? NO | Mô t? video | Max 500 characters |

---

## ?? Flow Diagram

```
???????????????????????????????????????
?  1. User Login ? Get JWT Token      ?
???????????????????????????????????????
                  ?
???????????????????????????????????????
?  2. Fetch User's Brands             ?
?     GET /api/auth/user-brands       ?
?     Response: [BrandId, Name, ...]  ?
???????????????????????????????????????
                  ?
???????????????????????????????????????
?  3. User Selects Brand & Files      ?
?     - Choose Brand Dropdown         ?
?     - Select Video File             ?
?     - Select Thumbnail File         ?
?     - Enter Caption (optional)      ?
???????????????????????????????????????
                  ?
???????????????????????????????????????
?  4. Upload to Server                ?
?     POST /api/reels/upload          ?
?     FormData: {BrandId, files, ...} ?
???????????????????????????????????????
                  ?
        ?????????????????????
        ?                   ?
    ? SUCCESS          ? ERROR
        ?                   ?
        ?                   ?
    200 OK          400/403/500
    VideoId         Error Message
```

---

## ?? Frontend Implementation

### Step 1: Fetch User's Brands
```javascript
async function getUserBrands() {
  try {
    const response = await fetch('http://localhost:7258/api/auth/user-info', {
      headers: {
        'Authorization': `Bearer ${jwtToken}`
      }
    });
    
    if (!response.ok) throw new Error('Failed to fetch user info');
    
    const userData = await response.json();
    // userData contains userId, email, role, brandId
    
    // Fetch all brands owned by this user
    // NOTE: This endpoint may need to be created if not exists
    const brandsResponse = await fetch(
      `http://localhost:7258/api/brands/my-brands`,
      {
        headers: {
          'Authorization': `Bearer ${jwtToken}`
        }
      }
    );
    
    const brands = await brandsResponse.json();
    return brands; // [{ brandId: 1, name: "Brand A" }, ...]
  } catch (error) {
    console.error('Error fetching brands:', error);
    return [];
  }
}
```

### Step 2: Create Upload Form HTML

```html
<div class="upload-container">
  <!-- Brand Selector -->
  <div class="form-group">
    <label>Ch?n Brand *</label>
    <select id="brandSelect" required>
      <option value="">-- Ch?n Brand --</option>
    </select>
  </div>

  <!-- Video File Input -->
  <div class="form-group">
    <label>Video File (mp4, webm, mov, avi) - Max 500MB *</label>
    <input type="file" id="videoInput" accept=".mp4,.webm,.mov,.avi" required />
    <small>H? tr?: MP4, WebM, MOV, AVI</small>
  </div>

  <!-- Thumbnail Input -->
  <div class="form-group">
    <label>Thumbnail (jpg, jpeg, png) - Max 10MB *</label>
    <input type="file" id="thumbnailInput" accept=".jpg,.jpeg,.png" required />
    <small>H? tr?: JPG, PNG</small>
  </div>

  <!-- Caption Input -->
  <div class="form-group">
    <label>Mô t? (Tùy ch?n)</label>
    <textarea id="captionInput" placeholder="Nh?p mô t? video..."></textarea>
  </div>

  <!-- Upload Button -->
  <button id="uploadBtn" onclick="handleUpload()">??ng Video</button>
</div>
```

### Step 3: Handle Upload Function

```javascript
async function handleUpload() {
  const brandId = document.getElementById('brandSelect').value;
  const videoFile = document.getElementById('videoInput').files[0];
  const thumbnailFile = document.getElementById('thumbnailInput').files[0];
  const caption = document.getElementById('captionInput').value;

  // ===== VALIDATION =====
  if (!brandId) {
    alert('Vui lòng ch?n brand');
    return;
  }

  if (!videoFile) {
    alert('Vui lòng ch?n video file');
    return;
  }

  if (!thumbnailFile) {
    alert('Vui lòng ch?n thumbnail file');
    return;
  }

  // Validate file sizes
  const maxVideoSize = 500 * 1024 * 1024; // 500MB
  const maxThumbnailSize = 10 * 1024 * 1024; // 10MB

  if (videoFile.size > maxVideoSize) {
    alert('Video file quá l?n (max 500MB)');
    return;
  }

  if (thumbnailFile.size > maxThumbnailSize) {
    alert('Thumbnail file quá l?n (max 10MB)');
    return;
  }

  // ===== CREATE FORM DATA =====
  const formData = new FormData();
  formData.append('BrandId', brandId);
  formData.append('VideoFile', videoFile);
  formData.append('ThumbnailFile', thumbnailFile);
  formData.append('Caption', caption);

  try {
    // ===== UPLOAD =====
    const response = await fetch('http://localhost:7258/api/reels/upload', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${jwtToken}`
        // DON'T set Content-Type, let browser set it with boundary
      },
      body: formData
    });

    const result = await response.json();

    // ===== HANDLE RESPONSE =====
    if (response.ok) {
      alert('Upload thành công!');
      console.log('Video ID:', result.videoId);
      
      // Reset form
      document.getElementById('videoInput').value = '';
      document.getElementById('thumbnailInput').value = '';
      document.getElementById('captionInput').value = '';
      
      // Redirect ho?c refresh video list
      // window.location.href = '/my-videos';
    } else {
      alert(`L?i: ${result.message}`);
    }
  } catch (error) {
    console.error('Upload error:', error);
    alert('Có l?i x?y ra khi upload');
  }
}
```

---

## ?? Response Examples

### ? Success Response (200 OK)
```json
{
  "videoId": 42,
  "brandId": 1,
  "message": "??ng video thành công!"
}
```

### ? Error Responses

**400 Bad Request - Missing Files**
```json
{
  "message": "Video file không ???c ?? tr?ng."
}
```

**400 Bad Request - Invalid Format**
```json
{
  "message": "??nh d?ng video không h? tr?. Ch? h? tr?: .mp4, .webm, .mov, .avi"
}
```

**400 Bad Request - File Too Large**
```json
{
  "message": "Kích th??c video v??t quá 500MB."
}
```

**401 Unauthorized**
```json
{
  "message": "Token không h?p l? ho?c ?ã h?t h?n"
}
```

**403 Forbidden - Invalid Brand Permission**
```json
{
  "message": "B?n không có quy?n upload video cho brand này."
}
```

**404 Not Found - Brand Doesn't Exist**
```json
{
  "message": "Brand không t?n t?i."
}
```

**500 Internal Server Error**
```json
{
  "message": "L?i khi ??ng video.",
  "error": "Detailed error message"
}
```

---

## ?? UI/UX Recommendations

### Upload Progress
```javascript
// Show upload progress
videoInput.addEventListener('change', (e) => {
  const file = e.target.files[0];
  if (file) {
    const sizeMB = (file.size / (1024 * 1024)).toFixed(2);
    document.getElementById('videoSize').innerText = `Size: ${sizeMB} MB`;
  }
});
```

### Disable Button During Upload
```javascript
async function handleUpload() {
  const btn = document.getElementById('uploadBtn');
  btn.disabled = true;
  btn.innerText = '?ang upload...';

  try {
    // ... upload logic ...
  } finally {
    btn.disabled = false;
    btn.innerText = '??ng Video';
  }
}
```

### Show Loading Spinner
```javascript
function showLoading() {
  document.getElementById('loadingSpinner').style.display = 'block';
}

function hideLoading() {
  document.getElementById('loadingSpinner').style.display = 'none';
}
```

---

## ?? Security Checklist

- ? Always validate files on **client-side**
  - Check file type by extension
  - Check file size
- ? Server validates again (already done)
- ? Token must be valid & not expired
- ? User must own the selected brand (server checks)
- ? Files are saved in protected `/Uploads` folder (configure in web server)

---

## ?? Advanced Features (Future)

1. **Drag & Drop Upload**
   ```javascript
   container.addEventListener('dragover', (e) => {
     e.preventDefault();
     container.classList.add('drag-over');
   });

   container.addEventListener('drop', (e) => {
     e.preventDefault();
     const files = e.dataTransfer.files;
     // Handle files
   });
   ```

2. **Resume Failed Upload**
   - Store BrandId in sessionStorage
   - Detect and resume on page refresh

3. **Batch Upload**
   - Allow multiple videos at once
   - Show upload queue

4. **Auto-Generate Thumbnail**
   - Extract first frame from video
   - Allow user to adjust

---

## ?? Support & Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| 403 Forbidden | Wrong BrandId or no ownership | Check brand list is correct |
| 400 Bad Request | Invalid file format | Ensure .mp4, .jpg files |
| Network Timeout | Large file upload too slow | Increase timeout or split upload |
| CORS Error | Frontend domain blocked | Check CORS config on backend |

---

## ?? API Endpoint Summary

| Method | Endpoint | Auth | Purpose |
|--------|----------|------|---------|
| POST | `/api/reels/upload` | ? Brand | Upload video |
| GET | `/api/reels/feed` | ? No | Get video feed |
| GET | `/api/reels/brand/my-videos` | ? Brand | Get my videos |
| PUT | `/api/reels/{id}` | ? Brand | Update caption |
| DELETE | `/api/reels/{id}` | ? Brand | Delete video |
