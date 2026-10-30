using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ImageMagick;

namespace AVMap
{
    public static class StreetViewAiService
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// Xử lý làm sạch ảnh Street View (xóa logo Google, copyright) bằng AI hoặc Smart Inpainting cục bộ,
        /// và đóng dấu thông tin trắc địa chuyên nghiệp nếu được cấu hình.
        /// </summary>
        public static async Task<bool> ProcessStreetViewImageAsync(
            string inputImagePath, 
            string outputImagePath, 
            double lat, 
            double lon, 
            double heading, 
            string crsName, 
            double cadX, 
            double cadY, 
            Action<string> statusCallback = null)
        {
            if (!File.Exists(inputImagePath)) return false;

            var settings = StreetViewAiSettings.Instance;
            statusCallback?.Invoke("Đang phân tích cấu trúc ảnh Street View...");

            byte[] processedBytes = null;

            // 1. Thử gọi API ngoài nếu người dùng có cấu hình API Key
            if (settings.Provider == "Clipdrop" && !string.IsNullOrWhiteSpace(settings.ClipdropApiKey))
            {
                statusCallback?.Invoke("Đang gọi Clipdrop Inpaint API để xóa logo...");
                processedBytes = await InpaintViaClipdropAsync(inputImagePath, settings.ClipdropApiKey);
            }
            else if (settings.Provider == "GoogleGemini" && !string.IsNullOrWhiteSpace(settings.GoogleApiKey))
            {
                statusCallback?.Invoke("Đang xử lý qua Google AI...");
                // Nếu người dùng có Google AI Key
                processedBytes = await InpaintViaGoogleAiAsync(inputImagePath, settings.GoogleApiKey);
            }

            // 2. Chế độ AI Local / Pristine Clean: 
            // Do toàn bộ UI Google (Searchbox, Titlecard, Pegman, La bàn) đã được ẩn sạch 100% từ DOM,
            // ảnh chụp từ Canvas là ảnh toàn cảnh góc nhìn thực tế độ nét cao nguyên bản.
            // Tool tiến hành cắt gọt chính xác mép đáy 42px để loại bỏ dải chữ bản quyền nhỏ của Google.
            if (processedBytes == null || processedBytes.Length == 0)
            {
                statusCallback?.Invoke("Đang tinh chỉnh làm sạch mép đáy và xóa mờ chữ chìm trên mặt đường...");
                processedBytes = SmartLocalInpaint(inputImagePath);
            }

            if (processedBytes == null || processedBytes.Length == 0)
            {
                try
                {
                    processedBytes = File.ReadAllBytes(inputImagePath);
                }
                catch { }
            }

            if (processedBytes == null || processedBytes.Length == 0)
            {
                File.Copy(inputImagePath, outputImagePath, true);
                return true;
            }

            // 3. Đóng dấu thông tin khảo sát nếu người dùng BẬT tùy chọn trong cài đặt
            if (settings.AddSurveyStamp)
            {
                statusCallback?.Invoke("Đang đóng dấu thông tin tọa độ & khảo sát hiện trường...");
                processedBytes = AddSurveyStampToImage(
                    processedBytes, lat, lon, heading, crsName, cadX, cadY, settings.ProjectName);
            }

            // 4. Gắn siêu dữ liệu định vị GPS EXIF (Tọa độ WGS84, góc nhìn la bàn, VN-2000) vào ảnh
            if (Math.Abs(lat) > 0.0001 || Math.Abs(lon) > 0.0001)
            {
                statusCallback?.Invoke("Đang nhúng tọa độ GPS & góc nhìn la bàn vào siêu dữ liệu EXIF...");
                processedBytes = EmbedGpsMetadataToBytes(processedBytes, outputImagePath, lat, lon, heading, crsName, cadX, cadY);
            }

            // 5. Lưu ra file đích
            File.WriteAllBytes(outputImagePath, processedBytes);
            statusCallback?.Invoke("Hoàn tất xử lý ảnh hiện trường!");
            return true;
        }

