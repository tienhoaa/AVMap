# AVMap - Plugin Bản Đồ Nền, Street View 360° & GIS Chuyên Nghiệp Cho AutoCAD / enjiCAD

**AVMap** là bộ công cụ tích hợp bản đồ địa lý trực tiếp vào môi trường bản vẽ CAD (AutoCAD và enjiCAD 2026), hỗ trợ quy hoạch, khảo sát, thiết kế giao thông và hạ tầng kỹ thuật tại Việt Nam.

---

## 🌟 Tính Năng Nổi Bật

### 1. 🌐 Hệ Tọa Độ VN-2000 Chuẩn 63 Tỉnh Thành
* Tích hợp 67 hệ tọa độ VN-2000 múi chiếu 3° và 6° theo chuẩn Thông tư 25/2014/TT-BTNMT & Thông tư 02/2023/TT-BTNMT.
* Hộp thoại CRS trực quan: Tự động ghi nhớ và thể hiện rõ tỉnh thành / KTT đang áp dụng cho bản vẽ kèm thẻ trạng thái `[✔ ĐANG SỬ DỤNG]`.

### 2. 🗺️ Bản Đồ Nền Vệ Tinh Thời Gian Thực (`AVMAPON` / `AVMAPOFF`)
* Tự động nạp ảnh nền Google Maps (Vệ tinh, Giao thông, Địa hình, Hybrid), Esri World Imagery, OpenStreetMap.
* Cơ chế Debounce & Multi-threading siêu mượt, lia chuột và zoom đến đâu tự nạp đến đó mà không làm đơ giật CAD.

### 3. 👁️ Google Street View 360° Đồng Bộ Hai Chiều (`AVSTREETVIEW`)
* Click chọn một điểm trên bình đồ CAD để mở cửa sổ tương tác toàn cảnh 360° thực tế.
* Mũi tên định hướng trên CAD tự động xoay theo hướng camera của người dùng trong Street View.
* **Chụp ảnh hiện trường chuyên nghiệp:** Nút `[📸 Chụp Ảnh Hiện Trạng]` tự động dọn sạch 100% giao diện điều khiển của Google, xóa mờ vệt chữ chìm trên mặt đường, nhúng trực tiếp siêu dữ liệu **GPS EXIF WGS-84 & góc la bàn Heading** vào file ảnh.
* Tương thích hoàn toàn với tính năng chèn ảnh khảo sát `AVPHOTO` để đặt lại ảnh đúng vị trí trên tuyến đường CAD.

### 4. 🌍 Xuất & Nhập Google Earth KMZ (`AVKMZ` / `AVIMPORTKMZ`)
* **Xuất CAD sang KMZ:** Hỗ trợ 100% các đối tượng: Polyline, Line, Arc, Circle, Hatch, Text, MText, DBPoint và **BlockReference (Block tĩnh, Dynamic Block, Block thuộc tính Attributes)**.
* Tự động tạo ghim vị trí, bảng thuộc tính HTML chi tiết trong cửa sổ Balloon và rã nét vẽ xuống mặt đất vệ tinh.
* **Nhập KMZ vào CAD:** Chuyển đổi trực tiếp các ranh giới, tim tuyến từ Google Earth về tọa độ VN-2000.

### 5. 🏗️ Bình Đồ Vector & Mô Hình Địa Hình
* **Lấy Vector Bình Đồ (`AVGETVECTORDATA`):** Quét vùng chọn để tải nhà cửa, đường sá (có tên đường), sông suối ao hồ thành đối tượng CAD.
* **Đường Đồng Mức (`AVCONTOUR`):** Tải dữ liệu độ cao vệ tinh toàn cầu (DEM) và sinh đường đồng mức có gắn cao độ Elevation Z.
* **Chèn Ảnh Bay Chụp Ortho (`AVINSERTORTHO`):** Chèn ảnh trực giao Flycam/UAV (TIF, GeoTIFF, ECW, PNG, JPG) kèm World File (TFW, JGW).

### 6. ⚡ Tự Động Cập Nhật 1-Click (Fast Auto-Updater)
* Người dùng chỉ cần giải nén bộ cài một lần duy nhất lúc ban đầu.
* Khi có phiên bản mới, ngay sau khi `NETLOAD` hoặc trên thanh Ribbon sẽ xuất hiện thông báo `[🔔 CÓ BẢN MỚI]`.
* Nhấp cập nhật: Trình cập nhật tự động đóng CAD, tải DUY NHẤT các file thay đổi (~600 KB siêu nhanh) thay vì tải lại toàn bộ file ZIP, ghi đè trực tiếp vào thư mục cài đặt và tự động mở lại AutoCAD.

---

## 🛠️ Hướng Dẫn Biên Dịch & Cài Đặt

### 1. Biên dịch
1. Chạy file `BUILD_ALL.bat` để biên dịch tự động cả 2 phiên bản:
   * **AutoCAD** (.NET Framework 4.8)
   * **enjiCAD** (.NET 8.0 Windows)
2. Kết quả biên dịch và các gói phân phối độc lập sẽ được lưu tại:
   * `BAN_GIAO_AVMAP_BAN_MOI_AI\AutoCAD\`
   * `BAN_GIAO_AVMAP_BAN_MOI_AI\enjiCAD\`

### 2. Nạp vào CAD
Trong AutoCAD hoặc enjiCAD:
1. Gõ lệnh: `NETLOAD`
2. Chọn file `AVMap_AutoCAD_AI.dll` (hoặc `AVMap_enjiCAD_AI.dll`).
3. Thanh Ribbon **AVMap** sẽ tự động xuất hiện trên giao diện.

---

## 📜 Giấy Phép
Dự án được phát triển phục vụ công tác khảo sát, quy hoạch và thiết kế hạ tầng.
