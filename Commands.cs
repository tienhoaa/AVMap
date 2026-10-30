using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace AVMap
{
    public class Commands : IExtensionApplication
    {
        static Commands()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    string requestedName = new System.Reflection.AssemblyName(args.Name).Name;
                    string name = requestedName + ".dll";

                    // 1. Tìm file DLL cùng thư mục với AVMap
                    try
                    {
                        string folder = Path.GetDirectoryName(typeof(Commands).Assembly.Location);
                        if (!string.IsNullOrEmpty(folder))
                        {
                            string[] candidates = new[]
                            {
                                Path.Combine(folder, name),
                                Path.Combine(folder, "Release_AutoCAD", name),
                                Path.Combine(folder, "bin", "Release", "net48", name)
                            };
                            foreach (var path in candidates)
                            {
                                if (File.Exists(path)) return System.Reflection.Assembly.LoadFrom(path);
                            }
                        }
                    }
                    catch { }

                    // 2. Tự động nạp trực tiếp từ Embedded Resource bên trong file DLL (gửi 1 file DLL duy nhất vẫn chạy 100%)
                    var curAsm = typeof(Commands).Assembly;
                    foreach (string resName in curAsm.GetManifestResourceNames())
                    {
                        if (resName.EndsWith("." + name, StringComparison.OrdinalIgnoreCase) ||
                            resName.EndsWith("." + requestedName, StringComparison.OrdinalIgnoreCase))
                        {
                            using (var stream = curAsm.GetManifestResourceStream(resName))
                            {
                                if (stream != null)
                                {
                                    byte[] bytes = new byte[stream.Length];
                                    stream.Read(bytes, 0, bytes.Length);
                                    return System.Reflection.Assembly.Load(bytes);
                                }
                            }
                        }
                    }
                }
                catch { }
                return null;
            };

            ExtractNativeWebView2Loader();
        }

        private static void ExtractNativeWebView2Loader()
        {
            try
            {
                string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVMap");
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                string targetFile = Path.Combine(targetDir, "WebView2Loader.dll");
                if (!File.Exists(targetFile))
                {
                    var curAsm = typeof(Commands).Assembly;
                    foreach (string resName in curAsm.GetManifestResourceNames())
                    {
                        if (resName.EndsWith("WebView2Loader.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var stream = curAsm.GetManifestResourceStream(resName))
                            {
                                if (stream != null)
                                {
                                    byte[] bytes = new byte[stream.Length];
                                    stream.Read(bytes, 0, bytes.Length);
                                    File.WriteAllBytes(targetFile, bytes);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (File.Exists(targetFile))
                {
                    ConfigureWebView2LoaderPath(targetDir);
                }
            }
            catch { }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ConfigureWebView2LoaderPath(string path)
        {
            try
            {
                Microsoft.Web.WebView2.Core.CoreWebView2Environment.SetLoaderDllFolderPath(path);
            }
            catch { }
        }

        public void Initialize()
        {
            try
            {
                // Reset danh sách lịch sử hệ tọa độ gần đây theo từng phiên NETLOAD
                VN2000Catalog.ResetSessionRecent();

                // FILEDIA & CMDDIA Guardian: Tự động phát hiện và khôi phục hộp thoại Open/Save nếu bị tắt về 0
                Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("FILEDIA", 1);
                Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("CMDDIA", 1);
                Autodesk.AutoCAD.ApplicationServices.Application.Idle -= FilediaGuardian_OnIdle;
                Autodesk.AutoCAD.ApplicationServices.Application.Idle += FilediaGuardian_OnIdle;

                // Lắng nghe sự kiện tắt file CAD: Tự động dọn dẹp sạch sẽ toàn bộ file ảnh nền tạm & cache
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.DocumentDestroyed -= DocumentManager_DocumentDestroyed;
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.DocumentDestroyed += DocumentManager_DocumentDestroyed;

                RibbonManager.InitializeRibbon();
                var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\n=======================================================");
                ed?.WriteMessage("\n  AVMap - Bản Đồ Nền & GIS AutoCAD (Đã khởi động thành công!)");
                ed?.WriteMessage("\n  Đã kích hoạt Hộ Vệ FILEDIA = 1 (Chống lỗi mất hộp thoại file)");
                ed?.WriteMessage("\n  Thanh Ribbon AVMap đã sẵn sàng trên thanh công cụ.");
                ed?.WriteMessage("\n  Gõ lệnh AVHELP hoặc AVHD để mở Bảng Hướng Dẫn Sử Dụng.");
                ed?.WriteMessage("\n=======================================================\n");

                // Tự động mở Hướng dẫn sử dụng khi nạp AVMap
                if (HelpWindow.ShouldShowOnStartup())
                {
                    Autodesk.AutoCAD.ApplicationServices.Application.Idle -= ShowWelcomeOnIdle;
                    Autodesk.AutoCAD.ApplicationServices.Application.Idle += ShowWelcomeOnIdle;
                }

                // Kiểm tra cập nhật ngầm sau khi CAD nạp xong
                System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(3000);
                    await UpdateService.CheckUpdateAsync(false);
                });
            }
            catch { }
        }

        private static void ShowWelcomeOnIdle(object sender, EventArgs e)
        {
            try
            {
                Autodesk.AutoCAD.ApplicationServices.Application.Idle -= ShowWelcomeOnIdle;
                var win = new HelpWindow();
                Autodesk.AutoCAD.ApplicationServices.Application.ShowModelessWindow(win);
            }
            catch { }
        }

        private static void FilediaGuardian_OnIdle(object sender, EventArgs e)
        {
            try
            {
                object filediaVal = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("FILEDIA");
                if (filediaVal != null && Convert.ToInt32(filediaVal) == 0)
                {
                    Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("FILEDIA", 1);
                }
                object cmddiaVal = Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("CMDDIA");
                if (cmddiaVal != null && Convert.ToInt32(cmddiaVal) == 0)
                {
                    Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("CMDDIA", 1);
                }
            }
            catch { }
        }

        [CommandMethod("AVRIBBON")]
        public void ShowRibbonCommand()
        {
            RibbonManager.CreateRibbon();
        }

        public void Terminate()
        {
            try { Autodesk.AutoCAD.ApplicationServices.Application.Idle -= FilediaGuardian_OnIdle; } catch { }
            try { Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.DocumentDestroyed -= DocumentManager_DocumentDestroyed; } catch { }
            RealtimeMapEngine.Stop();
            CleanAllTempCaches();
        }

        private static void DocumentManager_DocumentDestroyed(object sender, DocumentDestroyedEventArgs e)
        {
            CleanAllTempCaches();
        }

        private static void CleanAllTempCaches()
        {
            try
            {
                // Khi tắt file bản vẽ hoặc thoát CAD: Tự động dọn sạch toàn bộ ảnh nền ghép và cache tạm
                GoogleTileClient.ClearAllTileCache();
                CloudPhotoDownloader.ClearCache();
                SurveyPhotoService.ClearSurveyCache();
            }
            catch { }
        }

        [CommandMethod("AVCLEAN")]
        [CommandMethod("AVCLEARTEMP")]
        public void CleanTempCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            try
            {
                CleanAllTempCaches();
                StreetViewService.RemoveMarker(doc);
                ed?.WriteMessage("\n[AVMap] ĐÃ QUÉT DỌN SẠCH SẼ TOÀN BỘ FILE TẠM TRONG BỘ NHỚ ĐỆM (CACHE = 0 MB)!\n");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n[AVMap Lỗi]: {ex.Message}");
            }
        }

        [CommandMethod("AVFILEDIA")]
        [CommandMethod("AVFIX")]
        public void FixFilediaCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            try
            {
                Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("FILEDIA", 1);
                Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("CMDDIA", 1);
                Autodesk.AutoCAD.ApplicationServices.Application.SetSystemVariable("ATTDIA", 1);
                doc?.Editor?.WriteMessage("\n[AVMap] ĐÃ KHÔI PHỤC FILEDIA = 1 & CMDDIA = 1! Hộp thoại mở/lưu file đã hoạt động trở lại bình thường.\n");
            }
            catch (System.Exception ex)
            {
                doc?.Editor?.WriteMessage($"\n[AVMap Lỗi]: {ex.Message}");
            }
        }

        [CommandMethod("AVMAP")]
        public void AvMapMenuCommand()
        {
            RibbonManager.CreateRibbon();
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var crs = CrsManager.GetActiveCrs(doc);
            string crsInfo = crs != null ? $"{crs.Name} (KTT {crs.CentralMeridian}°)" : "Chưa thiết lập";

            doc.Editor.WriteMessage($"\n--- THÔNG TIN AVMAP ---");
            doc.Editor.WriteMessage($"\n• Trạng thái nền động: {(RealtimeMapEngine.IsRunning ? "ĐANG BẬT" : "ĐANG TẮT")}");
            doc.Editor.WriteMessage($"\n• Hệ tọa độ hiện tại: {crsInfo}");
            doc.Editor.WriteMessage($"\n• Loại bản đồ: {GoogleTileClient.ActiveMapType}");
            doc.Editor.WriteMessage($"\n-----------------------\n");
        }

        [CommandMethod("AVCRS")]
        public void SetCrsCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var crs = CrsManager.ShowCrsSelectionDialog(doc);
            if (crs != null && RealtimeMapEngine.IsRunning)
            {
                RealtimeMapEngine.TriggerUpdate();
            }
        }

        [CommandMethod("AVMAPON")]
        public void MapOnCommand()
        {
            RealtimeMapEngine.Start();
        }

        [CommandMethod("AVMAPOFF")]
        public void MapOffCommand()
        {
            RealtimeMapEngine.Stop();
        }

        [CommandMethod("AVMAPHIDE")]
        public void MapHideAliasCommand()
        {
            RealtimeMapEngine.Stop();
        }

        [CommandMethod("AVHIDE")]
        public void AvHideAliasCommand()
        {
            RealtimeMapEngine.Stop();
        }

        [CommandMethod("AVOFF")]
        public void AvOffAliasCommand()
        {
            RealtimeMapEngine.Stop();
        }

        [CommandMethod("AVMAPTYPE")]
        public void MapTypeCommand()
        {
            var win = new MapTypeWindow();
            Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(win);
            if (win.DialogResult == true && RealtimeMapEngine.IsRunning)
            {
                RealtimeMapEngine.TriggerUpdate();
            }
        }

        [CommandMethod("AVINSERTORTHO")]
        public void InsertOrthoCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            OrthoImageService.InsertOrthoImage(doc);
        }

        [CommandMethod("AVBAYCHUP")]
        public void BayChupAliasCommand()
        {
            InsertOrthoCommand();
        }

        [CommandMethod("AVCHENANHKS")]
        public void InsertSurveyPhotoCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            SurveyPhotoService.InsertSurveyPhotosCommand(doc);
        }

        [CommandMethod("AVANHKS")]
        public void AnhKsAliasCommand()
        {
            InsertSurveyPhotoCommand();
        }

        [CommandMethod("AVPHOTO")]
        public void PhotoAliasCommand()
        {
            InsertSurveyPhotoCommand();
        }

        [CommandMethod("AVGETVECTORDATA")]
        public void GetVectorDataCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            VectorDataService.ExtractVectorDataCommand(doc);
        }

        [CommandMethod("AVBINHDOVECTOR")]
        public void BinhDoVectorAliasCommand()
        {
            GetVectorDataCommand();
        }

        [CommandMethod("AVVECTOR")]
        public void VectorAliasCommand()
        {
            GetVectorDataCommand();
        }

        [CommandMethod("AVCONTOUR")]
        [CommandMethod("AVDONGMUC")]
        [CommandMethod("AVCONTOURS")]
        public void ContourCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            ContourService.GenerateContoursCommand(doc);
        }

        [CommandMethod("AVCAPTURE")]
        public void CaptureCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            CaptureService.CaptureCurrentView(doc);
        }

        [CommandMethod("AVCAPTUREBOX")]
        public void CaptureBoxCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            CaptureService.OpenCaptureBoxDialog(doc);
        }

        [CommandMethod("AVREFRESH")]
        public void RefreshCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            RealtimeMapEngine.TriggerUpdate();
        }

        [CommandMethod("AVSTREETVIEW")]
        public void StreetViewCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            StreetViewService.ExecuteStreetView(doc);
        }

        [CommandMethod("AVSV")]
        [CommandMethod("AVSVLINES")]
        [CommandMethod("AVSTREETVIEWLINES")]
        public void ToggleStreetViewLinesCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            StreetViewService.ToggleCoverageOverlay(doc);
        }

        [CommandMethod("AVSVOFF")]
        public void HideStreetViewLinesCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            StreetViewService.RemoveCoverageOverlay(doc);
            doc?.Editor?.WriteMessage("\n[AVMap] Đã ẩn mạng lưới đường Google Street View.\n");
        }

        [CommandMethod("AVKMZ")]
        [CommandMethod("AVEXPORTKMZ")]
        public void ExportKmzCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            KmzWindow win = new KmzWindow(doc, 0);
            Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(win);
        }

        [CommandMethod("AVIMPORTKMZ")]
        public void ImportKmzCommand()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            KmzWindow win = new KmzWindow(doc, 1);
            Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(win);
        }

        [CommandMethod("AVHELP")]
        [CommandMethod("AVHD")]
        [CommandMethod("AVABOUT")]
        public void ShowHelpCommand()
        {
            try
            {
                var win = new HelpWindow();
                Autodesk.AutoCAD.ApplicationServices.Application.ShowModelessWindow(win);
            }
            catch (System.Exception ex)
            {
                var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n[AVMap Lỗi]: {ex.Message}");
            }
        }

        [CommandMethod("AVUPDATE")]
        [CommandMethod("AVCHECKUPDATE")]
        [CommandMethod("AVCAPNHAT")]
        public async void UpdateCommand()
        {
            await UpdateService.CheckUpdateAsync(true);
        }
    }
}
