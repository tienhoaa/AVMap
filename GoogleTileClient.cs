using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;

namespace AVMap
{
    public static class GoogleTileClient
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly string cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVMap", "Cache");

        static GoogleTileClient()
        {
            try
            {
                // Kích hoạt giao thức bảo mật TLS 1.2 và tăng giới hạn kết nối đồng thời cho .NET Framework
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12 | (System.Net.SecurityProtocolType)3072;
                System.Net.ServicePointManager.DefaultConnectionLimit = 64;
            }
            catch { }

            if (!Directory.Exists(cacheFolder))
            {
                Directory.CreateDirectory(cacheFolder);
            }
            try
            {
                // Tự động dọn sạch các tile cũ có thể bị lưu lỗi trước đó
                string[] patterns = new[] { "osm_*.*", "cyclosm_*.*", "carto_*.*", "opentopo_*.*" };
                foreach (var p in patterns)
                {
                    foreach (var f in Directory.GetFiles(cacheFolder, p))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }
            catch { }
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 AVMap/1.4.2");
        }

        public static string ActiveMapType { get; set; } = "y"; // "y" (Hybrid), "s" (Satellite), "m" (Road), "p" (Terrain)

        public static void ClearStitchedCache()
        {
            try
            {
                if (Directory.Exists(cacheFolder))
                {
                    foreach (var file in Directory.GetFiles(cacheFolder, "*_stitched_*.png"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                    foreach (var file in Directory.GetFiles(cacheFolder, "osm_*.*"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                    foreach (var file in Directory.GetFiles(cacheFolder, "opentopo_*.*"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        public static void ClearAllTileCache()
        {
            try
            {
                if (Directory.Exists(cacheFolder))
                {
                    foreach (var file in Directory.GetFiles(cacheFolder))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        public static async Task<Bitmap> DownloadTileAsync(int x, int y, int z, string mapType)
        {
            string safeType = mapType.Replace(",", "_");
            string ext = (mapType == "svv" || mapType == "osm" || mapType == "opentopo") ? "png" : "jpg";
            string fileName = $"{safeType}_{z}_{x}_{y}.{ext}";
            string filePath = Path.Combine(cacheFolder, fileName);

            if (File.Exists(filePath))
            {
                try
                {
                    var fi = new FileInfo(filePath);
                    if (fi.Length > 500)
                    {
                        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            return new Bitmap(fs);
                        }
                    }
                    else
                    {
                        File.Delete(filePath);
                    }
                }
                catch { }
            }

            int serverIndex = Math.Abs((x + y) % 4);
            string url;
            if (mapType == "svv")
            {
                // Lớp phủ các tuyến đường có Google Street View màu xanh dương
                url = $"https://mt{serverIndex}.google.com/mapslt?lyrs=svv&x={x}&y={y}&z={z}&w=256&h=256&hl=en&style=40,18";
            }
            else if (mapType == "osm")
            {
                // Bản đồ OpenStreetMap (OSM) từ cụm CDN Humanitarian HOT (100% dữ liệu OSM, tốc độ cao, mở ổn định)
                char[] subs = new[] { 'a', 'b', 'c' };
                char sub = subs[Math.Abs((x + y) % 3)];
                url = $"https://{sub}.tile.openstreetmap.fr/hot/{z}/{x}/{y}.png";
            }
            else if (mapType == "bing")
            {
                // Ảnh vệ tinh Microsoft Bing Maps Satellite độ phân giải cao, khử mây tốt (nguồn đỉnh từ SAS.Planet)
                string quadKey = TileXYToQuadKey(x, y, z);
                url = $"https://ecn.t{serverIndex}.tiles.virtualearth.net/tiles/a{quadKey}.jpeg?g=1";
            }
            else if (mapType == "opentopo")
            {
                // Bản đồ địa hình OpenTopoMap (Đường đồng mức + đổ bóng 3D từ SAS.Planet)
                char[] subs = new[] { 'a', 'b', 'c' };
                char sub = subs[Math.Abs((x + y) % 3)];
                url = $"https://{sub}.tile.opentopomap.org/{z}/{x}/{y}.png";
            }
            else if (mapType == "esri")
            {
                // Ảnh vệ tinh toàn cầu độ nét cao từ Esri ArcGIS World Imagery miễn phí
                url = $"https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}.jpg";
            }
            else
            {
                url = $"https://mt{serverIndex}.google.com/vt/lyrs={mapType}&x={x}&y={y}&z={z}";
            }

            try
            {
                byte[] data = await client.GetByteArrayAsync(url).ConfigureAwait(false);
                if (data != null && data.Length > 500)
                {
                    try { File.WriteAllBytes(filePath, data); } catch { }
                    using (var ms = new MemoryStream(data))
                    {
                        return new Bitmap(ms);
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> StitchTilesToFileAsync(int minTileX, int maxTileX, int minTileY, int maxTileY, int zoom, string mapType)
        {
            int tileCountX = maxTileX - minTileX + 1;
            int tileCountY = maxTileY - minTileY + 1;

            if (tileCountX <= 0 || tileCountY <= 0 || tileCountX > 25 || tileCountY > 25) return null;

            int bmpWidth = tileCountX * 256;
            int bmpHeight = tileCountY * 256;

            PixelFormat format = (mapType == "svv") ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;

            using (Bitmap bigBmp = new Bitmap(bmpWidth, bmpHeight, format))
            using (Graphics g = Graphics.FromImage(bigBmp))
            {
                if (mapType == "svv")
                {
                    g.Clear(Color.Transparent);
                }
                else
                {
                    g.Clear(Color.Black);
                }

                var tasks = new Task<(int x, int y, Bitmap bmp)>[tileCountX * tileCountY];
                int idx = 0;

                for (int ty = minTileY; ty <= maxTileY; ty++)
                {
                    for (int tx = minTileX; tx <= maxTileX; tx++)
                    {
                        int curX = tx;
                        int curY = ty;
                        tasks[idx++] = Task.Run(async () =>
                        {
                            var bmp = await DownloadTileAsync(curX, curY, zoom, mapType);
                            return (curX, curY, bmp);
                        });
                    }
                }

                var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (var item in results)
                {
                    if (item.bmp != null)
                    {
                        int px = (item.x - minTileX) * 256;
                        int py = (item.y - minTileY) * 256;
                        g.DrawImage(item.bmp, px, py, 256, 256);
                        item.bmp.Dispose();
                    }
                }

                if (mapType == "svv")
                {
                    EnhanceStreetViewCoverage(bigBmp);
                }

                string safeType = mapType.Replace(",", "_");
                string outPath = Path.Combine(cacheFolder, $"{safeType}_stitched_{DateTime.Now.Ticks}.png");
                bigBmp.Save(outPath, ImageFormat.Png);
                return outPath;
            }
        }

        private static void EnhanceStreetViewCoverage(Bitmap bmp)
        {
            try
            {
                int w = bmp.Width;
                int h = bmp.Height;
                var rect = new Rectangle(0, 0, w, h);
                BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                try
                {
                    int stride = Math.Abs(data.Stride);
                    byte[] bytes = new byte[stride * h];
                    System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

                    // Chuẩn hóa nét vẽ: Giữ nguyên độ thanh mảnh tự nhiên từ Google (không dilate làm dày thô),
                    // chuyển sang màu Xanh Hoàng Gia (Royal Blue - #1A73E8) chuẩn Spatial Manager & Google Street View:
                    // R = 26, G = 115, B = 232
                    for (int y = 0; y < h; y++)
                    {
                        int rowOffset = y * stride;
                        for (int x = 0; x < w; x++)
                        {
                            int idx = rowOffset + x * 4;
                            byte a = bytes[idx + 3];
                            if (a > 15)
                            {
                                bytes[idx] = 232;       // B
                                bytes[idx + 1] = 115;   // G
                                bytes[idx + 2] = 26;    // R
                                bytes[idx + 3] = 255;   // A (Opaque 100% để hiển thị sắc nét trong AutoCAD)
                            }
                            else
                            {
                                bytes[idx] = 0;
                                bytes[idx + 1] = 0;
                                bytes[idx + 2] = 0;
                                bytes[idx + 3] = 0;     // Trong suốt hoàn toàn
                            }
                        }
                    }

                    System.Runtime.InteropServices.Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
            catch { }
        }

        public static string TileXYToQuadKey(int tileX, int tileY, int zoom)
        {
            var quadKey = new System.Text.StringBuilder();
            for (int i = zoom; i > 0; i--)
            {
                int digit = 0;
                int mask = 1 << (i - 1);
                if ((tileX & mask) != 0)
                {
                    digit++;
                }
                if ((tileY & mask) != 0)
                {
                    digit += 2;
                }
                quadKey.Append(digit);
            }
            return quadKey.ToString();
        }
    }
}
