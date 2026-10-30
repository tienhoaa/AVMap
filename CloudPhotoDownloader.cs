using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AVMap
{
    public static class CloudPhotoDownloader
    {
        private static readonly HttpClient client;
        private static readonly string cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVMap", "SurveyCloud");

        static CloudPhotoDownloader()
        {
            if (!Directory.Exists(cacheFolder))
            {
                Directory.CreateDirectory(cacheFolder);
            }

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            };
            client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public static void ClearCache()
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

        public static long GetCacheSizeBytes()
        {
            try
            {
                if (!Directory.Exists(cacheFolder)) return 0;
                long total = 0;
                foreach (var file in Directory.GetFiles(cacheFolder))
                {
                    try { total += new FileInfo(file).Length; } catch { }
                }
                return total;
            }
            catch { return 0; }
        }

        public static async Task<List<string>> DownloadPhotosFromUrlAsync(string inputUrl, string targetFolder = null, Action<int, int, string> progressCallback = null)
        {
            List<string> downloadedFiles = new List<string>();
            if (string.IsNullOrWhiteSpace(inputUrl)) return downloadedFiles;

            string saveDir = !string.IsNullOrWhiteSpace(targetFolder) ? targetFolder : cacheFolder;
            if (!Directory.Exists(saveDir))
            {
                try { Directory.CreateDirectory(saveDir); } catch { }
            }

            string trimmedUrl = inputUrl.Trim();

            // 1. Kiểm tra nếu là link Google Photos Album / Chia sẻ
            if (trimmedUrl.Contains("photos.app.goo.gl") || trimmedUrl.Contains("photos.google.com"))
            {
                downloadedFiles = await DownloadFromGooglePhotosAsync(trimmedUrl, saveDir, progressCallback);
            }
            // 2. Kiểm tra nếu là link Google Drive File
            else if (trimmedUrl.Contains("drive.google.com/file/d/"))
            {
                string downloaded = await DownloadFromGoogleDriveFileAsync(trimmedUrl, saveDir, progressCallback);
                if (!string.IsNullOrEmpty(downloaded)) downloadedFiles.Add(downloaded);
            }
            // 3. Link ảnh trực tiếp
            else if (trimmedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                string downloaded = await DownloadDirectImageAsync(trimmedUrl, saveDir, 1, 1, progressCallback);
                if (!string.IsNullOrEmpty(downloaded)) downloadedFiles.Add(downloaded);
            }

            return downloadedFiles;
        }

        private static async Task<List<string>> DownloadFromGooglePhotosAsync(string albumUrl, string saveDir, Action<int, int, string> progressCallback)
        {
            List<string> result = new List<string>();
            progressCallback?.Invoke(0, 0, "Đang kết nối đến album Google Photos...");

            try
            {
                // Tải trang HTML của album chia sẻ
                string html = await client.GetStringAsync(albumUrl);

                // Trích xuất các liên kết ảnh gốc trên máy chủ Google (lh3.googleusercontent.com)
                Regex regex = new Regex(@"https://lh3\.googleusercontent\.com/(?:pw/)?[a-zA-Z0-9_\-]+", RegexOptions.IgnoreCase);
                MatchCollection matches = regex.Matches(html);

                HashSet<string> photoUrls = new HashSet<string>();
                foreach (Match match in matches)
                {
                    string url = match.Value;
                    if (url.Length > 55)
                    {
                        photoUrls.Add(url);
                    }
                }

                if (photoUrls.Count == 0)
                {
                    progressCallback?.Invoke(0, 0, "Không tìm thấy ảnh công khai trong link Google Photos này.");
                    return result;
                }

                int total = photoUrls.Count;
                int current = 0;
                long sessionTag = DateTime.Now.Ticks;

                foreach (string baseUrl in photoUrls)
                {
                    current++;
                    try
                    {
                        string downloadUrl = baseUrl + "=d";
                        progressCallback?.Invoke(current, total, $"Đang tải ảnh {current}/{total} từ Google Photos (giữ nguyên GPS)...");

                        byte[] data = await client.GetByteArrayAsync(downloadUrl);
                        if (data != null && data.Length > 1024)
                        {
                            string fileName = $"GP_{sessionTag}_{current:D3}.jpg";
                            string savePath = Path.Combine(saveDir, fileName);
                            File.WriteAllBytes(savePath, data);
                            result.Add(savePath);
                        }
                    }
                    catch { }
                }

                progressCallback?.Invoke(total, total, $"Đã tải xong {result.Count}/{total} ảnh từ Google Photos!");
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke(0, 0, $"Lỗi tải từ Google Photos: {ex.Message}");
            }

            return result;
        }

        private static async Task<string> DownloadFromGoogleDriveFileAsync(string driveUrl, string saveDir, Action<int, int, string> progressCallback)
        {
            progressCallback?.Invoke(1, 1, "Đang kết nối Google Drive...");
            try
            {
                Match match = Regex.Match(driveUrl, @"drive\.google\.com/file/d/([a-zA-Z0-9_\-]+)");
                if (!match.Success) return null;

                string fileId = match.Groups[1].Value;
                string downloadUrl = $"https://drive.google.com/uc?export=download&id={fileId}";

                byte[] data = await client.GetByteArrayAsync(downloadUrl);
                if (data != null && data.Length > 1024)
                {
                    string fileName = $"GD_{fileId}_{DateTime.Now.Ticks}.jpg";
                    string savePath = Path.Combine(saveDir, fileName);
                    File.WriteAllBytes(savePath, data);
                    return savePath;
                }
            }
            catch { }
            return null;
        }

        private static async Task<string> DownloadDirectImageAsync(string directUrl, string saveDir, int current, int total, Action<int, int, string> progressCallback)
        {
            try
            {
                progressCallback?.Invoke(current, total, $"Đang tải ảnh trực tiếp {current}/{total}...");
                byte[] data = await client.GetByteArrayAsync(directUrl);
                if (data != null && data.Length > 1024)
                {
                    string ext = ".jpg";
                    if (directUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) ext = ".png";
                    else if (directUrl.EndsWith(".heic", StringComparison.OrdinalIgnoreCase)) ext = ".heic";

                    string fileName = $"Direct_{DateTime.Now.Ticks}_{current}{ext}";
                    string savePath = Path.Combine(saveDir, fileName);
                    File.WriteAllBytes(savePath, data);
                    return savePath;
                }
            }
            catch { }
            return null;
        }
    }
}