        /// <summary>
        /// Nhúng thông tin tọa độ GPS (WGS-84), hệ tọa độ VN-2000, góc nhìn la bàn (Heading) 
        /// và thời gian chụp vào siêu dữ liệu tiêu chuẩn EXIF của ảnh (JPEG/PNG).
        /// Giúp ảnh chụp từ Street View có đầy đủ thông tin định vị để tự động import vào CAD 
        /// qua tính năng Ảnh Hiện Trạng hoặc mở trên Google Earth, QGIS, ArcGIS, Windows Properties.
        /// </summary>
        public static byte[] EmbedGpsMetadataToBytes(
            byte[] imageBytes,
            string targetPath,
            double lat,
            double lon,
            double heading,
            string crsName,
            double cadX,
            double cadY)
        {
            if (imageBytes == null || imageBytes.Length == 0) return imageBytes;
            try
            {
                using (var image = new MagickImage(imageBytes))
                {
                    var profile = image.GetExifProfile() ?? new ExifProfile();

                    if (Math.Abs(lat) > 0.0001 || Math.Abs(lon) > 0.0001)
                    {
                        // 1. Tọa độ Vĩ độ (GPS Latitude)
                        string latRef = lat >= 0 ? "N" : "S";
                        profile.SetValue(ExifTag.GPSLatitudeRef, latRef);
                        profile.SetValue(ExifTag.GPSLatitude, ConvertDoubleToRationals(Math.Abs(lat)));

                        // 2. Tọa độ Kinh độ (GPS Longitude)
                        string lonRef = lon >= 0 ? "E" : "W";
                        profile.SetValue(ExifTag.GPSLongitudeRef, lonRef);
                        profile.SetValue(ExifTag.GPSLongitude, ConvertDoubleToRationals(Math.Abs(lon)));

                        // 3. Hướng nhìn camera (Heading / Direction)
                        if (heading >= 0)
                        {
                            profile.SetValue(ExifTag.GPSImgDirectionRef, "T"); // T = True North
                            double cleanHeading = ((heading % 360) + 360) % 360;
                            uint h100 = (uint)Math.Max(0, Math.Min(36000, Math.Round(cleanHeading * 100)));
                            profile.SetValue(ExifTag.GPSImgDirection, new Rational(h100, 100));
                        }

                        // 4. Ngày giờ chụp
                        DateTime now = DateTime.Now;
                        profile.SetValue(ExifTag.DateTimeOriginal, now.ToString("yyyy:MM:dd HH:mm:ss"));
                        profile.SetValue(ExifTag.DateTimeDigitized, now.ToString("yyyy:MM:dd HH:mm:ss"));
                        profile.SetValue(ExifTag.GPSDateStamp, DateTime.UtcNow.ToString("yyyy:MM:dd"));

                        // 5. Ghi chú thông tin VN-2000 & dự án
                        string desc = $"VN-2000 ({crsName}): X={cadX:F2}, Y={cadY:F2} | WGS-84: Lat={lat:F6}, Lon={lon:F6} | Hướng={heading:F1}° | AVMap Google Street View";
                        profile.SetValue(ExifTag.ImageDescription, desc);
                        profile.SetValue(ExifTag.Software, "AVMap Survey Suite");

                        image.SetProfile(profile);
                    }

                    // Đảm bảo định dạng xuất tương ứng với phần mở rộng
                    if (!string.IsNullOrEmpty(targetPath) && targetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        image.Format = MagickFormat.Png;
                    }
                    else
                    {
                        image.Format = MagickFormat.Jpeg;
                    }

                    return image.ToByteArray();
                }
            }
            catch
            {
                return imageBytes;
            }
        }

        private static Rational[] ConvertDoubleToRationals(double val)
        {
            val = Math.Abs(val);
            uint d = (uint)Math.Floor(val);
            val = (val - d) * 60.0;
            uint m = (uint)Math.Floor(val);
            val = (val - m) * 60.0;
            uint s = (uint)Math.Round(val * 10000.0);
            return new Rational[]
            {
                new Rational(d, 1),
                new Rational(m, 1),
                new Rational(s, 10000)
            };
        }

