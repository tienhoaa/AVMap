using System;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AVMap
{
    public static class RealtimeMapEngine
    {
        private const string BackgroundLayerName = "_AVMAP_BACKGROUND_";
        private const string BackgroundDefName = "AVMAP_BG_DEF";
        private static System.Windows.Forms.Timer debounceTimer;
        private static bool isRunning = false;
        private static bool isDownloading = false;
        private static bool hasPendingUpdate = false;
        private static Point3d lastCenter = Point3d.Origin;
        private static double lastSize = 0;
        private static Document currentDoc = null;
        private static string previousImageFile = null;

        public static bool IsRunning => isRunning;

        public static void Start()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            ProvinceCrs crs = CrsManager.GetActiveCrs(doc);
            if (crs == null)
            {
                // Người dùng bấm Bật Nền mà chưa chọn hệ tọa độ -> Tự động mở ngay hộp thoại chọn VN-2000!
                // Chọn xong là tự động nạp ảnh nền ngay, không bắt người dùng phải làm theo thứ tự cứng nhắc!
                crs = CrsManager.ShowCrsSelectionDialog(doc);
                if (crs == null)
                {
                    ed.WriteMessage("\n[AVMap] Đã hủy chọn hệ tọa độ. Chưa bật bản đồ nền.\n");
                    return;
                }
            }

            if (GoogleTileClient.ActiveMapType != null && GoogleTileClient.ActiveMapType.Contains(",svv"))
            {
                GoogleTileClient.ActiveMapType = GoogleTileClient.ActiveMapType.Replace(",svv", "");
            }
            StreetViewService.RemoveCoverageOverlay(doc);

            if (currentDoc != doc)
            {
                if (currentDoc != null)
                {
                    try { currentDoc.ViewChanged -= Doc_ViewChanged; } catch { }
                }
                currentDoc = doc;
                doc.ViewChanged -= Doc_ViewChanged;
                doc.ViewChanged += Doc_ViewChanged;
                lastCenter = Point3d.Origin;
                lastSize = 0;
            }

            if (isRunning)
            {
                ed.WriteMessage("\n[AVMap] Đang làm mới bản đồ nền...");
                TriggerUpdate();
                return;
            }

            isRunning = true;
            lastCenter = Point3d.Origin;
            lastSize = 0;

            if (debounceTimer == null)
            {
                debounceTimer = new System.Windows.Forms.Timer();
                debounceTimer.Interval = 400; // 400ms debounce
                debounceTimer.Tick += DebounceTimer_Tick;
            }

            doc.ViewChanged -= Doc_ViewChanged;
            doc.ViewChanged += Doc_ViewChanged;

            CadApp.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated;
            CadApp.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;

            ed.WriteMessage($"\n[AVMap] ĐÃ BẬT BẢN ĐỒ NỀN GOOGLE MAPS!");
            ed.WriteMessage($"\n[AVMap] Hệ tọa độ: {crs.Name} (KTT {crs.CentralMeridian}°). Đang nạp gạch ảnh vệ tinh...");

            TriggerUpdate();
        }

        private static void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            if (!isRunning || e.Document == null) return;
            currentDoc = e.Document;
            try { e.Document.ViewChanged -= Doc_ViewChanged; } catch { }
            e.Document.ViewChanged += Doc_ViewChanged;
            lastCenter = Point3d.Origin;
            lastSize = 0;
            TriggerUpdate();
        }

        public static void Stop()
        {
            // Bấm ẩn/tắt nền thì luôn dừng và xóa sạch ảnh nền
            isRunning = false;

            try { CadApp.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated; } catch { }

            if (debounceTimer != null)
            {
                debounceTimer.Stop();
                debounceTimer.Tick -= DebounceTimer_Tick;
                debounceTimer.Dispose();
                debounceTimer = null;
            }

            Document doc = CadApp.DocumentManager.MdiActiveDocument ?? currentDoc;
            if (doc != null)
            {
                try { doc.ViewChanged -= Doc_ViewChanged; } catch { }
                RemoveBackgroundImages(doc);
                StreetViewService.RemoveCoverageOverlay(doc);
                doc.Editor?.WriteMessage("\n[AVMap] Đã ẨN bản đồ nền. Bản vẽ sạch sẽ trở lại.\n");
            }
            currentDoc = null;
            lastCenter = Point3d.Origin;
            lastSize = 0;
        }

        private static void Doc_ViewChanged(object sender, EventArgs e)
        {
            if (!isRunning) return;
            debounceTimer?.Stop();
            debounceTimer?.Start();
        }

        public static void TriggerUpdate()
        {
            lastCenter = Point3d.Origin;
            lastSize = 0;
            debounceTimer?.Stop();
            debounceTimer?.Start();
        }

        private static async void DebounceTimer_Tick(object sender, EventArgs e)
        {
            debounceTimer?.Stop();
            if (!isRunning) return;

            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            if (GoogleTileClient.ActiveMapType != null && GoogleTileClient.ActiveMapType.Contains(",svv"))
            {
                GoogleTileClient.ActiveMapType = GoogleTileClient.ActiveMapType.Replace(",svv", "");
            }
            StreetViewService.RemoveCoverageOverlay(doc);

            if (currentDoc != doc)
            {
                currentDoc = doc;
                doc.ViewChanged -= Doc_ViewChanged;
                doc.ViewChanged += Doc_ViewChanged;
                lastCenter = Point3d.Origin;
                lastSize = 0;
            }
            Editor ed = doc.Editor;

            ProvinceCrs crs = CrsManager.GetActiveCrs(doc);
            if (crs == null) return;

            // 0. Bảo vệ Layout / PaperSpace để phục vụ in ấn:
            // TILEMODE = 1: Tab Model | TILEMODE = 0: Tab Layout
            // CVPORT = 1: Đang ở PaperSpace (trang giấy in, khung tên)
            // Nếu người dùng đang dàn trang ở Layout (PaperSpace), tuyệt đối không tính toán hay xóa ảnh nền ModelSpace,
            // nhờ đó khung nhìn Viewport trên Layout luôn hiển thị trọn vẹn bản đồ nền để in ấn ra PDF / máy in!
            try
            {
                short tilemode = Convert.ToInt16(CadApp.GetSystemVariable("TILEMODE"));
                short cvport = Convert.ToInt16(CadApp.GetSystemVariable("CVPORT"));
                if (tilemode == 0 && cvport == 1)
                {
                    return;
                }
            }
            catch { }

            // 1. Đọc thông tin khung nhìn trực tiếp trên UI Thread (Tuyệt đối an toàn)
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
                catch
                {
                    return;
                }
            }

            double viewWidth = viewHeight * (screenW / screenH);
            if (viewHeight <= 0 || viewWidth <= 0) return;

            // Kiểm tra xem khung nhìn có thay đổi đáng kể không
            if (Math.Abs(cx - lastCenter.X) < 1.0 &&
                Math.Abs(cy - lastCenter.Y) < 1.0 &&
                Math.Abs(viewHeight - lastSize) < 1.0)
            {
                return;
            }

            // Kiểm tra tọa độ cục bộ (gần gốc 0,0)
            if (Math.Abs(cx) < 50000 && Math.Abs(cy) < 50000)
            {
                try
                {
                    Database db = doc.Database;
                    if (db != null && (Math.Abs(db.Extmax.X) > 100000 || Math.Abs(db.Extmax.Y) > 500000))
                    {
                        ed.WriteMessage($"\n[AVMap Lưu Ý] Màn hình hiện tại đang ở gốc (0,0), trong khi bản vẽ có đối tượng ở tọa độ VN-2000 (X={db.Extmin.X:F0}, Y={db.Extmin.Y:F0}).");
                        ed.WriteMessage("\n        Vui lòng dùng lệnh ZOOM -> E (Zoom Extents) để đưa màn hình về vị trí tim tuyến VN-2000!\n");
                    }
                    else
                    {
                        ed.WriteMessage($"\n[AVMap Lưu Ý] Tâm màn hình (X={cx:F0}, Y={cy:F0}) đang ở vùng tọa độ cục bộ (gần gốc 0,0).");
                        ed.WriteMessage("\n        Vui lòng Zoom đến khu vực tim tuyến / bình đồ có tọa độ VN-2000 thực tế để nạp ảnh nền.\n");
                    }
                }
                catch { }
                return;
            }

            // Tự động nhận diện đơn vị bản vẽ: Mét (m) hay Milimét (mm)
            double unitScale = (Math.Abs(cx) > 10000000 || Math.Abs(cy) > 10000000) ? 0.001 : 1.0;

            // Nếu đang trong quá trình tải một đợt trước đó, đánh dấu có bản cập nhật mới
            if (isDownloading)
            {
                hasPendingUpdate = true;
                return;
            }

            lastCenter = new Point3d(cx, cy, 0);
            lastSize = viewHeight;

            // Quy đổi sang Mét để chuyển sang WGS-84
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

            while ((maxTX - minTX + 1) * (maxTY - minTY + 1) > 25 && zoom > 4)
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

            string mapType = GoogleTileClient.ActiveMapType;

            // 2. Tải và ghép ảnh trên luồng phụ không làm đơ AutoCAD
            isDownloading = true;
            string imageFile = null;
            try
            {
                imageFile = await Task.Run(() => GoogleTileClient.StitchTilesToFileAsync(minTX, maxTX, minTY, maxTY, zoom, mapType));
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[AVMap Lỗi Mạng]: {ex.Message}");
            }
            finally
            {
                isDownloading = false;
            }

            // 3. Sau khi await hoàn tất, code tự động quay về UI Thread của AutoCAD
            if (string.IsNullOrEmpty(imageFile) || !File.Exists(imageFile))
            {
                return;
            }

            if (!isRunning || doc.IsDisposed) return;

            ApplyMapImageToDatabase(doc, imageFile, originX, originY, widthMeters, heightMeters, unitScale);

            if (hasPendingUpdate)
            {
                hasPendingUpdate = false;
                TriggerUpdate();
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

            while ((maxTX - minTX + 1) * (maxTY - minTY + 1) > 25 && zoom > 4)
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

            string mapType = forceMapType ?? (GoogleTileClient.ActiveMapType?.Replace(",svv", "") ?? "y");

            string imageFile = null;
            try
            {
                imageFile = Task.Run(async () => await GoogleTileClient.StitchTilesToFileAsync(minTX, maxTX, minTY, maxTY, zoom, mapType).ConfigureAwait(false)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[AVMap Lỗi Mạng]: {ex.Message}");
                return false;
            }

            if (string.IsNullOrEmpty(imageFile) || !File.Exists(imageFile)) return false;

            lastCenter = new Point3d(cx, cy, 0);
            lastSize = viewHeight;

            return ApplyMapImageToDatabase(doc, imageFile, originX, originY, widthMeters, heightMeters, unitScale);
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
                    // Đảm bảo Layer hợp lệ để hiển thị ảnh nền
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
                    ObjectId defId = ObjectId.Null;
                    RasterImageDef imgDef = null;

                    if (imgDict.Contains(BackgroundDefName))
                    {
                        defId = imgDict.GetAt(BackgroundDefName);
                        imgDef = (RasterImageDef)tr.GetObject(defId, OpenMode.ForWrite);
                        try { imgDef.Unload(true); } catch { }
                        imgDef.SourceFileName = imageFile;
                        imgDef.Load();
                    }
                    else
                    {
                        imgDef = new RasterImageDef();
                        imgDef.SourceFileName = imageFile;
                        imgDef.Load();
                        defId = imgDict.SetAt(BackgroundDefName, imgDef);
                        tr.AddNewlyCreatedDBObject(imgDef, true);
                    }

                    // Tìm kiếm RasterImage nền hiện hữu để tái sử dụng, tránh xóa/tạo liên tục gây giật lag
                    RasterImage img = null;
                    ObjectId imgId = ObjectId.Null;
                    var extraImagesToClean = new System.Collections.Generic.List<RasterImage>();

                    foreach (ObjectId id in btr)
                    {
                        if (id.IsValid && !id.IsErased)
                        {
                            if (tr.GetObject(id, OpenMode.ForRead) is RasterImage rImg && rImg.Layer == BackgroundLayerName)
                            {
                                if (img == null)
                                {
                                    rImg.UpgradeOpen();
                                    img = rImg;
                                    imgId = id;
                                }
                                else
                                {
                                    rImg.UpgradeOpen();
                                    extraImagesToClean.Add(rImg);
                                }
                            }
                        }
                    }

                    // Nếu có ảnh thừa từ phiên làm việc trước, dọn dẹp an toàn khi tắt reactor
                    if (extraImagesToClean.Count > 0)
                    {
                        RasterImage.EnableReactors(false);
                        foreach (var extra in extraImagesToClean)
                        {
                            try { extra.Erase(true); } catch { }
                        }
                        RasterImage.EnableReactors(true);
                    }

                    // Quy đổi kích thước theo đơn vị bản vẽ (Mét hoặc Milimét)
                    double cadWidth = widthMeters / unitScale;
                    double cadHeight = heightMeters / unitScale;
                    Vector3d uVector = new Vector3d(cadWidth, 0, 0);
                    Vector3d vVector = new Vector3d(0, cadHeight, 0);
                    Point3d originPoint = new Point3d(originX / unitScale, originY / unitScale, 0);

                    if (img == null)
                    {
                        img = new RasterImage();
                        img.ImageDefId = defId;
                        img.Layer = BackgroundLayerName;
                        imgId = btr.AppendEntity(img);
                        tr.AddNewlyCreatedDBObject(img, true);

                        // Quan trọng cho Civil 3D & AutoCAD 2026: Phải Associate trước khi gán Orientation & ClipBoundary!
                        RasterImage.EnableReactors(true);
                        try { img.AssociateRasterDef(imgDef); } catch { }

                        img.Orientation = new CoordinateSystem3d(originPoint, uVector, vVector);
                        img.ShowImage = true;
                        img.ImageTransparency = false;
                        img.SetClipBoundaryToWholeImage();
                    }
                    else
                    {
                        if (img.ImageDefId != defId)
                        {
                            img.ImageDefId = defId;
                            try { img.AssociateRasterDef(imgDef); } catch { }
                        }

                        img.Orientation = new CoordinateSystem3d(originPoint, uVector, vVector);
                        img.ShowImage = true;
                        img.ImageTransparency = false;
                        img.SetClipBoundaryToWholeImage();
                    }

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

                    tr.Commit();
                }

                // Tự động xóa file ảnh nền của khung nhìn trước đó để tránh đầy ổ cứng
                if (!string.IsNullOrEmpty(previousImageFile) && previousImageFile != imageFile)
                {
                    try { if (File.Exists(previousImageFile)) File.Delete(previousImageFile); } catch { }
                }
                previousImageFile = imageFile;

                // Force Regeneration cho Civil 3D và AutoCAD 2026 DirectX 12
                try { ed.Regen(); } catch { }
                ed.UpdateScreen();
                ed.WriteMessage($"\n[AVMap] Đã nạp ảnh nền thành công ({widthMeters:F0}m x {heightMeters:F0}m)!");
                return true;
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[AVMap Lỗi Vẽ]: {ex.Message}");
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
                    // Tắt Layer nền
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (lt.Has(BackgroundLayerName))
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[BackgroundLayerName], OpenMode.ForWrite);
                        ltr.IsOff = true;
                    }

                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    var defsToErase = new System.Collections.Generic.List<ObjectId>();

                    RasterImage.EnableReactors(false);
                    try
                    {
                        foreach (ObjectId id in btr)
                        {
                            if (id.IsValid && !id.IsErased)
                            {
                                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                                if (ent is RasterImage rImg && ent.Layer == BackgroundLayerName)
                                {
                                    if (!rImg.ImageDefId.IsNull && rImg.ImageDefId.IsValid)
                                    {
                                        defsToErase.Add(rImg.ImageDefId);
                                    }
                                    ent.UpgradeOpen();
                                    ent.Erase(true);
                                }
                            }
                        }

                        foreach (var defId in defsToErase)
                        {
                            try
                            {
                                DBObject defObj = tr.GetObject(defId, OpenMode.ForWrite);
                                defObj.Erase(true);
                            }
                            catch { }
                        }
                    }
                    finally
                    {
                        RasterImage.EnableReactors(true);
                    }

                    tr.Commit();
                }

                GoogleTileClient.ClearStitchedCache();
                previousImageFile = null;
                try { doc.Editor?.Regen(); } catch { }
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