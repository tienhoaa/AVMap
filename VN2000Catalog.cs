using System;
using System.Collections.Generic;

namespace AVMap
{
    public class ProvinceCrs
    {
        public int EpsgCode { get; set; }
        public string Name { get; set; }
        public string Province { get; set; }
        public double CentralMeridian { get; set; } // Kinh tuyến trục (độ)
        public double ScaleFactor { get; set; }     // 0.9999 (3 độ) hoặc 0.9996 (6 độ)
        public string Description { get; set; }
        public bool IsActive { get; set; } = false;
        public string StatusText => IsActive ? "✔ Đang dùng" : "";

        public override string ToString()
        {
            return $"[{EpsgCode}] {Name} - {Province} (KTT: {CentralMeridian}°)";
        }
    }

    public static class VN2000Catalog
    {
        public static List<ProvinceCrs> Provinces = new List<ProvinceCrs>
        {
            // ==================== HỆ TỌA ĐỘ VN-2000 MÚI CHIẾU 3 ĐỘ (CHUẨN THÔNG TƯ 25/2014/TT-BTNMT & TT 02/2023/TT-BTNMT) ====================
            new ProvinceCrs { EpsgCode = 9254, Name = "VN-2000 / TM-3 Vĩnh Long", Province = "Vĩnh Long", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9263, Name = "VN-2000 / TM-3 TP. Hồ Chí Minh", Province = "TP. Hồ Chí Minh", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9263, Name = "VN-2000 / TM-3 TP. Thủ Đức", Province = "TP. Thủ Đức (TP. Hồ Chí Minh)", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3° (Thuộc TP. Hồ Chí Minh)" },
            new ProvinceCrs { EpsgCode = 9220, Name = "VN-2000 / TM-3 Hà Nội", Province = "TP. Hà Nội", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9220, Name = "VN-2000 / TM-3 Hà Tây (Hà Nội)", Province = "Hà Tây (Đã sáp nhập vào Hà Nội)", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3° (Toàn bộ Hà Tây sáp nhập Hà Nội)" },
            new ProvinceCrs { EpsgCode = 9232, Name = "VN-2000 / TM-3 Đà Nẵng", Province = "TP. Đà Nẵng", CentralMeridian = 107.75, ScaleFactor = 0.9999, Description = "KTT 107°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9217, Name = "VN-2000 / TM-3 Hải Phòng", Province = "TP. Hải Phòng", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9258, Name = "VN-2000 / TM-3 Cần Thơ", Province = "TP. Cần Thơ", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },

            new ProvinceCrs { EpsgCode = 9256, Name = "VN-2000 / TM-3 An Giang", Province = "An Giang", CentralMeridian = 104.75, ScaleFactor = 0.9999, Description = "KTT 104°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9248, Name = "VN-2000 / TM-3 Bà Rịa – Vũng Tàu", Province = "Bà Rịa – Vũng Tàu", CentralMeridian = 107.75, ScaleFactor = 0.9999, Description = "KTT 107°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9212, Name = "VN-2000 / TM-3 Bắc Kạn", Province = "Bắc Kạn", CentralMeridian = 106.50, ScaleFactor = 0.9999, Description = "KTT 106°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9214, Name = "VN-2000 / TM-3 Bắc Giang", Province = "Bắc Giang", CentralMeridian = 107.00, ScaleFactor = 0.9999, Description = "KTT 107°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9261, Name = "VN-2000 / TM-3 Bạc Liêu", Province = "Bạc Liêu", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9215, Name = "VN-2000 / TM-3 Bắc Ninh", Province = "Bắc Ninh", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9252, Name = "VN-2000 / TM-3 Bến Tre", Province = "Bến Tre", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9235, Name = "VN-2000 / TM-3 Bình Định", Province = "Bình Định", CentralMeridian = 108.25, ScaleFactor = 0.9999, Description = "KTT 108°15' múi 3°" },
            new ProvinceCrs { EpsgCode = 9245, Name = "VN-2000 / TM-3 Bình Dương", Province = "Bình Dương", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9246, Name = "VN-2000 / TM-3 Bình Phước", Province = "Bình Phước", CentralMeridian = 106.25, ScaleFactor = 0.9999, Description = "KTT 106°15' múi 3°" },
            new ProvinceCrs { EpsgCode = 9243, Name = "VN-2000 / TM-3 Bình Thuận", Province = "Bình Thuận", CentralMeridian = 108.50, ScaleFactor = 0.9999, Description = "KTT 108°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9262, Name = "VN-2000 / TM-3 Cà Mau", Province = "Cà Mau", CentralMeridian = 104.50, ScaleFactor = 0.9999, Description = "KTT 104°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9210, Name = "VN-2000 / TM-3 Cao Bằng", Province = "Cao Bằng", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9238, Name = "VN-2000 / TM-3 Đắk Lắk", Province = "Đắk Lắk", CentralMeridian = 108.50, ScaleFactor = 0.9999, Description = "KTT 108°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9239, Name = "VN-2000 / TM-3 Đắk Nông", Province = "Đắk Nông", CentralMeridian = 108.50, ScaleFactor = 0.9999, Description = "KTT 108°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9202, Name = "VN-2000 / TM-3 Điện Biên", Province = "Điện Biên", CentralMeridian = 103.00, ScaleFactor = 0.9999, Description = "KTT 103°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9247, Name = "VN-2000 / TM-3 Đồng Nai", Province = "Đồng Nai", CentralMeridian = 107.75, ScaleFactor = 0.9999, Description = "KTT 107°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9253, Name = "VN-2000 / TM-3 Đồng Tháp", Province = "Đồng Tháp", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9237, Name = "VN-2000 / TM-3 Gia Lai", Province = "Gia Lai", CentralMeridian = 108.50, ScaleFactor = 0.9999, Description = "KTT 108°30' múi 3° (Chuẩn TT 25/2014/TT-BTNMT)" },
            new ProvinceCrs { EpsgCode = 9206, Name = "VN-2000 / TM-3 Hà Giang", Province = "Hà Giang", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9222, Name = "VN-2000 / TM-3 Hà Nam", Province = "Hà Nam", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9228, Name = "VN-2000 / TM-3 Hà Tĩnh", Province = "Hà Tĩnh", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9218, Name = "VN-2000 / TM-3 Hải Dương", Province = "Hải Dương", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9259, Name = "VN-2000 / TM-3 Hậu Giang", Province = "Hậu Giang", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9221, Name = "VN-2000 / TM-3 Hoà Bình", Province = "Hoà Bình", CentralMeridian = 106.00, ScaleFactor = 0.9999, Description = "KTT 106°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9219, Name = "VN-2000 / TM-3 Hưng Yên", Province = "Hưng Yên", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9241, Name = "VN-2000 / TM-3 Khánh Hoà", Province = "Khánh Hoà", CentralMeridian = 108.25, ScaleFactor = 0.9999, Description = "KTT 108°15' múi 3°" },
            new ProvinceCrs { EpsgCode = 9257, Name = "VN-2000 / TM-3 Kiên Giang", Province = "Kiên Giang", CentralMeridian = 104.50, ScaleFactor = 0.9999, Description = "KTT 104°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9236, Name = "VN-2000 / TM-3 Kon Tum", Province = "Kon Tum", CentralMeridian = 107.50, ScaleFactor = 0.9999, Description = "KTT 107°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9201, Name = "VN-2000 / TM-3 Lai Châu", Province = "Lai Châu", CentralMeridian = 103.00, ScaleFactor = 0.9999, Description = "KTT 103°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9244, Name = "VN-2000 / TM-3 Lâm Đồng", Province = "Lâm Đồng", CentralMeridian = 107.75, ScaleFactor = 0.9999, Description = "KTT 107°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9211, Name = "VN-2000 / TM-3 Lạng Sơn", Province = "Lạng Sơn", CentralMeridian = 107.25, ScaleFactor = 0.9999, Description = "KTT 107°15' múi 3°" },
            new ProvinceCrs { EpsgCode = 9204, Name = "VN-2000 / TM-3 Lào Cai", Province = "Lào Cai", CentralMeridian = 104.75, ScaleFactor = 0.9999, Description = "KTT 104°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9250, Name = "VN-2000 / TM-3 Long An", Province = "Long An", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9223, Name = "VN-2000 / TM-3 Nam Định", Province = "Nam Định", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9227, Name = "VN-2000 / TM-3 Nghệ An", Province = "Nghệ An", CentralMeridian = 104.75, ScaleFactor = 0.9999, Description = "KTT 104°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9225, Name = "VN-2000 / TM-3 Ninh Bình", Province = "Ninh Bình", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9242, Name = "VN-2000 / TM-3 Ninh Thuận", Province = "Ninh Thuận", CentralMeridian = 108.25, ScaleFactor = 0.9999, Description = "KTT 108°15' múi 3°" },
            new ProvinceCrs { EpsgCode = 9208, Name = "VN-2000 / TM-3 Phú Thọ", Province = "Phú Thọ", CentralMeridian = 104.75, ScaleFactor = 0.9999, Description = "KTT 104°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9240, Name = "VN-2000 / TM-3 Phú Yên", Province = "Phú Yên", CentralMeridian = 108.50, ScaleFactor = 0.9999, Description = "KTT 108°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9229, Name = "VN-2000 / TM-3 Quảng Bình", Province = "Quảng Bình", CentralMeridian = 106.00, ScaleFactor = 0.9999, Description = "KTT 106°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9233, Name = "VN-2000 / TM-3 Quảng Nam", Province = "Quảng Nam", CentralMeridian = 107.75, ScaleFactor = 0.9999, Description = "KTT 107°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9234, Name = "VN-2000 / TM-3 Quảng Ngãi", Province = "Quảng Ngãi", CentralMeridian = 108.00, ScaleFactor = 0.9999, Description = "KTT 108°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9216, Name = "VN-2000 / TM-3 Quảng Ninh", Province = "Quảng Ninh", CentralMeridian = 107.75, ScaleFactor = 0.9999, Description = "KTT 107°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9230, Name = "VN-2000 / TM-3 Quảng Trị", Province = "Quảng Trị", CentralMeridian = 106.25, ScaleFactor = 0.9999, Description = "KTT 106°15' múi 3°" },
            new ProvinceCrs { EpsgCode = 9260, Name = "VN-2000 / TM-3 Sóc Trăng", Province = "Sóc Trăng", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9203, Name = "VN-2000 / TM-3 Sơn La", Province = "Sơn La", CentralMeridian = 104.00, ScaleFactor = 0.9999, Description = "KTT 104°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9249, Name = "VN-2000 / TM-3 Tây Ninh", Province = "Tây Ninh", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9224, Name = "VN-2000 / TM-3 Thái Bình", Province = "Thái Bình", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9213, Name = "VN-2000 / TM-3 Thái Nguyên", Province = "Thái Nguyên", CentralMeridian = 106.50, ScaleFactor = 0.9999, Description = "KTT 106°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9226, Name = "VN-2000 / TM-3 Thanh Hoá", Province = "Thanh Hoá", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9231, Name = "VN-2000 / TM-3 Huế", Province = "Thừa Thiên Huế (TP. Huế)", CentralMeridian = 107.00, ScaleFactor = 0.9999, Description = "KTT 107°00' múi 3° (TP. Huế trực thuộc TW)" },
            new ProvinceCrs { EpsgCode = 9251, Name = "VN-2000 / TM-3 Tiền Giang", Province = "Tiền Giang", CentralMeridian = 105.75, ScaleFactor = 0.9999, Description = "KTT 105°45' múi 3°" },
            new ProvinceCrs { EpsgCode = 9255, Name = "VN-2000 / TM-3 Trà Vinh", Province = "Trà Vinh", CentralMeridian = 105.50, ScaleFactor = 0.9999, Description = "KTT 105°30' múi 3°" },
            new ProvinceCrs { EpsgCode = 9207, Name = "VN-2000 / TM-3 Tuyên Quang", Province = "Tuyên Quang", CentralMeridian = 106.00, ScaleFactor = 0.9999, Description = "KTT 106°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9209, Name = "VN-2000 / TM-3 Vĩnh Phúc", Province = "Vĩnh Phúc", CentralMeridian = 105.00, ScaleFactor = 0.9999, Description = "KTT 105°00' múi 3°" },
            new ProvinceCrs { EpsgCode = 9205, Name = "VN-2000 / TM-3 Yên Bái", Province = "Yên Bái", CentralMeridian = 104.75, ScaleFactor = 0.9999, Description = "KTT 104°45' múi 3°" },

            // ==================== MÚI 6 ĐỘ (TOÀN QUỐC / QUY HOẠCH DIỆN RỘNG) ====================
            new ProvinceCrs { EpsgCode = 3405, Name = "VN-2000 / UTM zone 48N", Province = "Toàn quốc (Múi 48N KTT 105°)", CentralMeridian = 105.00, ScaleFactor = 0.9996, Description = "Múi 6° KTT 105°" },
            new ProvinceCrs { EpsgCode = 3406, Name = "VN-2000 / UTM zone 49N", Province = "Toàn quốc (Múi 49N KTT 111°)", CentralMeridian = 111.00, ScaleFactor = 0.9996, Description = "Múi 6° KTT 111°" }
        };

        public static ProvinceCrs DefaultCrs => Provinces[0]; // Vĩnh Long mặc định
    }
}