        /// <summary>
        /// Tự động quét và loại bỏ triệt để dải đen co 2 bên (pillarbox black bars) nếu có,
        /// đảm bảo bức ảnh xuất ra luôn tự động tràn viền 100% toàn màn hình.
        /// </summary>
        public static Bitmap AutoCropBlackBars(Bitmap src)
        {
            if (src == null) return null;
            int w = src.Width;
            int h = src.Height;
            if (w < 100 || h < 100) return (Bitmap)src.Clone();

            int leftBlack = 0;
            int rightBlack = 0;

            // Quét dải đen bên trái (từ mép x = 0 vào trong tối đa 25% chiều rộng)
            for (int x = 0; x < w / 4; x++)
            {
                bool isColBlack = true;
                for (int step = 1; step <= 9; step++)
                {
                    int y = (h * step) / 10;
                    Color p = src.GetPixel(x, y);
                    if (p.R > 18 || p.G > 18 || p.B > 18)
                    {
                        isColBlack = false;
                        break;
                    }
                }
                if (isColBlack) leftBlack = x + 1;
                else break;
            }

            // Quét dải đen bên phải (từ mép x = w - 1 lùi vào trong tối đa 25% chiều rộng)
            for (int x = w - 1; x >= w - (w / 4); x--)
            {
                bool isColBlack = true;
                for (int step = 1; step <= 9; step++)
                {
                    int y = (h * step) / 10;
                    Color p = src.GetPixel(x, y);
                    if (p.R > 18 || p.G > 18 || p.B > 18)
                    {
                        isColBlack = false;
                        break;
                    }
                }
                if (isColBlack) rightBlack = (w - x);
                else break;
            }

            // Nếu phát hiện có dải đen co 2 bên (từ 3px trở lên), cắt bỏ ngay để ảnh tràn viền 100%
            if (leftBlack > 2 || rightBlack > 2)
            {
                int cropX = leftBlack;
                int cropW = w - leftBlack - rightBlack;
                if (cropW > 100)
                {
                    var cropped = new Bitmap(cropW, h, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(cropped))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(src, new Rectangle(0, 0, cropW, h), new Rectangle(cropX, 0, cropW, h), GraphicsUnit.Pixel);
                    }
                    return cropped;
                }
            }

