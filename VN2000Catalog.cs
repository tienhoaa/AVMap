using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace AVMap
{
    public class ProvinceCrs
    {
        public int EpsgCode { get; set; }
        public string Name { get; set; }
        public string Province { get; set; }
        public double CentralMeridian { get; set; } // Kinh tuyến trục (độ)
        public double ScaleFactor { get; set; }     // 0.9999 (3 độ) hoặc 0.9996 (6 độ)
        public string ZoneText => (ScaleFactor == 0.9996 || Math.Abs(ScaleFactor - 0.9996) < 0.0001) ? "Múi 6° (TM-6)" : "Múi 3° (TM-3)";
        public string Description { get; set; }
        public bool IsActive { get; set; } = false;
        public string StatusText => IsActive ? "✔ Đang dùng" : "";

        public override string ToString()
        {
            return $"[{EpsgCode}] {Name} - {Province} (KTT: {CentralMeridian}°, {ZoneText})";
        }
    }

    public static class VN2000Catalog
    {
        // ==================== DANH MỤC TẤT CẢ HỆ TỌA ĐỘ VN-2000 ====================
        public static List<ProvinceCrs> Provinces = new List<ProvinceCrs>
        {
            // ========== 1. DANH MỤC 34 TỈNH THÀNH MỚI SAU SÁP NHẬP (THÔNG TƯ 24/2025/TT-BTNMT) ==========
            new ProvinceCrs { EpsgCode = 9263, Name = "VN-2000 / TM-3 TP. Hồ Chí Minh (Mới)", Province = "TP. Hồ Chí Minh (Mới)", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' (TT 24/2025: TP.HCM + Bà Rịa - Vũng Tàu + Bình Dương)" },
            new ProvinceCrs { EpsgCode = 9220, Name = "VN-2000 / TM-3 TP. Hà Nội (Mới)", Province = "TP. Hà Nội", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' (TT 24/2025: TP. Hà Nội)" },
            new ProvinceCrs { EpsgCode = 9232, Name = "VN-2000 / TM-3 TP. Đà Nẵng (Mới)", Province = "TP. Đà Nẵng (Mới)", CentralMeridian = 107.75, ScaleFactor = 0.9999, Description = "KTT 107°45' (TT 24/2025: Đà Nẵng + Quảng Nam)" },
            new ProvinceCrs { EpsgCode = 9217, Name = "VN-2000 / TM-3 TP. Hải Phòng (Mới)", Province = "TP. Hải Phòng (Mới)", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' (TT 24/2025: Hải Phòng + Hải Dương)" },
            new ProvinceCrs { EpsgCode = 9258, Name = "VN-2000 / TM-3 TP. Cần Thơ (Mới)", Province = "TP. Cần Thơ (Mới)", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' (TT 24/2025: Cần Thơ + Sóc Trăng + Hậu Giang)" },
            new ProvinceCrs { EpsgCode = 9231, Name = "VN-2000 / TM-3 TP. Huế (Mới)", Province = "TP. Huế (Trực thuộc TW)", CentralMeridian = 107.00, ScaleFactor = 0.9999, Description = "KTT 107°00' (TT 24/2025: TP. Huế)" },

            new ProvinceCrs { EpsgCode = 9254, Name = "VN-2000 / TM-3 Vĩnh Long (Mới)", Province = "Vĩnh Long (Mới)", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' (TT 24/2025: Vĩnh Long + Bến Tre + Trà Vinh)" },
            new ProvinceCrs { EpsgCode = 9241, Name = "VN-2000 / TM-3 Khánh Hòa (Mới)", Province = "Khánh Hòa (Mới)", CentralMeridian = 108.25, ScaleFactor = 0.9999, Description = "KTT 108°15' (TT 24/2025: Khánh Hòa + Ninh Thuận)" },
            new ProvinceCrs { EpsgCode = 9244, Name = "VN-2000 / TM-3 Lâm Đồng (Mới)", Province = "Lâm Đồng (Mới)", CentralMeridian = 107.75, ScaleFactor = 0.9999, Description = "KTT 107°45' (TT 24/2025: Lâm Đồng + Đắk Nông + Bình Thuận)" },
            new ProvinceCrs { EpsgCode = 9215, Name = "VN-2000 / TM-3 Bắc Ninh (Mới)", Province = "Bắc Ninh (Mới)", CentralMeridian = 107.00, ScaleFactor = 0.9999, Description = "KTT 107°00' (TT 24/2025: Bắc Ninh + Bắc Giang)" },
            new ProvinceCrs { EpsgCode = 9219, Name = "VN-2000 / TM-3 Hưng Yên (Mới)", Province = "Hưng Yên (Mới)", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' (TT 24/2025: Hưng Yên + Thái Bình)" },
            new ProvinceCrs { EpsgCode = 9225, Name = "VN-2000 / TM-3 Ninh Bình (Mới)", Province = "Ninh Bình (Mới)", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' (TT 24/2025: Ninh Bình + Hà Nam + Nam Định)" },
            new ProvinceCrs { EpsgCode = 9208, Name = "VN-2000 / TM-3 Phú Thọ (Mới)", Province = "Phú Thọ (Mới)", CentralMeridian = 104.75, ScaleFactor = 0.9999, Description = "KTT 104°45' (TT 24/2025: Phú Thọ + Vĩnh Phúc + Hòa Bình)" },
            new ProvinceCrs { EpsgCode = 9234, Name = "VN-2000 / TM-3 Quảng Ngãi (Mới)", Province = "Quảng Ngãi (Mới)", CentralMeridian = 108.00, ScaleFactor = 0.9999, Description = "KTT 108°00' (TT 24/2025: Quảng Ngãi + Kon Tum)" },
            new ProvinceCrs { EpsgCode = 9230, Name = "VN-2000 / TM-3 Quảng Trị (Mới)", Province = "Quảng Trị (Mới)", CentralMeridian = 106.00, ScaleFactor = 0.9999, Description = "KTT 106°00' (TT 24/2025: Quảng Trị + Quảng Bình)" },
            new ProvinceCrs { EpsgCode = 9249, Name = "VN-2000 / TM-3 Tây Ninh (Mới)", Province = "Tây Ninh (Mới)", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' (TT 24/2025: Tây Ninh + Long An)" },
            new ProvinceCrs { EpsgCode = 9213, Name = "VN-2000 / TM-3 Thái Nguyên (Mới)", Province = "Thái Nguyên (Mới)", CentralMeridian = 106.50, ScaleFactor = 0.9999, Description = "KTT 106°30' (TT 24/2025: Thái Nguyên + Bắc Kạn)" },
            new ProvinceCrs { EpsgCode = 9207, Name = "VN-2000 / TM-3 Tuyên Quang (Mới)", Province = "Tuyên Quang (Mới)", CentralMeridian = 106.00, ScaleFactor = 0.9999, Description = "KTT 106°00' (TT 24/2025: Tuyên Quang + Hà Giang)" },
            new ProvinceCrs { EpsgCode = 9204, Name = "VN-2000 / TM-3 Lào Cai (Mới)", Province = "Lào Cai (Mới)", CentralMeridian = 104.75, ScaleFactor = 0.9999, Description = "KTT 104°45' (TT 24/2025: Lào Cai + Yên Bái)" },

            new ProvinceCrs { EpsgCode = 9256, Name = "VN-2000 / TM-3 An Giang", Province = "An Giang", CentralMeridian = 104.75, ScaleFactor = 0.9999, Description = "KTT 104°45' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9262, Name = "VN-2000 / TM-3 Cà Mau", Province = "Cà Mau", CentralMeridian = 104.50, ScaleFactor = 0.9999, Description = "KTT 104°30' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9210, Name = "VN-2000 / TM-3 Cao Bằng", Province = "Cao Bằng", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9238, Name = "VN-2000 / TM-3 Đắk Lắk", Province = "Đắk Lắk", CentralMeridian = 108.50, ScaleFactor = 0.9999, Description = "KTT 108°30' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9202, Name = "VN-2000 / TM-3 Điện Biên", Province = "Điện Biên", CentralMeridian = 103.00, ScaleFactor = 0.9999, Description = "KTT 103°00' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9247, Name = "VN-2000 / TM-3 Đồng Nai", Province = "Đồng Nai", CentralMeridian = 107.75, ScaleFactor = 0.9999, Description = "KTT 107°45' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9253, Name = "VN-2000 / TM-3 Đồng Tháp", Province = "Đồng Tháp", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9237, Name = "VN-2000 / TM-3 Gia Lai", Province = "Gia Lai", CentralMeridian = 108.25, ScaleFactor = 0.9999, Description = "KTT 108°15' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9228, Name = "VN-2000 / TM-3 Hà Tĩnh", Province = "Hà Tĩnh", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9201, Name = "VN-2000 / TM-3 Lai Châu", Province = "Lai Châu", CentralMeridian = 104.75, ScaleFactor = 0.9999, Description = "KTT 104°45' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9211, Name = "VN-2000 / TM-3 Lạng Sơn", Province = "Lạng Sơn", CentralMeridian = 107.25, ScaleFactor = 0.9999, Description = "KTT 107°15' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9227, Name = "VN-2000 / TM-3 Nghệ An", Province = "Nghệ An", CentralMeridian = 104.75, ScaleFactor = 0.9999, Description = "KTT 104°45' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9216, Name = "VN-2000 / TM-3 Quảng Ninh", Province = "Quảng Ninh", CentralMeridian = 107.75, ScaleFactor = 0.9999, Description = "KTT 107°45' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9203, Name = "VN-2000 / TM-3 Sơn La", Province = "Sơn La", CentralMeridian = 104.00, ScaleFactor = 0.9999, Description = "KTT 104°00' múi 3° (TT 24/2025)" },
            new ProvinceCrs { EpsgCode = 9226, Name = "VN-2000 / TM-3 Thanh Hóa", Province = "Thanh Hóa", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3° (TT 24/2025)" },

            // ========== 2. HỆ TỌA ĐỘ VN-2000 MÚI CHIẾU 6 ĐỘ (TM-6 / UTM VN-2000) ==========
            new ProvinceCrs { EpsgCode = 3405, Name = "VN-2000 / UTM zone 48N (Múi 6°)", Province = "Toàn quốc - Múi 48N (KTT 105°)", CentralMeridian = 105.00, ScaleFactor = 0.9996, Description = "Múi 6° KTT 105°00' (Từ 102°E đến 108°E: Miền Bắc, Miền Tây, Tây Nguyên)" },
            new ProvinceCrs { EpsgCode = 3406, Name = "VN-2000 / UTM zone 49N (Múi 6°)", Province = "Toàn quốc - Múi 49N (KTT 111°)", CentralMeridian = 111.00, ScaleFactor = 0.9996, Description = "Múi 6° KTT 111°00' (Từ 108°E đến 114°E: Duyên Hải Miền Trung, Hoàng Sa, Trường Sa)" },
            new ProvinceCrs { EpsgCode = 9200, Name = "VN-2000 / TM-6 KTT 108° (Múi 6°)", Province = "Quy hoạch & Tuyến Dài KTT 108°", CentralMeridian = 108.00, ScaleFactor = 0.9996, Description = "Múi 6° KTT 108°00' (Dự án giao thông, cao tốc, đường dây 500kV liên tỉnh)" },
            new ProvinceCrs { EpsgCode = 9200, Name = "VN-2000 / TM-6 KTT 105°45' (Múi 6°)", Province = "Quy hoạch Miền Đông KTT 105°45'", CentralMeridian = 105.75, ScaleFactor = 0.9996, Description = "Múi 6° KTT 105°45' (Vùng kinh tế trọng điểm phía Nam)" },
            new ProvinceCrs { EpsgCode = 9200, Name = "VN-2000 / TM-6 KTT 107°45' (Múi 6°)", Province = "Quy hoạch Duyên Hải KTT 107°45'", CentralMeridian = 107.75, ScaleFactor = 0.9996, Description = "Múi 6° KTT 107°45' (Vùng duyên hải miền Trung)" },

            // ========== 3. CÁC TỈNH THÀNH TRUYỀN THỐNG TRƯỚC SÁP NHẬP (ĐỂ THUẬN TIỆN TRA CỨU HỒ SƠ CŨ) ==========
            new ProvinceCrs { EpsgCode = 9263, Name = "VN-2000 / TM-3 TP. Thủ Đức", Province = "TP. Thủ Đức (TP.HCM)", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9220, Name = "VN-2000 / TM-3 Hà Tây (Cũ)", Province = "Hà Tây (Nay thuộc Hà Nội)", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9248, Name = "VN-2000 / TM-3 Bà Rịa – Vũng Tàu", Province = "Bà Rịa – Vũng Tàu (Cũ)", CentralMeridian = 107.75, ScaleFactor = 0.9999, Description = "KTT 107°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9245, Name = "VN-2000 / TM-3 Bình Dương", Province = "Bình Dương (Cũ)", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9246, Name = "VN-2000 / TM-3 Bình Phước", Province = "Bình Phước", CentralMeridian = 106.25, ScaleFactor = 0.9999, Description = "KTT 106°15' múi 3°" },
            new ProvinceCrs { EpsgCode = 9243, Name = "VN-2000 / TM-3 Bình Thuận", Province = "Bình Thuận (Cũ)", CentralMeridian = 108.50, ScaleFactor = 0.9999, Description = "KTT 108°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9235, Name = "VN-2000 / TM-3 Bình Định", Province = "Bình Định", CentralMeridian = 108.25, ScaleFactor = 0.9999, Description = "KTT 108°15' múi 3°" },
            new ProvinceCrs { EpsgCode = 9252, Name = "VN-2000 / TM-3 Bến Tre", Province = "Bến Tre (Cũ)", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9261, Name = "VN-2000 / TM-3 Bạc Liêu", Province = "Bạc Liêu", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9214, Name = "VN-2000 / TM-3 Bắc Giang", Province = "Bắc Giang (Cũ)", CentralMeridian = 107.00, ScaleFactor = 0.9999, Description = "KTT 107°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9212, Name = "VN-2000 / TM-3 Bắc Kạn", Province = "Bắc Kạn (Cũ)", CentralMeridian = 106.50, ScaleFactor = 0.9999, Description = "KTT 106°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9239, Name = "VN-2000 / TM-3 Đắk Nông", Province = "Đắk Nông (Cũ)", CentralMeridian = 108.50, ScaleFactor = 0.9999, Description = "KTT 108°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9206, Name = "VN-2000 / TM-3 Hà Giang", Province = "Hà Giang (Cũ)", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9222, Name = "VN-2000 / TM-3 Hà Nam", Province = "Hà Nam (Cũ)", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9218, Name = "VN-2000 / TM-3 Hải Dương", Province = "Hải Dương (Cũ)", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9259, Name = "VN-2000 / TM-3 Hậu Giang", Province = "Hậu Giang (Cũ)", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9221, Name = "VN-2000 / TM-3 Hòa Bình", Province = "Hòa Bình (Cũ)", CentralMeridian = 106.00, ScaleFactor = 0.9999, Description = "KTT 106°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9257, Name = "VN-2000 / TM-3 Kiên Giang", Province = "Kiên Giang", CentralMeridian = 104.50, ScaleFactor = 0.9999, Description = "KTT 104°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9236, Name = "VN-2000 / TM-3 Kon Tum", Province = "Kon Tum (Cũ)", CentralMeridian = 107.50, ScaleFactor = 0.9999, Description = "KTT 107°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9250, Name = "VN-2000 / TM-3 Long An", Province = "Long An (Cũ)", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9223, Name = "VN-2000 / TM-3 Nam Định", Province = "Nam Định (Cũ)", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9242, Name = "VN-2000 / TM-3 Ninh Thuận", Province = "Ninh Thuận (Cũ)", CentralMeridian = 108.25, ScaleFactor = 0.9999, Description = "KTT 108°15' múi 3°" },
            new ProvinceCrs { EpsgCode = 9240, Name = "VN-2000 / TM-3 Phú Yên", Province = "Phú Yên", CentralMeridian = 108.50, ScaleFactor = 0.9999, Description = "KTT 108°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9229, Name = "VN-2000 / TM-3 Quảng Bình", Province = "Quảng Bình (Cũ)", CentralMeridian = 106.00, ScaleFactor = 0.9999, Description = "KTT 106°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9233, Name = "VN-2000 / TM-3 Quảng Nam", Province = "Quảng Nam (Cũ)", CentralMeridian = 107.75, ScaleFactor = 0.9999, Description = "KTT 107°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9260, Name = "VN-2000 / TM-3 Sóc Trăng", Province = "Sóc Trăng (Cũ)", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9224, Name = "VN-2000 / TM-3 Thái Bình", Province = "Thái Bình (Cũ)", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9251, Name = "VN-2000 / TM-3 Tiền Giang", Province = "Tiền Giang", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9255, Name = "VN-2000 / TM-3 Trà Vinh", Province = "Trà Vinh (Cũ)", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9209, Name = "VN-2000 / TM-3 Vĩnh Phúc", Province = "Vĩnh Phúc (Cũ)", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9205, Name = "VN-2000 / TM-3 Yên Bái", Province = "Yên Bái (Cũ)", CentralMeridian = 104.75, ScaleFactor = 0.9999, Description = "KTT 104°45' múi 3°" }
        };

        public static ProvinceCrs DefaultCrs => Provinces[0];

        // ==================== QUẢN LÝ LỊCH SỬ HỆ TỌA ĐỘ ĐÃ DÙNG (RECENT CRS) ====================
        private static string RecentCrsFilePath
        {
            get
            {
                try
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVMap");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    return Path.Combine(folder, "recent_crs.json");
                }
                catch
                {
                    return Path.Combine(Path.GetTempPath(), "avmap_recent_crs.json");
                }
            }
        }

        public static List<ProvinceCrs> GetRecentProvinces()
        {
            try
            {
                string path = RecentCrsFilePath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var list = JsonConvert.DeserializeObject<List<ProvinceCrs>>(json);
                    if (list != null && list.Count > 0)
                    {
                        // Đồng bộ lại đối tượng từ danh mục để đảm bảo tính chuẩn xác
                        var result = new List<ProvinceCrs>();
                        foreach (var item in list)
                        {
                            var matched = Provinces.FirstOrDefault(p => p.EpsgCode == item.EpsgCode && 
                                          Math.Abs(p.CentralMeridian - item.CentralMeridian) < 0.001 &&
                                          Math.Abs(p.ScaleFactor - item.ScaleFactor) < 0.0001);
                            if (matched != null)
                            {
                                if (!result.Contains(matched)) result.Add(matched);
                            }
                            else
                            {
                                if (!result.Any(r => r.Name == item.Name)) result.Add(item);
                            }
                        }
                        return result;
                    }
                }
            }
            catch { }

            // Mặc định ban đầu nếu chưa có lịch sử: gợi ý 3 hệ phổ biến nhất (TP.HCM, Hà Nội, Múi 6° 48N)
            return new List<ProvinceCrs>
            {
                Provinces[0], // TP.HCM (Mới)
                Provinces[1], // Hà Nội
                Provinces.FirstOrDefault(p => p.EpsgCode == 3405) ?? Provinces[2] // Múi 6° 48N
            };
        }

        public static void SaveToRecent(ProvinceCrs crs)
        {
            if (crs == null) return;
            try
            {
                var recentList = GetRecentProvinces();
                recentList.RemoveAll(p => p.EpsgCode == crs.EpsgCode && 
                                          Math.Abs(p.CentralMeridian - crs.CentralMeridian) < 0.001 &&
                                          Math.Abs(p.ScaleFactor - crs.ScaleFactor) < 0.0001);
                recentList.Insert(0, crs);
                if (recentList.Count > 5) recentList = recentList.Take(5).ToList(); // Giữ tối đa 5 lịch sử gần nhất

                string json = JsonConvert.SerializeObject(recentList, Formatting.Indented);
                File.WriteAllText(RecentCrsFilePath, json);
            }
            catch { }
        }
    }
}
