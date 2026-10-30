using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using Forms = System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace AVMap
{
    public class UpdateInfo
    {
        public string NewVersion { get; set; }
        public string Title { get; set; }
        public string ChangeLogText { get; set; }
        public string DownloadUrlAcad { get; set; }
        public string DownloadUrlEnji { get; set; }
        public bool IsLatest { get; set; }
    }

    public static class UpdateService
    {
        // Đặt 1.1.0 để thử nghiệm kích hoạt popup cập nhật lên 1.2.0 sau khi NETLOAD
        public const string CurrentVersion = "1.1.0";
        private const string VersionCheckUrl = "https://raw.githubusercontent.com/tienhoaa/AVMap/main/version.json";
        private static bool _hasNotifiedThisSession = false;

        public static async Task CheckUpdateAsync(bool isManualCheck = false)
        {
            try
            {
                string json = null;
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(4);
                        client.DefaultRequestHeaders.Add("User-Agent", "AVMap-Client");
                        json = await client.GetStringAsync(VersionCheckUrl);
                    }
                }
                catch { }

                if (string.IsNullOrWhiteSpace(json))
                {
                    // Dự phòng đọc file version.json cục bộ nếu máy tính tạm mất mạng
                    try
                    {
                        string localFile = Path.Combine(Path.GetDirectoryName(typeof(UpdateService).Assembly.Location), "version.json");
                        if (!File.Exists(localFile)) localFile = @"C:\Users\GAMING-PC\Documents\AVMap\version.json";
                        if (File.Exists(localFile)) json = File.ReadAllText(localFile);
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    if (isManualCheck)
                    {
                        Forms.MessageBox.Show(
                            "Không thể kết nối máy chủ cập nhật GitHub lúc này.\n\nVui lòng kiểm tra kết nối mạng Internet hoặc thử lại sau.",
                            "AVMap Cập Nhật",
                            Forms.MessageBoxButtons.OK,
                            Forms.MessageBoxIcon.Warning);
                    }
                    return;
                }

                JObject obj = JObject.Parse(json);
                string remoteVerStr = (string)obj["version"];
                if (string.IsNullOrEmpty(remoteVerStr)) return;

                Version remoteVer = ParseVersion(remoteVerStr);
                Version localVer = ParseVersion(CurrentVersion);

                var info = new UpdateInfo
                {
                    NewVersion = remoteVerStr,
                    Title = (string)obj["title"] ?? "Bản cập nhật mới",
                    DownloadUrlAcad = (string)obj["downloadUrl_AutoCAD"] ?? "",
                    DownloadUrlEnji = (string)obj["downloadUrl_enjiCAD"] ?? "",
                    IsLatest = (remoteVer <= localVer)
                };

                if (obj["changeLog"] is JArray arr)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var item in arr)
                    {
                        sb.AppendLine("• " + item.ToString());
                    }
                    info.ChangeLogText = sb.ToString();
                }
                else
                {
                    info.ChangeLogText = (string)obj["changeLog"] ?? "Cập nhật tính năng mới và cải tiến hiệu năng.";
                }

                if (remoteVer > localVer)
                {
                    // 1. Cập nhật nút trên thanh Ribbon thành chuông thông báo
                    try
                    {
                        Type ribbonMgr = Type.GetType("AVMap.RibbonManager");
                        ribbonMgr?.GetMethod("SetUpdateNotification")?.Invoke(null, new object[] { info.NewVersion, info.ChangeLogText });
                    }
                    catch { }

                    // 2. Thông báo nổi bật ở đáy dòng lệnh CAD (chỉ hiện 1 lần trong phiên làm việc)
                    if (!_hasNotifiedThisSession || isManualCheck)
                    {
                        _hasNotifiedThisSession = true;
                        WriteCadMessage("\n================================================================================");
                        WriteCadMessage($"\n  🔔 [AVMAP] ĐÃ CÓ BẢN CẬP NHẬT MỚI: v{info.NewVersion}! (Đang dùng: v{CurrentVersion})");
                        WriteCadMessage("\n  Nội dung nâng cấp:");
                        if (!string.IsNullOrEmpty(info.ChangeLogText))
                        {
                            foreach (var line in info.ChangeLogText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                WriteCadMessage($"\n    {line}");
                            }
                        }
                        WriteCadMessage("\n  👉 Bấm vào thông báo ở góc màn hình hoặc nút [🔔 CÓ BẢN MỚI] trên Ribbon để cập nhật!");
                        WriteCadMessage("\n================================================================================\n");
                    }

                    if (!isManualCheck)
                    {
                        // SAU KHI NETLOAD: HIỆN POPUP Ở GÓC DƯỚI BÊN PHẢI MÀN HÌNH
                        ShowToastNotification(info);
                    }
                    else
                    {
                        // Người dùng bấm thủ công trên Ribbon: Mở bảng cập nhật đầy đủ
                        SafeShowModal(new UpdateWindow(info));
                    }
                }
                else
                {
                    // ĐÃ LÀ BẢN MỚI NHẤT:
                    if (!isManualCheck)
                    {
                        // SAU KHI NETLOAD: ĐÃ LÀ MỚI NHẤT THÌ KHỎI! Không hiện bất kỳ popup nào!
                        return;
                    }
                    else
                    {
                        // Người dùng tự tay bấm "Kiểm Tra Cập Nhật" trên Ribbon:
                        // Chỉ thông báo đã là mới nhất kèm nút Đóng, KHÔNG CÓ NÚT CẬP NHẬT
                        Forms.MessageBox.Show(
                            $"Bạn đang sử dụng phiên bản AVMap v{CurrentVersion} (Mới nhất).\n\nHệ thống đã được cập nhật đầy đủ, không có bản cập nhật nào mới.",
                            "AVMap - Kiểm Tra Cập Nhật",
                            Forms.MessageBoxButtons.OK,
                            Forms.MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                if (isManualCheck)
                {
                    Forms.MessageBox.Show(
                        $"Không thể kết nối máy chủ cập nhật lúc này.\nLỗi: {ex.Message}\n\n(Lưu ý: Hãy chắc chắn máy có kết nối mạng Internet)",
                        "AVMap Cập Nhật",
                        Forms.MessageBoxButtons.OK,
                        Forms.MessageBoxIcon.Warning);
                }
            }
        }

        public static void ShowToastNotification(UpdateInfo info)
        {
            try
            {
                Action showAction = () =>
                {
                    try
                    {
                        var toast = new ToastNotificationWindow(info);
                        toast.Show();
                    }
                    catch { }
                };

                var disp = GetUiDispatcher();
                if (disp != null && !disp.CheckAccess())
                {
                    disp.BeginInvoke(showAction);
                }
                else
                {
                    showAction();
                }
            }
            catch { }
        }

        private static System.Windows.Threading.Dispatcher GetUiDispatcher()
        {
            try
            {
                Type compMgr = Type.GetType("Autodesk.Windows.ComponentManager, AdWindows");
                if (compMgr != null)
                {
                    object ribbon = compMgr.GetProperty("Ribbon")?.GetValue(null, null);
                    if (ribbon is System.Windows.Threading.DispatcherObject dObj)
                    {
                        return dObj.Dispatcher;
                    }
                }
            }
            catch { }

            if (System.Windows.Application.Current != null)
            {
                return System.Windows.Application.Current.Dispatcher;
            }

            return null;
        }

        private static Version ParseVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return new Version(1, 0, 0);
            v = v.TrimStart('v', 'V');
            if (Version.TryParse(v, out Version res)) return res;
            return new Version(1, 0, 0);
        }

        public static void ShowModalDialog(Window win)
        {
            SafeShowModal(win);
        }

        public static void ExecuteUpdatePipeline(UpdateInfo info)
        {
            try
            {
                string asmLocation = typeof(UpdateService).Assembly.Location;
                string targetDir = Path.GetDirectoryName(asmLocation);
                string updaterExe = Path.Combine(targetDir, "AVMapUpdater.exe");

                if (!File.Exists(updaterExe))
                {
                    string[] candidates = new[]
                    {
                        Path.Combine(targetDir, "AVMapUpdater.exe"),
                        Path.Combine(targetDir, "..", "Updater", "bin", "Release", "net48", "AVMapUpdater.exe"),
                        @"C:\Users\GAMING-PC\Documents\AVMap\Updater\bin\Release\net48\AVMapUpdater.exe",
                        Path.Combine(targetDir, "..", "AVMapUpdater.exe")
                    };
                    foreach (var c in candidates)
                    {
                        if (File.Exists(c)) { updaterExe = c; break; }
                    }
                }

                if (!File.Exists(updaterExe))
                {
                    Forms.MessageBox.Show(
                        "Không tìm thấy công cụ AVMapUpdater.exe trong thư mục cài đặt!\nVui lòng kiểm tra lại thư mục hoặc nạp bản đầy đủ.",
                        "Lỗi Cập Nhật",
                        Forms.MessageBoxButtons.OK,
                        Forms.MessageBoxIcon.Error);
                    return;
                }

                string cadExe = Process.GetCurrentProcess().MainModule.FileName;
                string dwgPath = GetCurrentDwgPath();

                bool isEnji = asmLocation.IndexOf("enjiCAD", StringComparison.OrdinalIgnoreCase) >= 0;
                int cadPid = Process.GetCurrentProcess().Id;
                string mode = isEnji ? "enji" : "acad";
                string args = $"-targetDir \"{targetDir}\" -mode \"{mode}\" -cadExe \"{cadExe}\" -dwg \"{dwgPath}\" -cadPid {cadPid} -versionUrl \"{VersionCheckUrl}\"";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = updaterExe,
                    Arguments = args,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Forms.MessageBox.Show(
                    $"Lỗi khởi động trình cập nhật: {ex.Message}",
                    "Lỗi",
                    Forms.MessageBoxButtons.OK,
                    Forms.MessageBoxIcon.Error);
            }
        }

        private static Type FindCadAppType()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string name = asm.GetName().Name;
                    if (name.Equals("acmgd", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("gcmgd", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("accoremgd", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("gccoremgd", StringComparison.OrdinalIgnoreCase))
                    {
                        var t = asm.GetType("Autodesk.AutoCAD.ApplicationServices.Application") ??
                                asm.GetType("GrxCAD.ApplicationServices.Application");
                        if (t != null) return t;
                    }
                }
            }
            catch { }
            return null;
        }

        private static void WriteCadMessage(string msg)
        {
            try
            {
                Type appType = FindCadAppType();
                if (appType != null)
                {
                    object docMgr = appType.GetProperty("DocumentManager")?.GetValue(null, null);
                    if (docMgr != null)
                    {
                        object doc = docMgr.GetType().GetProperty("MdiActiveDocument")?.GetValue(docMgr, null);
                        if (doc != null)
                        {
                            object ed = doc.GetType().GetProperty("Editor")?.GetValue(doc, null);
                            if (ed != null)
                            {
                                var writeMethod = ed.GetType().GetMethod("WriteMessage", new[] { typeof(string) });
                                writeMethod?.Invoke(ed, new object[] { msg });
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static void SafeShowModal(Window win)
        {
            try
            {
                Type appType = FindCadAppType();
                if (appType != null)
                {
                    var showModal = appType.GetMethod("ShowModalWindow", new[] { typeof(Window) });
                    if (showModal != null)
                    {
                        showModal.Invoke(null, new object[] { win });
                        return;
                    }
                }
            }
            catch { }

            try
            {
                win.ShowDialog();
            }
            catch { }
        }

        private static string GetCurrentDwgPath()
        {
            try
            {
                Type appType = FindCadAppType();
                if (appType != null)
                {
                    object docMgr = appType.GetProperty("DocumentManager")?.GetValue(null, null);
                    if (docMgr != null)
                    {
                        object doc = docMgr.GetType().GetProperty("MdiActiveDocument")?.GetValue(docMgr, null);
                        if (doc != null)
                        {
                            object db = doc.GetType().GetProperty("Database")?.GetValue(doc, null);
                            if (db != null)
                            {
                                return (string)db.GetType().GetProperty("Filename")?.GetValue(db, null) ?? "";
                            }
                        }
                    }
                }
            }
            catch { }
            return "";
        }
    }
}