            return (Bitmap)src.Clone();
        }

        /// <summary>
        /// Thuật toán làm sạch ảnh Street View cục bộ:
        /// 1. Tự động cắt bỏ dải mép đáy 42px chứa dòng chữ bản quyền nhỏ của Google.
        /// 2. Giữ nguyên vẹn 100% độ sắc nét và màu sắc chân thực của ảnh chụp thực tế.
        /// 3. Không chắp vá texture giả tạo, không để lại vết mờ nhòe hay vệt kẻ ngang.
        /// </summary>
        public static byte[] SmartLocalInpaint(string imagePath)
        {
            try
            {
                using (var rawBmp = new Bitmap(imagePath))
                using (var srcBmp = AutoCropBlackBars(rawBmp))
                {
                    int w = srcBmp.Width;
                    int h = srcBmp.Height;

                    // Cắt bỏ dải mép dưới cùng chứa dòng chữ bản quyền nhỏ 42px
                    int cropH = Math.Max(100, h - 42);
                    using (var baseBmp = srcBmp.Clone(new Rectangle(0, 0, w, cropH), PixelFormat.Format32bppArgb))
                    {
                        // 2. Tự động xóa mờ vệt chữ bản quyền Google in chìm trên mặt đường
                        if (StreetViewAiSettings.Instance.RemoveRoadWatermark)
                        {
                            RemoveRoadWatermark(baseBmp);
                        }

                        using (var ms = new MemoryStream())
                        {
                            baseBmp.Save(ms, ImageFormat.Jpeg);
                            return ms.ToArray();
                        }
                    }
                }
            }
            catch
            {
                return File.Exists(imagePath) ? File.ReadAllBytes(imagePath) : null;
            }
        }

        /// <summary>
        /// Bộ lọc thông minh xóa mờ vệt chữ bản quyền Google chìm trên mặt đường (Road Watermark Remover).
        /// Google Street View thường in chìm chữ '© [Năm] Google' lặp lại dạng bán trong suốt trên mặt đường nhựa.
        /// Thuật toán sử dụng White Top-Hat Morphological Transform trên dải phổ màu bê tông nhựa:
        /// 1. Nhận diện chính xác vùng mặt đường nhựa (màu xám trung tính, không chạm vào vạch kẻ sơn vàng/trắng và cây cối).
        /// 2. Bóc tách thành phần nét chữ sáng mảnh (Top-Hat 7x7) và triệt tiêu độ sáng vượt trội của chữ chìm.
        /// 3. Khôi phục lại bề mặt nhựa đường liền mạch, đồng nhất mà không làm mờ ảnh hay để lại vệt vá.
        /// </summary>
        public static void RemoveRoadWatermark(Bitmap bmp)
        {
            if (bmp == null) return;
            int w = bmp.Width;
            int h = bmp.Height;
            if (w < 100 || h < 100) return;

            // Chỉ xử lý từ 38% chiều cao trở xuống (vùng chứa mặt đường và lề)
            int startY = (int)(h * 0.38);

            BitmapData bmpData = bmp.LockBits(
                new Rectangle(0, 0, w, h),
                ImageLockMode.ReadWrite,
                PixelFormat.Format32bppArgb);

            try
            {
                int stride = bmpData.Stride;
                int bytesPerPixel = 4;
                byte[] rgbValues = new byte[stride * h];
                Marshal.Copy(bmpData.Scan0, rgbValues, 0, rgbValues.Length);

                // Tạo mảng bản đồ độ sáng Luminance và đánh dấu vùng mặt đường
                byte[] lumMap = new byte[w * h];
                bool[] isRoadMap = new bool[w * h];

                for (int y = startY; y < h; y++)
                {
                    int rowOffset = y * stride;
                    int mapOffset = y * w;

                    for (int x = 0; x < w; x++)
                    {
                        int idx = rowOffset + x * bytesPerPixel;
                        byte b = rgbValues[idx];
                        byte g = rgbValues[idx + 1];
                        byte r = rgbValues[idx + 2];

                        int lum = (299 * r + 587 * g + 114 * b) / 1000;
                        lumMap[mapOffset + x] = (byte)lum;

                        // Tiêu chí nhận diện mặt đường nhựa (Neutral Gray Asphalt):
                        int diffRg = Math.Abs(r - g);
                        int diffGb = Math.Abs(g - b);
                        int diffRb = Math.Abs(r - b);

                        // Không phải vạch sơn vàng
                        bool isYellowLine = (r > 130 && g > 110 && (r - b) > 30 && (g - b) > 20);

                        // Không phải cây cỏ/thảm thực vật xanh
                        bool isVegetation = (g > r + 12 && g > b + 12);

                        // Độ sáng trong khoảng mặt đường nhựa thực tế
                        bool isAsphaltLum = (lum >= 40 && lum <= 180);

                        if (diffRg <= 22 && diffGb <= 22 && diffRb <= 25 && !isYellowLine && !isVegetation && isAsphaltLum)
                        {
                            isRoadMap[mapOffset + x] = true;
                        }
                    }
                }

                // Áp dụng White Top-Hat Morphological Filter (Bán kính r = 3, cửa sổ 7x7)
                int radius = 3;
                byte[] minLum = new byte[w * h];

                // Bước 1: Phép xói mòn (Erosion - tìm giá trị tối nhất của mặt đường xung quanh nét chữ)
                for (int y = startY + radius; y < h - radius; y++)
                {
                    for (int x = radius; x < w - radius; x++)
                    {
                        int centerIdx = y * w + x;
                        if (!isRoadMap[centerIdx]) continue;

                        int minVal = 255;
                        int roadCount = 0;

                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            int ny = y + dy;
                            int nRow = ny * w;
                            for (int dx = -radius; dx <= radius; dx++)
                            {
                                int nIdx = nRow + (x + dx);
                                if (isRoadMap[nIdx])
                                {
                                    roadCount++;
                                    if (lumMap[nIdx] < minVal) minVal = lumMap[nIdx];
                                }
                            }
                        }

                        if (roadCount >= 8)
                        {
                            minLum[centerIdx] = (byte)minVal;
                        }
                        else
                        {
                            minLum[centerIdx] = lumMap[centerIdx];
                        }
                    }
                }

                // Bước 2: Phép giãn nở (Dilation - tái tạo nền mặt đường không còn nét chữ)
                // và triệt tiêu độ sáng thừa của chữ chìm (Top-Hat subtraction)
                for (int y = startY + radius; y < h - radius; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = radius; x < w - radius; x++)
                    {
                        int centerIdx = y * w + x;
                        if (!isRoadMap[centerIdx]) continue;

                        int maxOfMin = 0;
                        int validCount = 0;

                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            int ny = y + dy;
                            int nRow = ny * w;
                            for (int dx = -radius; dx <= radius; dx++)
                            {
                                int nIdx = nRow + (x + dx);
                                if (isRoadMap[nIdx] && minLum[nIdx] > 0)
                                {
                                    validCount++;
                                    if (minLum[nIdx] > maxOfMin) maxOfMin = minLum[nIdx];
                                }
                            }
                        }

                        if (validCount < 6) continue;

                        int curLum = lumMap[centerIdx];
                        int topHat = curLum - maxOfMin;

                        // Chữ chìm Google '© 2025 Google' có độ sáng cao hơn nền nhựa từ 6 đến 45 đơn vị
                        if (topHat >= 6 && topHat <= 45)
                        {
                            int sub = (int)(topHat * 0.90); // Triệt tiêu 90% phần sáng thừa của chữ
                            int idx = rowOffset + x * bytesPerPixel;

                            rgbValues[idx] = (byte)Math.Max(0, rgbValues[idx] - sub);         // B
                            rgbValues[idx + 1] = (byte)Math.Max(0, rgbValues[idx + 1] - sub); // G
                            rgbValues[idx + 2] = (byte)Math.Max(0, rgbValues[idx + 2] - sub); // R
                        }
                    }
                }

                Marshal.Copy(rgbValues, 0, bmpData.Scan0, rgbValues.Length);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        private static void InpaintSoftPatch(Graphics g, Bitmap src, int targetX, int targetY, int targetW, int targetH, int sampleX, int sampleY)
        {
            if (src == null) return;
            targetX = Math.Max(0, Math.Min(src.Width - 1, targetX));
            targetY = Math.Max(0, Math.Min(src.Height - 1, targetY));
            targetW = Math.Max(1, Math.Min(targetW, src.Width - targetX));
            targetH = Math.Max(1, Math.Min(targetH, src.Height - targetY));

            sampleX = Math.Max(0, Math.Min(src.Width - targetW, sampleX));
            sampleY = Math.Max(0, Math.Min(src.Height - targetH, sampleY));

            var sampleRect = new Rectangle(sampleX, sampleY, targetW, targetH);
            using (var patch = src.Clone(sampleRect, PixelFormat.Format32bppArgb))
            {
                // Lật nhẹ vân ảnh để không bị lặp hoa văn giả tạo
                patch.RotateFlip(RotateFlipType.RotateNoneFlipX);

                // Vẽ patch đè lên vùng cần xóa
                g.DrawImage(patch, new Rectangle(targetX, targetY, targetW, targetH));

                // Làm mềm viền bằng gradient feathering để hòa trộn mượt mà
                int feather = Math.Min(12, Math.Min(targetW, targetH) / 3);
                if (feather > 2)
                {
                    using (var blendBrush = new LinearGradientBrush(
                        new Point(targetX, targetY),
                        new Point(targetX, targetY + feather),
                        Color.FromArgb(60, 20, 20, 20),
                        Color.FromArgb(0, 20, 20, 20)))
                    {
                        g.FillRectangle(blendBrush, targetX, targetY, targetW, feather);
                    }
                }
            }
        }

        /// <summary>
        /// Đóng dấu thông tin khảo sát hiện trường chuyên nghiệp vào góc ảnh
        /// </summary>
        public static byte[] AddSurveyStampToImage(
            byte[] imgBytes, 
            double lat, 
            double lon, 
            double heading, 
            string crsName, 
            double cadX, 
            double cadY, 
            string projectName)
        {
            try
            {
                using (var msIn = new MemoryStream(imgBytes))
                using (var bmp = new Bitmap(msIn))
                {
                    int w = bmp.Width;
                    int h = bmp.Height;

                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                        bool incTime = StreetViewAiSettings.Instance.IncludeTimestamp;

                        // Kích thước badge thông tin: nếu có thời gian thì 88px, nếu không thì 70px gọn gàng
                        int badgeW = Math.Min(560, (int)(w * 0.45));
                        int badgeH = incTime ? 88 : 70;
                        int badgeX = 16;
                        int badgeY = 16;

                        // Nền mờ kính tối (Dark Glassmorphism)
                        using (var bgBrush = new SolidBrush(Color.FromArgb(190, 15, 23, 42)))
                        using (var borderPen = new Pen(Color.FromArgb(140, 56, 189, 248), 1.5f))
                        {
                            var rect = new Rectangle(badgeX, badgeY, badgeW, badgeH);
                            g.FillRectangle(bgBrush, rect);
                            g.DrawRectangle(borderPen, rect);
                        }

                        // Vẽ dải màu nhấn bên trái
                        using (var barBrush = new SolidBrush(Color.FromArgb(255, 34, 197, 94)))
                        {
                            g.FillRectangle(barBrush, badgeX, badgeY, 4, badgeH);
                        }

                        string dirText = StreetViewWindow.GetDirectionText(heading);

                        using (var titleFont = new Font("Segoe UI", 11, FontStyle.Bold))
                        using (var textFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                        using (var titleBrush = new SolidBrush(Color.FromArgb(255, 248, 250, 252)))
                        using (var goldBrush = new SolidBrush(Color.FromArgb(255, 251, 191, 36)))
                        using (var blueBrush = new SolidBrush(Color.FromArgb(255, 56, 189, 248)))
                        using (var subBrush = new SolidBrush(Color.FromArgb(255, 203, 213, 225)))
                        {
                            g.DrawString($"ẢNH KHẢO SÁT HIỆN TRƯỜNG - {projectName.ToUpper()}", titleFont, goldBrush, badgeX + 12, badgeY + 8);
                            g.DrawString($"Hệ VN-2000 ({crsName}): X = {cadX:N2} m  |  Y = {cadY:N2} m", textFont, titleBrush, badgeX + 12, badgeY + 30);
                            g.DrawString($"Góc nhìn: {heading:F0}° ({dirText})  |  Tọa độ: {lat:F6}°, {lon:F6}°", textFont, blueBrush, badgeX + 12, badgeY + 48);

                            if (incTime)
                            {
                                string timeStr = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                                g.DrawString($"Thời gian ghi nhận: {timeStr}", textFont, subBrush, badgeX + 12, badgeY + 66);
                            }
                        }
                    }

                    using (var msOut = new MemoryStream())
                    {
                        bmp.Save(msOut, ImageFormat.Jpeg);
                        return msOut.ToArray();
                    }
                }
            }
            catch
            {
                return imgBytes;
            }
        }

        /// <summary>
        /// Gọi API Clipdrop Cleanup (chuyên xóa vật thể/watermark tự động không cần prompt)
        /// </summary>
        private static async Task<byte[]> InpaintViaClipdropAsync(string imagePath, string apiKey)
        {
            try
            {
                // Tạo ảnh mask trắng đen (phần logo màu trắng, phần còn lại màu đen)
                using (var srcBmp = new Bitmap(imagePath))
                {
                    int w = srcBmp.Width;
                    int h = srcBmp.Height;

                    using (var maskBmp = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                    {
                        using (var g = Graphics.FromImage(maskBmp))
                        {
                            g.Clear(Color.Black);

                            // Đánh dấu vùng logo góc trái và copyright góc phải màu trắng
                            int logoW = (int)Math.Max(180, w * 0.25);
                            int logoH = (int)Math.Max(48, h * 0.06);
                            g.FillRectangle(Brushes.White, 0, h - logoH, logoW, logoH);

                            int crW = (int)Math.Max(380, w * 0.48);
                            int crH = (int)Math.Max(36, h * 0.045);
                            g.FillRectangle(Brushes.White, w - crW, h - crH, crW, crH);
                        }

                        using (var msMask = new MemoryStream())
                        {
                            maskBmp.Save(msMask, ImageFormat.Png);
                            byte[] maskBytes = msMask.ToArray();
                            byte[] imgBytes = File.ReadAllBytes(imagePath);

                            using (var content = new MultipartFormDataContent())
                            {
                                var imgContent = new ByteArrayContent(imgBytes);
                                imgContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                                content.Add(imgContent, "image_file", "image.png");

                                var maskContent = new ByteArrayContent(maskBytes);
                                maskContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                                content.Add(maskContent, "mask_file", "mask.png");

                                using (var request = new HttpRequestMessage(HttpMethod.Post, "https://clipdrop-api.co/cleanup/v1"))
                                {
                                    request.Headers.Add("x-api-key", apiKey);
                                    request.Content = content;

                                    var response = await _httpClient.SendAsync(request);
                                    if (response.IsSuccessStatusCode)
                                    {
                                        return await response.Content.ReadAsByteArrayAsync();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Gọi Google Generative Inpainting
        /// </summary>
        private static async Task<byte[]> InpaintViaGoogleAiAsync(string imagePath, string apiKey)
        {
            // Placeholder: Nếu người dùng cấu hình Google Gemini / Imagen
            // Khi chưa cấu hình đầy đủ sẽ fallback về Smart Inpaint
            await Task.Delay(50);
            return null;
        }
    }
}
