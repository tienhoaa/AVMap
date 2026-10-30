using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;

namespace AVMapUpdater
{
    public class UpdaterForm : Form
    {
        private string _targetDir;
        private string _mode; // "acad" or "enji"
        private string _cadExe;
        private string _dwgPath;
        private int _cadPid;
        private string _versionUrl;

        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblStatus;
        private Label lblDetails;
        private ProgressBar prgDownload;
        private Panel pnlHeader;
        private Button btnCancel;

        private bool _isUpdating = false;

        public UpdaterForm(string targetDir, string mode, string cadExe, string dwgPath, int cadPid, string versionUrl)
        {
            _targetDir = targetDir;
            _mode = string.IsNullOrEmpty(mode) ? "acad" : mode.ToLower();
            _cadExe = cadExe;
            _dwgPath = dwgPath;
            _cadPid = cadPid;
            _versionUrl = string.IsNullOrEmpty(versionUrl) ?
                "https://raw.githubusercontent.com/tienhoaa/AVMap/main/version.json" : versionUrl;

            InitializeUi();
        }

        private void InitializeUi()
        {
            this.Text = "AVMap - Cập Nhật File Trực Tiếp";
            this.Size = new Size(580, 310);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.BackColor = Color.FromArgb(248, 250, 252);
            this.Font = new Font("Segoe UI", 9.5f);

            // Header Panel
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(20, 10, 20, 10)
            };

            lblTitle = new Label
            {
                Text = "⚡ AVMAP FAST AUTO-UPDATER",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 12),
                AutoSize = true
            };

            lblSubtitle = new Label
            {
                Text = "Chỉ tải các file cập nhật cần thiết, ghi đè trực tiếp & mở lại CAD tự động",
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(20, 40),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);
            this.Controls.Add(pnlHeader);

            // Status Labels
            lblStatus = new Label
            {
                Text = "Đang chuẩn bị tiến trình cập nhật...",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Location = new Point(25, 92),
                Size = new Size(520, 26)
            };
            this.Controls.Add(lblStatus);

            prgDownload = new ProgressBar
            {
                Location = new Point(25, 124),
                Size = new Size(515, 24),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };
            this.Controls.Add(prgDownload);

            lblDetails = new Label
            {
                Text = "Khởi tạo kết nối máy chủ...",
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(25, 158),
                Size = new Size(515, 45)
            };
            this.Controls.Add(lblDetails);

            btnCancel = new Button
            {
                Text = "Hủy bỏ",
                Location = new Point(440, 218),
                Size = new Size(100, 32),
                BackColor = Color.FromArgb(226, 232, 240),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { this.Close(); };
            this.Controls.Add(btnCancel);

            this.Shown += async (s, e) => { await StartFastUpdateAsync(); };
        }

