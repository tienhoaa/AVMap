using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;

namespace AVMap
{
    public static class GoogleTileClient
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly SemaphoreSlim _osmThrottle = new SemaphoreSlim(4);
        private static readonly string cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVMap", "Cache");

        static GoogleTileClient()
        {
            if (!Directory.Exists(cacheFolder))
            {
                Directory.CreateDirectory(cacheFolder);
            }
            try
            {
                // Tự động dọn sạch các tile bị lưu lỗi Access Blocked cũ
                foreach (var f in Directory.GetFiles(cacheFolder, "osm_*.*"))
                {
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
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
            string ext = (mapType == "svv" || mapType == "osm" || mapType == "cyclosm" || mapType == "opentopo" || mapType == "carto_light") ? "png" : "jpg";
            string fileName = $"{safeType}_{z}_{x}_{y}.{ext}";
            string filePath = Path.Combine(cacheFolder, fileName);

            if (File.Exists(filePath))
            {
                try
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        return new Bitmap(fs);
                    }
                }
                catch { }
            }

            int serverIndex = Math.Abs((x + y) % 4);
            char[] subs3 = new[] { 'a', 'b', 'c' };
            char sub3 = subs3[Math.Abs((x + y) % 3)];
            char[] subs4 = new[] { 'a', 'b', 'c', 'd' };
            char sub4 = subs4[Math.Abs((x + y) % 4)];

            string url;
            bool isThrottled = false;

            if (mapType == "svv")
            {
                // Lớp phủ các tuyến đường có Google Street View màu xanh dương
                url = $"https://mt{serverIndex}.google.com/mapslt?lyrs=svv&x={x}&y={y}&z={z}&w=256&h=256&hl=en&style=40,18";
            }
            else if (mapType == "osm")
            {
                // Nguồn OpenStreetMap Standard chính thức từ web https://www.openstreetmap.org/ (Cập nhật thời gian thực mới nhất)
                url = $"https://{sub3}.tile.openstreetmap.org/{z}/{x}/{y}.png";
                isThrottled = true;
            }
            else if (mapType == "cyclosm")
            {
                // Nguồn CyclOSM chính thức từ OpenStreetMap (Bản đồ giao thông & số nhà chi tiết nhất thế hệ mới)
                url = $"https://{sub3}.tile.cyclosm.org/cyclosm/{z}/{x}/{y}.png";
                isThrottled = true;
            }
            else if (mapType == "carto_light")
            {
                // Nguồn CARTO Positron (SAS.Planet OSM CartoLight) - Nền xám sáng tối giản cho kỹ sư CAD
                url = $"https://{sub4}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}.png";
            }
            else if (mapType == "opentopo")
            {
                // Bản đồ địa hình OpenTopoMap (Đường đồng mức + đổ bóng 3D từ SAS.Planet)
                url = $"https://{sub3}.tile.opentopomap.org/{z}/{x}/{y}.png";
                isThrottled = true;
            }
            else if (mapType == "bing")
            {
                // Ảnh vệ tinh Microsoft Bing Maps Satellite độ phân giải cao, khử mây tốt (nguồn đỉnh từ SAS.Planet)
                string quadKey = TileXYToQuadKey(x, y, z);
                url = $"https://ecn.t{serverIndex}.tiles.virtualearth.net/tiles/a{quadKey}.jpeg?g=1";
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

            if (isThrottled)
            {
                await _osmThrottle.WaitAsync().ConfigureAwait(false);
            }

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Referrer = new Uri("https://www.openstreetmap.org/");
                    request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                    request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "image");
                    request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "no-cors");
                    request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site");

                    using (var response = await client.SendAsync(request).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            byte[] data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                            // Phòng ngừa: Nếu OSM trả về ảnh lỗi "Access Blocked" (thường dưới 4.5KB)
                            if (mapType == "osm" && data.Length < 4500)
                            {
                                // Tải từ máy chủ OSM Carto dự phòng (100% cùng kiểu vẽ Standard OSM từ CSDL gốc)
                                string fbUrl = $"https://tile.openstreetmap.de/{z}/{x}/{y}.png";
                                try
                                {
                                    using (var fbReq = new HttpRequestMessage(HttpMethod.Get, fbUrl))
                                    using (var fbResp = await client.SendAsync(fbReq).ConfigureAwait(false))
                                    {
                                        if (fbResp.IsSuccessStatusCode)
                                        {
                                            data = await fbResp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                                        }
                                    }
                                }
                                catch { }
                            }

                            try { File.WriteAllBytes(filePath, data); } catch { }
                            using (var ms = new MemoryStream(data))
                            {
                                return new Bitmap(ms);
                            }
                        }
                        else if (mapType == "osm")
                        {
                            string fbUrl = $"https://tile.openstreetmap.de/{z}/{x}/{y}.png";
                            try
                            {
                                using (var fbReq = new HttpRequestMessage(HttpMethod.Get, fbUrl))
                                using (var fbResp = await client.SendAsync(fbReq).ConfigureAwait(false))
                                {
                                    if (fbResp.IsSuccessStatusCode)
                                    {
                                        byte[] data = await fbResp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                                        try { File.WriteAllBytes(filePath, data); } catch { }
                                        using (var ms = new MemoryStream(data))
                                        {
                                            return new Bitmap(ms);
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (isThrottled)
                {
                    _osmThrottle.Release();
                }
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