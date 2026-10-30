using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace AVMap
{
    public static class IconHelper
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource> _cache = 
            new System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

        private static string GetIconPath(string fileName)
        {
            // 1. Thư mục cạnh DLL hiện tại
            string baseDir = Path.GetDirectoryName(typeof(IconHelper).Assembly.Location);
            string path1 = Path.Combine(baseDir, "Icons", fileName);
            if (File.Exists(path1)) return path1;

            string path2 = Path.Combine(baseDir, fileName);
            if (File.Exists(path2)) return path2;

            // 2. Thư mục dự án gốc
            string docPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AVMap", "Icons", fileName);
            if (File.Exists(docPath)) return docPath;

            return null;
        }

        public static BitmapSource LoadIcon(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            if (_cache.TryGetValue(fileName, out var cached)) return cached;

            try
            {
                var asm = typeof(IconHelper).Assembly;
                string asmName = asm.GetName().Name;

                // 1. Đọc trực tiếp từ Embedded Resource bên trong DLL (gửi 1 file DLL duy nhất vẫn đầy đủ 100% logo và icon)
                Stream stream = null;
                
                // Thử các biến thể tên namespace phổ biến
                string[] tryNames = new[]
                {
                    $"{asmName}.Icons.{fileName}",
                    $"AVMap.Icons.{fileName}",
                    $"AVMap_enjiCAD.Icons.{fileName}",
                    $"Icons.{fileName}"
                };

                foreach (var resName in tryNames)
                {
                    stream = asm.GetManifestResourceStream(resName);
                    if (stream != null) break;
                }

                // Nếu chưa thấy, duyệt danh sách Manifest tìm file kết thúc bằng fileName
                if (stream == null)
                {
                    foreach (string name in asm.GetManifestResourceNames())
                    {
                        if (name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase) ||
                            name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            stream = asm.GetManifestResourceStream(name);
                            break;
                        }
                    }
                }

                if (stream != null)
                {
                    byte[] bytes;
                    using (stream)
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        bytes = ms.ToArray();
                    }

                    // Khởi tạo BitmapImage từ MemoryStream độc lập hoàn toàn để không bao giờ bị lỗi stream đã đóng
                    var memStream = new MemoryStream(bytes);
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = memStream;
                    bi.EndInit();
                    bi.Freeze();

                    _cache[fileName] = bi;
                    return bi;
                }

                // 2. Fallback đọc từ thư mục Icons ngoài ổ cứng (nếu chạy trong môi trường phát triển)
                string fullPath = GetIconPath(fileName);
                if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                {
                    byte[] fileBytes = File.ReadAllBytes(fullPath);
                    var memStream = new MemoryStream(fileBytes);
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = memStream;
                    bi.EndInit();
                    bi.Freeze();

                    _cache[fileName] = bi;
                    return bi;
                }
            }
            catch { }
            return null;
        }
    }
}