        private async Task StartFastUpdateAsync()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            try
            {
                // BƯỚC 1: ĐÓNG AUTOCAD ĐỂ MỞ KHÓA FILE DLL
                lblStatus.Text = "Bước 1/3: Đang đóng AutoCAD để mở khóa file DLL...";
                lblDetails.Text = "AutoCAD cần đóng lại để hệ thống có thể ghi đè file mới.";
                prgDownload.Value = 20;

                await Task.Run(() =>
                {
                    if (_cadPid > 0)
                    {
                        try
                        {
                            var cadProc = Process.GetProcessById(_cadPid);
                            if (cadProc != null && !cadProc.HasExited)
                            {
                                cadProc.CloseMainWindow();
                                bool closed = cadProc.WaitForExit(7000);
                                if (!closed && !cadProc.HasExited)
                                {
                                    cadProc.Kill();
                                    cadProc.WaitForExit(3000);
                                }
                            }
                        }
                        catch { }
                    }
                });

                await Task.Delay(1000);

                // BƯỚC 2: TẢI DANH SÁCH FILE VÀ CHỈ TẢI CÁC FILE CẦN THIẾT
                lblStatus.Text = "Bước 2/3: Đang tải các file cập nhật cần thiết từ máy chủ...";
                lblDetails.Text = "Đang kết nối: " + _versionUrl;
                prgDownload.Value = 40;

                string jsonContent = "";
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "AVMap-FastUpdater");
                        string cacheBusterUrl = _versionUrl.Contains("?") ? $"{_versionUrl}&t={DateTime.UtcNow.Ticks}" : $"{_versionUrl}?t={DateTime.UtcNow.Ticks}";
                        jsonContent = await client.DownloadStringTaskAsync(new Uri(cacheBusterUrl));
                    }
                }
                catch (Exception ex)
                {
                    // Dự phòng đọc file version.json cục bộ
                    string localVerFile = Path.Combine(_targetDir, "version.json");
                    if (File.Exists(localVerFile))
                    {
                        jsonContent = File.ReadAllText(localVerFile);
                    }
                    else
                    {
                        throw new Exception($"Không thể kết nối máy chủ GitHub ({ex.Message}). Vui lòng đảm bảo kho GitHub tienhoaa/AVMap ở chế độ Public!");
                    }
                }

                var filesToUpdate = ParseFilesFromJson(jsonContent, _mode);

                if (filesToUpdate.Count == 0)
                {
                    // Dự phòng nếu không phân tích được JSON: tải trực tiếp DLL tương ứng
                    string defaultFile = (_mode == "enji") ? "AVMap_enjiCAD.dll" : "AVMap_AutoCAD.dll";
                    string defaultUrl = $"https://raw.githubusercontent.com/tienhoaa/AVMap/main/BAN_GIAO_AVMAP_BAN_MOI_AI/{(_mode == "enji" ? "enjiCAD" : "AutoCAD")}/{defaultFile}";
                    filesToUpdate.Add(new UpdateFileItem { Name = defaultFile, Url = defaultUrl });
                }

                // Tải từng file và ghi đè trực tiếp
                int count = 0;
                foreach (var item in filesToUpdate)
                {
                    count++;
                    lblDetails.Text = $"Đang tải ({count}/{filesToUpdate.Count}): {item.Name}...";

                    string targetFilePath = Path.Combine(_targetDir, item.Name);
                    string tempDownload = Path.Combine(Path.GetTempPath(), $"avm_{DateTime.Now.Ticks}_{item.Name}");

                    bool downloaded = false;
                    List<string> candidateUrls = new List<string> { item.Url };
                    string relUrl = $"https://github.com/tienhoaa/AVMap/releases/latest/download/{item.Name}";
                    if (!candidateUrls.Contains(relUrl)) candidateUrls.Add(relUrl);

                    foreach (var url in candidateUrls)
                    {
                        try
                        {
                            using (WebClient dlClient = new WebClient())
                            {
                                dlClient.Headers.Add("User-Agent", "AVMap-FastUpdater");
                                await dlClient.DownloadFileTaskAsync(new Uri(url), tempDownload);
                            }
                            if (File.Exists(tempDownload) && new FileInfo(tempDownload).Length > 1024)
                            {
                                downloaded = true;
                                break;
                            }
                        }
                        catch { }
                    }

                    if (!downloaded || !File.Exists(tempDownload))
                    {
                        throw new Exception($"Không thể tải file {item.Name} từ GitHub (Lỗi 404: File chưa được đẩy lên hoặc Kho GitHub đang ở chế độ Private). Hãy đảm bảo Kho GitHub tienhoaa/AVMap ở chế độ Public và đã chạy UPDATE_GITHUB.bat!");
                    }

                    // Ghi đè vào thư mục netload của người dùng
                    File.Copy(tempDownload, targetFilePath, true);

                    // Đồng bộ cả file bản AI nếu người dùng đang dùng bản này để netload
                    if (item.Name.Equals("AVMap_AutoCAD.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        string aiPath = Path.Combine(_targetDir, "AVMap_AutoCAD_AI.dll");
                        try { File.Copy(tempDownload, aiPath, true); } catch { }
                    }
                    else if (item.Name.Equals("AVMap_enjiCAD.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        string aiPath = Path.Combine(_targetDir, "AVMap_enjiCAD_AI.dll");
                        try { File.Copy(tempDownload, aiPath, true); } catch { }
                    }

                    try { File.Delete(tempDownload); } catch { }

                    prgDownload.Value = 40 + (int)((count / (double)filesToUpdate.Count) * 50); // 40% -> 90%
                }

                // Cập nhật cả file version.json cục bộ
                try
                {
                    string localVerFile = Path.Combine(_targetDir, "version.json");
                    File.WriteAllText(localVerFile, jsonContent);
                }
                catch { }

                prgDownload.Value = 100;

                // BƯỚC 3: MỞ LẠI AUTOCAD VÀ HOÀN TẤT
                lblStatus.Text = "Bước 3/3: Cập nhật thành công! Đang mở lại AutoCAD...";
                lblDetails.Text = "Các file mới đã được ghi đè hoàn chỉnh. AutoCAD đang khởi động lại...";
                btnCancel.Enabled = false;

                await Task.Delay(1200);

                if (!string.IsNullOrEmpty(_cadExe) && File.Exists(_cadExe))
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(_cadExe);
                        if (!string.IsNullOrEmpty(_dwgPath) && File.Exists(_dwgPath))
                        {
                            psi.Arguments = $"\"{_dwgPath}\"";
                        }
                        Process.Start(psi);
                    }
                    catch { }
                }

                await Task.Delay(1000);
                this.Close();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "❌ Quá trình cập nhật thất bại!";
                lblStatus.ForeColor = Color.Red;
                lblDetails.Text = "Lỗi: " + ex.Message;
                btnCancel.Text = "Đóng";
                btnCancel.Enabled = true;
            }
        }

        private List<UpdateFileItem> ParseFilesFromJson(string json, string mode)
        {
            var list = new List<UpdateFileItem>();
            try
            {
                string key = (mode == "enji") ? "files_enjiCAD" : "files_AutoCAD";
                int idx = json.IndexOf($"\"{key}\"");
                if (idx < 0) return list;

                int arrStart = json.IndexOf('[', idx);
                int arrEnd = json.IndexOf(']', arrStart);
                if (arrStart < 0 || arrEnd < 0) return list;

                string arrayStr = json.Substring(arrStart, arrEnd - arrStart + 1);

                // Phân tích các cặp "name" và "url"
                int cur = 0;
                while (cur < arrayStr.Length)
                {
                    int nameIdx = arrayStr.IndexOf("\"name\"", cur);
                    if (nameIdx < 0) break;
                    int valStart = arrayStr.IndexOf('"', nameIdx + 6) + 1;
                    int valEnd = arrayStr.IndexOf('"', valStart);
                    string name = arrayStr.Substring(valStart, valEnd - valStart);

                    int urlIdx = arrayStr.IndexOf("\"url\"", valEnd);
                    if (urlIdx < 0) break;
                    int urlStart = arrayStr.IndexOf('"', urlIdx + 5) + 1;
                    int urlEnd = arrayStr.IndexOf('"', urlStart);
                    string url = arrayStr.Substring(urlStart, urlEnd - urlStart);

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(url))
                    {
                        list.Add(new UpdateFileItem { Name = name.Trim(), Url = url.Trim() });
                    }
                    cur = urlEnd + 1;
                }
            }
            catch { }
            return list;
        }
    }

    public class UpdateFileItem
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }
}
