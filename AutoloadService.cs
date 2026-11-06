using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AVMap
{
    public static class AutoloadService
    {
        private const string BundleNameAcad = "AVMap.bundle";
        private const string BundleNameEnji = "AVMap_enji.bundle";

        public static bool IsEnjiCad
        {
            get
            {
                try
                {
                    string asmName = Assembly.GetExecutingAssembly().GetName().Name;
                    if (asmName.IndexOf("enji", StringComparison.OrdinalIgnoreCase) >= 0) return true;

                    string procName = Process.GetCurrentProcess().ProcessName;
                    if (procName.IndexOf("enji", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        procName.IndexOf("gscad", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
                catch { }
                return false;
            }
        }

        public static string GetBundleFolder()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (IsEnjiCad)
            {
                return Path.Combine(appData, "enjiCAD", "ApplicationPlugins", BundleNameEnji);
            }
            else
            {
                return Path.Combine(appData, "Autodesk", "ApplicationPlugins", BundleNameAcad);
            }
        }

        public static string GetPackageXmlPath()
        {
            return Path.Combine(GetBundleFolder(), "PackageContents.xml");
        }

        /// <summary>
        /// Kiểm tra tính năng Tự khởi động cùng CAD hiện đang BẬT hay TẮT
        /// </summary>
        public static bool IsAutoloadEnabled()
        {
            try
            {
                // 1. Kiểm tra qua gói .bundle
                string xmlPath = GetPackageXmlPath();
                if (File.Exists(xmlPath))
                {
                    string content = File.ReadAllText(xmlPath);
                    if (content.IndexOf("LoadOnAutoCADStartup=\"True\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        content.IndexOf("LoadOnAutoCADStartup=\"true\"", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                // 2. Nếu là enjiCAD, kiểm tra thêm trong Registry HKCU
                if (IsEnjiCad)
                {
                    if (CheckEnjiCadRegistryEnabled()) return true;
                }
            }
            catch { }
            return false;
        }

        private static bool CheckEnjiCadRegistryEnabled()
        {
            try
            {
                string[] rootKeys = new[] { @"Software\enjiCAD", @"Software\Gstarsoft\GstarCAD" };
                foreach (var rk in rootKeys)
                {
                    using (var baseKey = Registry.CurrentUser.OpenSubKey(rk))
                    {
                        if (baseKey == null) continue;
                        foreach (var subName in baseKey.GetSubKeyNames())
                        {
                            using (var appKey = baseKey.OpenSubKey($@"{subName}\Applications\AVMap"))
                            {
                                if (appKey != null)
                                {
                                    object loadCtrl = appKey.GetValue("LOADCTRLS");
                                    if (loadCtrl != null && Convert.ToInt32(loadCtrl) == 2)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static void RegisterEnjiCadRegistry(string dllPath)
        {
            try
            {
                string[] rootKeys = new[] { @"Software\enjiCAD", @"Software\Gstarsoft\GstarCAD" };
                foreach (var rk in rootKeys)
                {
                    using (var baseKey = Registry.CurrentUser.OpenSubKey(rk, true))
                    {
                        if (baseKey == null) continue;
                        foreach (var subName in baseKey.GetSubKeyNames())
                        {
                            using (var verKey = baseKey.OpenSubKey(subName, true))
                            {
                                if (verKey == null) continue;
                                using (var appKey = verKey.CreateSubKey(@"Applications\AVMap"))
                                {
                                    if (appKey != null)
                                    {
                                        appKey.SetValue("DESCRIPTION", "AVMap - Ban Do Nen & GIS enjiCAD", RegistryValueKind.String);
                                        appKey.SetValue("LOADCTRLS", 2, RegistryValueKind.DWord);
                                        appKey.SetValue("LOADER", dllPath, RegistryValueKind.String);
                                        appKey.SetValue("MANAGED", 1, RegistryValueKind.DWord);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static void UnregisterEnjiCadRegistry()
        {
            try
            {
                string[] rootKeys = new[] { @"Software\enjiCAD", @"Software\Gstarsoft\GstarCAD" };
                foreach (var rk in rootKeys)
                {
                    using (var baseKey = Registry.CurrentUser.OpenSubKey(rk, true))
                    {
                        if (baseKey == null) continue;
                        foreach (var subName in baseKey.GetSubKeyNames())
                        {
                            using (var verKey = baseKey.OpenSubKey(subName, true))
                            {
                                if (verKey == null) continue;
                                try { verKey.DeleteSubKeyTree(@"Applications\AVMap", false); } catch { }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Bật tính năng tự khởi động cùng CAD thông qua cơ chế .bundle + Registry
        /// </summary>
        public static bool EnableAutoload(out string error)
        {
            error = null;
            try
            {
                string bundleDir = GetBundleFolder();
                string contentsDir = Path.Combine(bundleDir, "Contents");
                if (!Directory.Exists(contentsDir)) Directory.CreateDirectory(contentsDir);

                string sourceDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string mainDllName = IsEnjiCad ? "AVMap_enjiCAD.dll" : "AVMap_AutoCAD.dll";

                // Sao chép các file cần thiết từ thư mục đang chạy sang Contents của .bundle
                string[] filesToCopy = new[]
                {
                    mainDllName,
                    "Newtonsoft.Json.dll",
                    "Magick.NET-Q8-x64.dll",
                    "Magick.Native-Q8-x64.dll",
                    "WebView2Loader.dll",
                    "version.json"
                };

                foreach (var file in filesToCopy)
                {
                    try
                    {
                        string src = Path.Combine(sourceDir, file);
                        string dst = Path.Combine(contentsDir, file);
                        if (File.Exists(src))
                        {
                            File.Copy(src, dst, true);
                        }
                    }
                    catch { }
                }

                // Ghi file PackageContents.xml chuẩn Autodesk / enjiCAD Autoloader
                string platform = IsEnjiCad ? "enjiCAD*" : "AutoCAD*";
                string desc = IsEnjiCad ? "AVMap - Ban Do Nen &amp; GIS enjiCAD" : "AVMap - Ban Do Nen &amp; GIS AutoCAD";
                string xmlContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<ApplicationPackage SchemaVersion=""1.0""
                    AppVersion=""1.4.0""
                    Author=""AVMap Team""
                    ProductCode=""{{0F72D04A-D8A8-4F11-8BD4-73891461A639}}""
                    Name=""AVMap""
                    Description=""{desc}"">
  <CompanyDetails Name=""AVMap"" Url=""https://github.com/tienhoaa/AVMap"" />
  <Components Description=""CAD Components"">
    <RuntimeRequirements OS=""Win64"" Platform=""{platform}"" SeriesMin=""R19.0"" />
    <ComponentEntry AppName=""AVMap""
                    ModuleName=""./Contents/{mainDllName}""
                    AppType="".NET""
                    LoadOnAutoCADStartup=""True"" />
  </Components>
</ApplicationPackage>";

                File.WriteAllText(GetPackageXmlPath(), xmlContent, System.Text.Encoding.UTF8);

                // Nếu là enjiCAD, hỗ trợ thêm Registry và Gstarsoft
                if (IsEnjiCad)
                {
                    string targetDll = Path.Combine(contentsDir, mainDllName);
                    RegisterEnjiCadRegistry(targetDll);

                    try
                    {
                        string gstarDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gstarsoft", "ApplicationPlugins", BundleNameEnji);
                        string gstarContents = Path.Combine(gstarDir, "Contents");
                        if (!Directory.Exists(gstarContents)) Directory.CreateDirectory(gstarContents);
                        foreach (var file in filesToCopy)
                        {
                            string src = Path.Combine(sourceDir, file);
                            string dst = Path.Combine(gstarContents, file);
                            if (File.Exists(src)) File.Copy(src, dst, true);
                        }
                        File.WriteAllText(Path.Combine(gstarDir, "PackageContents.xml"), xmlContent, System.Text.Encoding.UTF8);
                    }
                    catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Hủy / Xóa tính năng tự khởi động cùng CAD (về lại trạng thái chỉ gõ NETLOAD khi cần)
        /// </summary>
        public static bool DisableAutoload(out string error)
        {
            error = null;
            try
            {
                string xmlPath = GetPackageXmlPath();
                if (File.Exists(xmlPath))
                {
                    File.Delete(xmlPath);
                }

                string bundleDir = GetBundleFolder();
                try
                {
                    if (Directory.Exists(bundleDir))
                    {
                        Directory.Delete(bundleDir, true);
                    }
                }
                catch
                {
                    try
                    {
                        if (File.Exists(xmlPath)) File.Move(xmlPath, xmlPath + ".disabled");
                    }
                    catch { }
                }

                // Dọn dẹp cả bản enjiCAD
                if (IsEnjiCad)
                {
                    UnregisterEnjiCadRegistry();

                    try
                    {
                        string gstarDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gstarsoft", "ApplicationPlugins", BundleNameEnji);
                        string gstarXml = Path.Combine(gstarDir, "PackageContents.xml");
                        if (File.Exists(gstarXml)) File.Delete(gstarXml);
                        if (Directory.Exists(gstarDir)) Directory.Delete(gstarDir, true);
                    }
                    catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Hộp thoại tương tác thông minh cho người dùng Bật / Tắt theo ý muốn
        /// </summary>
        public static void ToggleAutoloadWithPrompt(Action onStatusChanged = null)
        {
            try
            {
                bool isCurrentlyOn = IsAutoloadEnabled();
                string cadName = IsEnjiCad ? "enjiCAD" : "AutoCAD";

                if (!isCurrentlyOn)
                {
                    var result = MessageBox.Show(
                        $"Bạn có muốn BẬT tính năng TỰ ĐỘNG KHỞI ĐỘNG AVMap cùng {cadName} mỗi khi mở máy không?\n\n" +
                        "• Cơ chế: Chuẩn .bundle và Registry (cực kỳ sạch sẽ, an toàn).\n" +
                        $"• Tiện ích: Từ phiên làm việc sau, khi bạn mở {cadName}, thanh công cụ AVMap sẽ sẵn sàng ngay mà KHÔNG CẦN gõ lệnh NETLOAD.\n\n" +
                        "👉 Bấm YES: BẬT tự động khởi động cùng CAD.\n" +
                        "👉 Bấm NO: Giữ nguyên (khi nào dùng mới gõ NETLOAD).",
                        $"Cài Đặt Khởi Động Cùng {cadName} - AVMap",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        if (EnableAutoload(out string err))
                        {
                            onStatusChanged?.Invoke();
                            MessageBox.Show(
                                $"✔ ĐÃ BẬT TỰ ĐỘNG KHỞI ĐỘNG CÙNG {cadName.ToUpper()} THÀNH CÔNG!\n\n" +
                                $"Từ lần mở {cadName} tiếp theo, AVMap sẽ tự nạp sẵn sàng 100%.\n" +
                                $"Bạn có thể đóng {cadName} và mở lại để kiểm tra.",
                                "AVMap - Cài Đặt Khởi Động",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show($"Không thể kích hoạt tự khởi động:\n{err}", "AVMap", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
                else
                {
                    var result = MessageBox.Show(
                        $"AVMap hiện ĐANG được cài đặt TỰ ĐỘNG KHỞI ĐỘNG cùng {cadName}.\n\n" +
                        "Bạn có muốn HỦY / TẮT tính năng tự khởi động này không?\n\n" +
                        $"• Sau khi hủy: {cadName} sẽ mở nguyên bản sạch sẽ, không tự nạp AVMap nữa (tránh làm rối giao diện).\n" +
                        "• Khi nào cần dùng: Bạn chỉ cần gõ lệnh NETLOAD để chọn file DLL như bình thường.\n\n" +
                        "👉 Bấm YES: HỦY tự khởi động cùng CAD.\n" +
                        "👉 Bấm NO: Giữ nguyên tự khởi động.",
                        $"Hủy Khởi Động Cùng {cadName} - AVMap",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        if (DisableAutoload(out string err))
                        {
                            onStatusChanged?.Invoke();
                            MessageBox.Show(
                                $"✔ ĐÃ HỦY TỰ ĐỘNG KHỞI ĐỘNG CÙNG {cadName.ToUpper()}!\n\n" +
                                $"Lần sau khi mở {cadName}, AVMap sẽ không tự nạp nữa.\n" +
                                "Khi nào cần sử dụng, bạn chỉ việc gõ lệnh NETLOAD.",
                                "AVMap - Cài Đặt Khởi Động",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show($"Không thể hủy tự khởi động:\n{err}", "AVMap", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi thao tác: {ex.Message}", "AVMap", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
