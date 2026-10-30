using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Colors;
using CadApp = Gssoft.Gscad.ApplicationServices.Application;

namespace AVMap.Enji
{
    public static class RealtimeMapEngine
    {
        private const string BackgroundLayerName = "_AVMAP_BACKGROUND_";

        private static System.Windows.Forms.Timer debounceTimer;
        private static bool isRunning = false;
        private static bool isDownloading = false;
        private static bool isUpdating = false;
        private static Point3d lastCenter = Point3d.Origin;
        private static double lastSize = 0;
        private static Document currentDoc = null;
        private static string previousImageFile = null;
        private static readonly List<ObjectId> trackedBgImageIds = new List<ObjectId>();

        public static bool IsRunning => isRunning;

        public static void Start()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            currentDoc = doc;
            Editor ed = doc.Editor;

            ProvinceCrs crs = CrsManager.EnsureCrsSelected(doc);
            if (crs == null)
            {
                ed.WriteMessage("\n[AVMap enjiCAD] Chưa chọn Hệ tọa độ VN-2000! Vui lòng chọn hệ tọa độ trước.");
                return;
            }

            if (isRunning)
            {
                ed.WriteMessage("\n[AVMap enjiCAD] Đang làm mới bản đồ nền theo khung nhìn hiện tại...");
                UpdateMapNow(doc);
                return;
            }

            isRunning = true;
            lastCenter = Point3d.Origin;
            lastSize = 0;

            if (debounceTimer == null)
            {
                debounceTimer = new System.Windows.Forms.Timer();
                debounceTimer.Interval = 500; // 500ms debounce mượt mà cho con lăn chuột
                debounceTimer.Tick += DebounceTimer_Tick;
            }

            try { doc.ViewChanged -= Doc_ViewChanged; } catch { }
            doc.ViewChanged += Doc_ViewChanged;

            foreach (Document d in CadApp.DocumentManager)
            {
                try { d.CommandEnded -= Doc_CommandEnded; } catch { }
                d.CommandEnded += Doc_CommandEnded;
            }

            CadApp.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated;
            CadApp.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;

            CadApp.DocumentManager.DocumentCreated -= DocumentManager_DocumentCreated;
            CadApp.DocumentManager.DocumentCreated += DocumentManager_DocumentCreated;

            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n  [AVMap enjiCAD] ĐÃ BẬT BẢN ĐỒ NỀN GOOGLE MAPS!");
            ed.WriteMessage($"\n  • Hệ tọa độ: {crs.Name} (KTT {crs.CentralMeridian}°)");
            ed.WriteMessage("\n  • Tự động tải bản đồ theo con lăn chuột (Zoom/Pan đến đâu tải đến đó).");
            ed.WriteMessage("\n  • Phím tắt làm mới tức thì: Gõ RE (Regen) hoặc bấm nút Làm Mới!");
            ed.WriteMessage("\n=======================================================\n");

            // Nạp và hiển thị ngay bản đồ cho khung nhìn ban đầu
            UpdateMapNow(doc);
        }

        public static void Stop()
        {
            isRunning = false;

            if (debounceTimer != null)
            {
                debounceTimer.Stop();
                debounceTimer.Tick -= DebounceTimer_Tick;
                debounceTimer.Dispose();
                debounceTimer = null;
            }

            foreach (Document d in CadApp.DocumentManager)
            {
                try { d.ViewChanged -= Doc_ViewChanged; } catch { }
                try { d.CommandEnded -= Doc_CommandEnded; } catch { }
            }

            try { CadApp.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated; } catch { }
            try { CadApp.DocumentManager.DocumentCreated -= DocumentManager_DocumentCreated; } catch { }

            Document doc = CadApp.DocumentManager.MdiActiveDocument ?? currentDoc;
            if (doc != null)
            {
                RemoveBackgroundImages(doc);
                doc.Editor?.WriteMessage("\n[AVMap enjiCAD] Đã ẨN bản đồ nền. Bản vẽ sạch sẽ trở lại.\n");
            }

            currentDoc = null;
            lastCenter = Point3d.Origin;
            lastSize = 0;
        }

        private static void Doc_ViewChanged(object sender, EventArgs e)
        {
            if (!isRunning || isUpdating) return;
            debounceTimer?.Stop();
            debounceTimer?.Start();
        }

        private static void DebounceTimer_Tick(object sender, EventArgs e)
        {
            debounceTimer?.Stop();
            if (!isRunning || isUpdating || isDownloading) return;

            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null || doc.IsDisposed) return;

