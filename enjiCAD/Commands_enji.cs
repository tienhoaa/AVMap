using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Geometry;
using Gssoft.Gscad.Runtime;
using CadApp = Gssoft.Gscad.ApplicationServices.Application;

namespace AVMap.Enji
{
    public class Commands : IExtensionApplication
    {
        static Commands()
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    try
                    {
                        string folder = Path.GetDirectoryName(typeof(Commands).Assembly.Location);
                        string name = new System.Reflection.AssemblyName(args.Name).Name + ".dll";
                        string[] candidates = new[]
                        {
                            Path.Combine(folder, name),
                            Path.Combine(folder, "Release_enjiCAD", name),
                            Path.Combine(folder, "enjiCAD", "bin", "Release", "net8.0-windows", name)
                        };
                        foreach (var path in candidates)
                        {
                            if (File.Exists(path)) return System.Reflection.Assembly.LoadFrom(path);
                        }
                    }
                    catch { }
                    return null;
                };

                System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
                {
                    try
                    {
                        string folder = Path.GetDirectoryName(typeof(Commands).Assembly.Location);
                        string name = assemblyName.Name + ".dll";
                        string[] candidates = new[]
                        {
                            Path.Combine(folder, name),
                            Path.Combine(folder, "Release_enjiCAD", name),
                            Path.Combine(folder, "enjiCAD", "bin", "Release", "net8.0-windows", name)
                        };
                        foreach (var path in candidates)
                        {
                            if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
                        }
                    }
                    catch { }
                    return null;
                };
            }
            catch { }
        }

        public void Initialize()
        {
            try
            {
                // Reset danh sách lịch sử hệ tọa độ gần đây theo từng phiên NETLOAD
                VN2000Catalog.ResetSessionRecent();

                var ed = CadApp.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\n=======================================================");
                ed?.WriteMessage("\n  AVMap - Bản Đồ Nền & GIS cho enjiCAD 2026!");
                ed?.WriteMessage("\n  Thanh Ribbon AVMap đã được tích hợp lên thanh công cụ.");
                ed?.WriteMessage("\n  Gõ lệnh AVHELP hoặc AVHD để mở Bảng Hướng Dẫn Sử Dụng.");
                ed?.WriteMessage("\n=======================================================\n");

                // Lắng nghe sự kiện tắt file bản vẽ trong enjiCAD: Tự động dọn sạch file ảnh nền tạm & cache
                CadApp.DocumentManager.DocumentDestroyed -= DocumentManager_DocumentDestroyed;
                CadApp.DocumentManager.DocumentDestroyed += DocumentManager_DocumentDestroyed;

                RibbonManager_enji.InitializeRibbon();

                if (HelpWindow.ShouldShowOnStartup())
                {
                    CadApp.Idle -= ShowWelcomeOnIdle;
                    CadApp.Idle += ShowWelcomeOnIdle;
                }

                // Kiểm tra cập nhật ngầm sau khi enjiCAD nạp xong (sau 3s)
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
                CadApp.Idle -= ShowWelcomeOnIdle;
                var win = new HelpWindow();
                win.Show();
            }
            catch { }
        }

        public void Terminate()
        {
            try { CadApp.DocumentManager.DocumentDestroyed -= DocumentManager_DocumentDestroyed; } catch { }
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
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            try
            {
                CleanAllTempCaches();
                StreetViewService.RemoveMarker(doc);
                ed?.WriteMessage("\n[AVMap enjiCAD] ĐÃ QUÉT DỌN SẠCH SẼ TOÀN BỘ FILE TẠM TRONG BỘ NHỚ ĐỆM (CACHE = 0 MB)!\n");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n[AVMap Lỗi]: {ex.Message}");
            }
        }

        [CommandMethod("AVRIBBON")]
        public void ShowRibbonCommand()
        {
            RibbonManager_enji.CreateRibbon();
        }

        [CommandMethod("AVMAP")]
        public void AvMapMenuCommand()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            var crs = CrsManager.GetActiveCrs(doc);
            string crsInfo = crs != null ? $"{crs.Name} (KTT {crs.CentralMeridian}°)" : "Chưa thiết lập";

            doc.Editor.WriteMessage($"\n--- THÔNG TIN AVMAP enjiCAD ---");
            doc.Editor.WriteMessage($"\n• Trạng thái nền động: {(RealtimeMapEngine.IsRunning ? "ĐANG BẬT" : "ĐANG TẮT")}");
            doc.Editor.WriteMessage($"\n• Hệ tọa độ hiện tại: {crsInfo}");
            doc.Editor.WriteMessage($"\n• Loại bản đồ: {GoogleTileClient.ActiveMapType}");
            doc.Editor.WriteMessage($"\n-------------------------------\n");
        }

        [CommandMethod("AVCRS")]
        public void SetCrsCommand()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            CrsManager.ShowCrsSelectionDialog(doc);
        }

        [CommandMethod("AVMAPON")]
        [CommandMethod("AVSHOW")]
        public void MapOnCommand()
        {
            RealtimeMapEngine.Start();
        }

        [CommandMethod("AVMAPOFF")]
        [CommandMethod("AVHIDE")]
        [CommandMethod("AVOFF")]
        public void MapOffCommand()
        {
            RealtimeMapEngine.Stop();
        }

        [CommandMethod("AVMAPTYPE")]
        public void MapTypeCommand()
        {
            var win = new MapTypeWindow();
            if (win.ShowDialog() == true && RealtimeMapEngine.IsRunning)
            {
                Document doc = CadApp.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    RealtimeMapEngine.UpdateMapNow(doc);
                }
            }
        }

        [CommandMethod("AVCAPTURE")]
        public void CaptureCommand()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            try
            {
                CaptureService.CaptureCurrentView(doc);
            }
            catch (System.Exception ex)
            {
                doc?.Editor?.WriteMessage($"\n[AVMap enjiCAD Lỗi]: {ex.Message}");
            }
        }

        [CommandMethod("AVCAPTUREBOX")]
        public void CaptureBoxCommand()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            try
            {
                CaptureService.CaptureWindowArea(doc);
            }
            catch (System.Exception ex)
            {
                doc?.Editor?.WriteMessage($"\n[AVMap enjiCAD Lỗi]: {ex.Message}");
            }
        }

        [CommandMethod("AVINSERTORTHO")]
        [CommandMethod("AVBAYCHUP")]
        public void InsertOrthoCommand()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            OrthoImageService.InsertOrthoImage(doc);
        }

        [CommandMethod("AVCHENANHKS")]
        [CommandMethod("AVANHKS")]
        [CommandMethod("AVPHOTO")]
        public void InsertSurveyPhotoCommand()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            SurveyPhotoService.InsertSurveyPhotosCommand(doc);
        }

        [CommandMethod("AVGETVECTORDATA")]
        [CommandMethod("AVBINHDOVECTOR")]
        [CommandMethod("AVVECTOR")]
        public void GetVectorDataCommand()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            VectorDataService.ExtractVectorDataCommand(doc);
        }

        [CommandMethod("AVCONTOUR")]
        [CommandMethod("AVDONGMUC")]
        [CommandMethod("AVCONTOURS")]
        public void ContourCommand()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            ContourService.GenerateContoursCommand(doc);
        }

        [CommandMethod("AVREFRESH")]
        public void RefreshCommand()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                RealtimeMapEngine.UpdateMapNow(doc);
            }
        }

        [CommandMethod("AVSTREETVIEW")]
        public void StreetViewCommand()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            StreetViewService.ExecuteStreetView(doc);
        }

        [CommandMethod("AVKMZ")]
        [CommandMethod("AVEXPORTKMZ")]
        public void ExportKmzCommand()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            KmzWindow_enji win = new KmzWindow_enji(doc, 0);
            CadApp.ShowModalWindow(win);
        }

        [CommandMethod("AVIMPORTKMZ")]
        public void ImportKmzCommand()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            KmzWindow_enji win = new KmzWindow_enji(doc, 1);
            CadApp.ShowModalWindow(win);
        }

        [CommandMethod("AVHELP")]
        [CommandMethod("AVHD")]
        [CommandMethod("AVABOUT")]
        public void ShowHelpCommand()
        {
            try
            {
                var win = new HelpWindow();
                win.Show();
            }
            catch (System.Exception ex)
            {
                var ed = CadApp.DocumentManager.MdiActiveDocument?.Editor;
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

