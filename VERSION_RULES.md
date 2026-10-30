# QUY TẮC ĐÁNH SỐ PHIÊN BẢN AVMAP (SEMANTIC VERSIONING)

Định dạng phiên bản chuẩn: `v[MAJOR].[MINOR].[PATCH]` (Ví dụ: `v1.2.0`)

---

### 1. Cập nhật nhỏ / Sửa lỗi (Patch - 1 chấm cuối cùng):
* **Quy tắc:** Tăng số cuối cùng, giữ nguyên số Major và Minor.
* **Áp dụng khi:**
  * Sửa lỗi (Bugfix), vá lỗi crash, khắc phục lỗi mất kết nối.
  * Tinh chỉnh giao diện nhỏ, cải thiện độ mượt, tối ưu hiệu năng.
  * Cập nhật tài liệu, hướng dẫn sử dụng.
* **Tiến trình mẫu:**
  $$\text{v1.2.0} \longrightarrow \text{v1.2.1} \longrightarrow \text{v1.2.2} \longrightarrow \text{v1.2.3} \dots$$

---

### 2. Thêm tính năng lớn mới (Minor - số ở giữa):
* **Quy tắc:** Tăng số ở giữa lên 1 đơn vị, đặt số cuối về 0.
* **Áp dụng khi:**
  * Thêm công cụ / module mới (VD: Thêm module trắc địa mới, thêm loại bản đồ mới, hỗ trợ định dạng file mới).
  * Nâng cấp tính năng lớn nhưng vẫn giữ nguyên cấu trúc phần mềm hiện hữu.
* **Tiến trình mẫu:**
  $$\text{v1.2.x} \longrightarrow \text{v1.3.0} \longrightarrow \text{v1.3.1} \longrightarrow \text{v1.3.2} \dots \longrightarrow \text{v1.4.0}$$

---

### 3. Thay đổi hoàn toàn (Major - số đầu tiên):
* **Quy tắc:** Tăng số đầu tiên lên 1 đơn vị, đặt cả hai số sau về 0.
* **Áp dụng khi:**
  * Đại tu toàn bộ kiến trúc lõi phần mềm (Overhaul).
  * Thay đổi toàn bộ giao diện, đổi sang nền tảng công nghệ mới.
  * Các thay đổi mang tính đột phá không còn tương thích ngược hoàn toàn với phiên bản cũ.
* **Tiến trình mẫu:**
  $$\text{v1.x.x} \longrightarrow \text{v2.0.0} \longrightarrow \text{v2.0.1} \dots \longrightarrow \text{v2.1.0}$$

---

### Các vị trí cần đồng bộ khi nâng phiên bản:
1. `version.json` (Trường `"version": "X.Y.Z"`)
2. `UpdateService.cs` (`public const string CurrentVersion = "X.Y.Z";`)
3. Chạy `BUILD_ALL.bat` để biên dịch cả AutoCAD và enjiCAD.
4. Chạy `UPDATE_GITHUB.bat` (Tự động nhận diện số phiên bản từ `version.json` để tạo Release).