            if (currentDoc != doc)
            {
                currentDoc = doc;
                try { doc.ViewChanged -= Doc_ViewChanged; } catch { }
                doc.ViewChanged += Doc_ViewChanged;
            }

            UpdateMapNow(doc);
        }

        private static void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            if (!isRunning || e.Document == null) return;
            currentDoc = e.Document;
            try { e.Document.ViewChanged -= Doc_ViewChanged; } catch { }
            e.Document.ViewChanged += Doc_ViewChanged;
            try { e.Document.CommandEnded -= Doc_CommandEnded; } catch { }
            e.Document.CommandEnded += Doc_CommandEnded;
        }

        private static void DocumentManager_DocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            if (!isRunning || e.Document == null) return;
            try { e.Document.ViewChanged -= Doc_ViewChanged; } catch { }
            e.Document.ViewChanged += Doc_ViewChanged;
            try { e.Document.CommandEnded -= Doc_CommandEnded; } catch { }
            e.Document.CommandEnded += Doc_CommandEnded;
        }

        private static void Doc_CommandEnded(object sender, CommandEventArgs e)
        {
            if (!isRunning || isUpdating) return;
            try
            {
                string cmd = e.GlobalCommandName?.TrimStart('_', '.', '#').ToUpperInvariant();
                if (cmd == "REGEN" || cmd == "REGENALL" || cmd == "RE" || cmd == "REA" || cmd == "REDRAW" || cmd == "REDRAWALL")
                {
                    Document doc = sender as Document ?? CadApp.DocumentManager.MdiActiveDocument;
                    if (doc != null && !doc.IsDisposed)
                    {
                        UpdateMapNow(doc);
                    }
                }
            }
            catch { }
        }

        public static void TriggerUpdate()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc != null && !doc.IsDisposed && isRunning)
            {
                UpdateMapNow(doc);
            }
        }

        public static bool UpdateMapNow(Document doc, string forceMapType = null)
        {
            if (doc == null || doc.IsDisposed) return false;
            Editor ed = doc.Editor;
            ProvinceCrs crs = CrsManager.GetActiveCrs(doc);
            if (crs == null) return false;

            double cx = 0, cy = 0;
            double viewHeight = 0;
            double screenW = 1920, screenH = 1080;

            try
            {
                object scr = CadApp.GetSystemVariable("SCREENSIZE");
                if (scr is Point2d sp) { screenW = Math.Max(100.0, sp.X); screenH = Math.Max(100.0, sp.Y); }
            }
            catch { }

            try
            {
                object vctr = CadApp.GetSystemVariable("VIEWCTR");
                if (vctr is Point3d p3) { cx = p3.X; cy = p3.Y; }
                else if (vctr is Point2d p2) { cx = p2.X; cy = p2.Y; }
                viewHeight = Convert.ToDouble(CadApp.GetSystemVariable("VIEWSIZE"));
            }
            catch
            {
                try
                {
                    using (ViewTableRecord view = ed.GetCurrentView())
                    {
                        cx = view.CenterPoint.X;
                        cy = view.CenterPoint.Y;
                        viewHeight = view.Height;
                    }
                }
                catch { return false; }
            }

            double viewWidth = viewHeight * (screenW / screenH);
            if (viewHeight <= 0 || viewWidth <= 0) return false;

            // Nếu góc nhìn thay đổi dưới 3% và cùng loại map, không cần tải lại để tiết kiệm mạng và tránh giật hình
            if (forceMapType == null && lastSize > 0 &&
                Math.Abs(viewHeight - lastSize) / lastSize < 0.03 &&
                Math.Abs(cx - lastCenter.X) < viewWidth * 0.03 &&
                Math.Abs(cy - lastCenter.Y) < viewHeight * 0.03)
            {
                return true;
            }

            double unitScale = (Math.Abs(cx) > 10000000 || Math.Abs(cy) > 10000000) ? 0.001 : 1.0;
            double minX = (cx - viewWidth / 2.0) * unitScale;
            double maxX = (cx + viewWidth / 2.0) * unitScale;
            double minY = (cy - viewHeight / 2.0) * unitScale;
            double maxY = (cy + viewHeight / 2.0) * unitScale;

            var ptSW = VN2000Transform.VN2000ToWGS84(minX, minY, crs.CentralMeridian, crs.ScaleFactor);
            var ptNE = VN2000Transform.VN2000ToWGS84(maxX, maxY, crs.CentralMeridian, crs.ScaleFactor);

            double viewHeightMeters = viewHeight * unitScale;
            int zoom = CalculateOptimalZoom(viewHeightMeters, screenH, (ptSW.Latitude + ptNE.Latitude) / 2.0);

            var (tileMinX, tileMaxY) = VN2000Transform.LatLonToTile(ptSW.Latitude, ptSW.Longitude, zoom);
            var (tileMaxX, tileMinY) = VN2000Transform.LatLonToTile(ptNE.Latitude, ptNE.Longitude, zoom);

            int minTX = Math.Min(tileMinX, tileMaxX);
            int maxTX = Math.Max(tileMinX, tileMaxX);
            int minTY = Math.Min(tileMinY, tileMaxY);
            int maxTY = Math.Max(tileMinY, tileMaxY);

            // Giới hạn số mảnh tải an toàn (không quá 20 mảnh mỗi chiều, tổng không quá 25)
            while (((maxTX - minTX + 1) * (maxTY - minTY + 1) > 25 || (maxTX - minTX + 1) > 20 || (maxTY - minTY + 1) > 20) && zoom > 4)
            {
                zoom--;
                var t1 = VN2000Transform.LatLonToTile(ptSW.Latitude, ptSW.Longitude, zoom);
                var t2 = VN2000Transform.LatLonToTile(ptNE.Latitude, ptNE.Longitude, zoom);
                minTX = Math.Min(t1.tileX, t2.tileX);
                maxTX = Math.Max(t1.tileX, t2.tileX);
                minTY = Math.Min(t1.tileY, t2.tileY);
                maxTY = Math.Max(t1.tileY, t2.tileY);
            }

            var nwGeo = VN2000Transform.TileToLatLon(minTX, minTY, zoom);
            var seGeo = VN2000Transform.TileToLatLon(maxTX + 1, maxTY + 1, zoom);

            var nwCad = VN2000Transform.WGS84ToVN2000(nwGeo.Latitude, nwGeo.Longitude, crs.CentralMeridian, crs.ScaleFactor);
            var seCad = VN2000Transform.WGS84ToVN2000(seGeo.Latitude, seGeo.Longitude, crs.CentralMeridian, crs.ScaleFactor);

            double originX = Math.Min(nwCad.X, seCad.X);
            double originY = Math.Min(nwCad.Y, seCad.Y);
            double widthMeters = Math.Abs(seCad.X - nwCad.X);
            double heightMeters = Math.Abs(nwCad.Y - seCad.Y);

            string mapType = forceMapType ?? GoogleTileClient.ActiveMapType;

            string imageFile = null;
            isDownloading = true;
            try
            {
                imageFile = Task.Run(async () => await GoogleTileClient.StitchTilesToFileAsync(minTX, maxTX, minTY, maxTY, zoom, mapType).ConfigureAwait(false)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[AVMap enjiCAD Lỗi Mạng]: {ex.Message}");
                return false;
            }
            finally
            {
                isDownloading = false;
            }

            if (string.IsNullOrEmpty(imageFile) || !File.Exists(imageFile)) return false;

            lastCenter = new Point3d(cx, cy, 0);
            lastSize = viewHeight;

            isUpdating = true;
            try
            {
                return ApplyMapImageToDatabase(doc, imageFile, originX, originY, widthMeters, heightMeters, unitScale);
            }
            finally
            {
                isUpdating = false;
            }
        }

        private static bool ApplyMapImageToDatabase(Document doc, string imageFile, double originX, double originY, double widthMeters, double heightMeters, double unitScale)
        {
            if (doc == null || doc.IsDisposed) return false;
            Editor ed = doc.Editor;
            try
            {
                Database db = doc.Database;
                if (db == null) return false;

                using (doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (!lt.Has(BackgroundLayerName))
                    {
                        lt.UpgradeOpen();
                        LayerTableRecord ltr = new LayerTableRecord
                        {
                            Name = BackgroundLayerName,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, 8),
                            IsOff = false,
                            IsFrozen = false,
                            IsLocked = false,
                            IsPlottable = true
                        };
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }
                    else
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[BackgroundLayerName], OpenMode.ForWrite);
                        ltr.IsOff = false;
                        ltr.IsFrozen = false;
                        ltr.IsPlottable = true;
                    }

                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    ObjectId imgDictId = RasterImageDef.GetImageDictionary(db);
                    if (imgDictId.IsNull)
                    {
                        imgDictId = RasterImageDef.CreateImageDictionary(db);
                    }

                    DBDictionary imgDict = (DBDictionary)tr.GetObject(imgDictId, OpenMode.ForWrite);

                    // Luôn tạo Definition MỚI với ID thời gian duy nhất để tránh xung đột bộ nhớ đệm
                    string defName = "AVMap_BG_" + DateTime.Now.Ticks;
                    RasterImageDef imgDef = new RasterImageDef();
                    imgDef.SourceFileName = imageFile;
                    imgDef.Load();
                    ObjectId defId = imgDict.SetAt(defName, imgDef);
                    tr.AddNewlyCreatedDBObject(imgDef, true);

                    RasterImage img = new RasterImage();
                    img.ImageDefId = defId;
                    img.Layer = BackgroundLayerName;
                    ObjectId imgId = btr.AppendEntity(img);
                    tr.AddNewlyCreatedDBObject(img, true);

                    RasterImage.EnableReactors(true);
                    try { img.AssociateRasterDef(imgDef); } catch { }

                    double cadWidth = widthMeters / unitScale;
                    double cadHeight = heightMeters / unitScale;
                    Vector3d uVector = new Vector3d(cadWidth, 0, 0);
                    Vector3d vVector = new Vector3d(0, cadHeight, 0);
                    Point3d originPoint = new Point3d(originX / unitScale, originY / unitScale, 0);

                    img.Orientation = new CoordinateSystem3d(originPoint, uVector, vVector);
                    img.ShowImage = true;
                    img.ImageTransparency = false;
                    img.SetClipBoundaryToWholeImage();

                    // Đưa ảnh mới xuống dưới cùng
                    if (!btr.DrawOrderTableId.IsNull && btr.DrawOrderTableId.IsValid)
                    {
                        try
                        {
                            DrawOrderTable dot = (DrawOrderTable)tr.GetObject(btr.DrawOrderTableId, OpenMode.ForWrite);
                            ObjectIdCollection ids = new ObjectIdCollection { imgId };
                            dot.MoveToBottom(ids);
                        }
                        catch { }
                    }

                    // Xóa siêu tốc các ảnh nền cũ đã theo dõi mà không duyệt hàng trăm ngàn thực thể trong bản vẽ nặng
                    if (trackedBgImageIds.Count > 0)
                    {
                        RasterImage.EnableReactors(false);
                        foreach (ObjectId oldId in trackedBgImageIds)
                        {
                            if (oldId != imgId && oldId.IsValid && !oldId.IsErased)
                            {
                                try
                                {
                                    DBObject obj = tr.GetObject(oldId, OpenMode.ForWrite);
                                    obj.Erase(true);
                                }
                                catch { }
                            }
                        }
                        RasterImage.EnableReactors(true);
                    }

                    trackedBgImageIds.Clear();
                    trackedBgImageIds.Add(imgId);

                    tr.Commit();
                }

                // Tự động xóa file ảnh nền của khung nhìn trước đó để tránh đầy ổ cứng
                if (!string.IsNullOrEmpty(previousImageFile) && previousImageFile != imageFile)
                {
                    try { if (File.Exists(previousImageFile)) File.Delete(previousImageFile); } catch { }
                }
                previousImageFile = imageFile;

                ed.UpdateScreen();
                return true;
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[AVMap enjiCAD Lỗi Vẽ]: {ex.Message}");
                return false;
            }
        }

        public static void RemoveBackgroundImages(Document doc)
        {
            if (doc == null || doc.IsDisposed) return;
            Database db = doc.Database;
            if (db == null) return;

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (lt.Has(BackgroundLayerName))
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[BackgroundLayerName], OpenMode.ForWrite);
                        ltr.IsOff = true;
                    }

                    if (trackedBgImageIds.Count > 0)
                    {
                        RasterImage.EnableReactors(false);
                        foreach (ObjectId oldId in trackedBgImageIds)
                        {
                            if (oldId.IsValid && !oldId.IsErased)
                            {
                                try
                                {
                                    DBObject obj = tr.GetObject(oldId, OpenMode.ForWrite);
                                    obj.Erase(true);
                                }
                                catch { }
                            }
                        }
                        RasterImage.EnableReactors(true);
                        trackedBgImageIds.Clear();
                    }

                    tr.Commit();
                }
                GoogleTileClient.ClearStitchedCache();
                previousImageFile = null;
                doc.Editor?.UpdateScreen();
            }
            catch { }
        }

        private static int CalculateOptimalZoom(double viewHeightMeters, double screenPixelHeight, double centerLat)
        {
            double metersPerPixel = viewHeightMeters / Math.Max(1.0, screenPixelHeight);
            double latRad = centerLat * Math.PI / 180.0;
            double z = Math.Floor(Math.Log(156543.03 * Math.Cos(latRad) / metersPerPixel) / Math.Log(2.0));
            return Math.Max(3, Math.Min(20, (int)z));
        }
    }
}
