using System;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Colors;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AVMap
{
    public static class StreetViewService
    {
        private const string CoverageLayer = "_AVMAP_STREETVIEW_COVERAGE_";
        public const string MarkerLayer = "_AVMAP_STREETVIEW_MARKER_";
        private static bool isUpdatingCoverage = false;
        private static StreetViewWindow _activeStreetViewWindow = null;

        private static System.Windows.Threading.DispatcherTimer _markerThrottleTimer = null;
        private static double _pendingCadX, _pendingCadY, _pendingHeading;
        private static Document _pendingDoc;
        private static ProvinceCrs _pendingCrs;
        private static bool _hasPendingMarkerUpdate = false;

        private static void ThrottledUpdateMarker(Document doc, double cadX, double cadY, double heading, ProvinceCrs crs)
        {
            _pendingDoc = doc;
            _pendingCrs = crs;
            _pendingCadX = cadX;
            _pendingCadY = cadY;
            _pendingHeading = heading;
            _hasPendingMarkerUpdate = true;

            if (_markerThrottleTimer == null)
            {
                _markerThrottleTimer = new System.Windows.Threading.DispatcherTimer();
                _markerThrottleTimer.Interval = TimeSpan.FromMilliseconds(50);
                _markerThrottleTimer.Tick += (s, e) =>
                {
                    if (_hasPendingMarkerUpdate && _pendingDoc != null && !_pendingDoc.IsDisposed)
                    {
                        _hasPendingMarkerUpdate = false;
                        UpdateMarker(_pendingDoc, _pendingCadX, _pendingCadY, _pendingHeading, _pendingCrs);
                    }
                };
                _markerThrottleTimer.Start();
            }
        }

        private static void EnsureMarkerLayer(Database db, Transaction tr)
        {
            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(MarkerLayer))
            {
                lt.UpgradeOpen();
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = MarkerLayer,
                    Color = Color.FromRgb(255, 25, 25), // Đỏ cờ rực rỡ tương phản tuyệt đối trên nền đường xanh
                    IsPlottable = false,
                    Description = "AVMap - Vi tri & Huong nhin Google Street View (Do / Trang / Den)"
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }

        public static void UpdateMarker(Document doc, double cadX, double cadY, double heading, ProvinceCrs crs)
        {
            if (doc == null || doc.IsDisposed) return;
            try
            {
                using (doc.LockDocument())
                {
                    Database db = doc.Database;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        EnsureMarkerLayer(db, tr);

                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                        // Xóa marker cũ
                        foreach (ObjectId id in btr)
                        {
                            if (id.IsValid && !id.IsErased)
                            {
                                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                                if (ent != null && ent.Layer == MarkerLayer)
                                {
                                    ent.UpgradeOpen();
                                    ent.Erase(true);
                                }
                            }
                        }

                        // Nhận diện đơn vị m hay mm
                        double unitFactor = (Math.Abs(cadX) > 10000000 || Math.Abs(cadY) > 10000000) ? 1000.0 : 1.0;
                        double rOuter = 4.0 * unitFactor;
                        double coneLen = 22.0 * unitFactor;
                        double tipLen = 28.0 * unitFactor;

                        Point3d center = new Point3d(cadX, cadY, 0);

                        // Góc CAD: 0° heading (Bắc, +Y) -> 90° CAD; 90° heading (Đông, +X) -> 0° CAD
                        double cadAngleRad = (90.0 - heading) * Math.PI / 180.0;
                        double halfFovRad = 32.0 * Math.PI / 180.0; // 64° góc quét tầm nhìn

                        Color vividRed = Color.FromRgb(255, 25, 25);   // Đỏ cờ siêu nổi bật trên vệt đường xanh Street View
                        Color pureWhite = Color.FromRgb(255, 255, 255); // Trắng sáng viền tương phản
                        Color solidBlack = Color.FromRgb(20, 20, 20);   // Đen đậm làm điểm nhấn

                        Point2d ptArrowStart = new Point2d(cadX + (rOuter + 0.3 * unitFactor) * Math.Cos(cadAngleRad),
                                                           cadY + (rOuter + 0.3 * unitFactor) * Math.Sin(cadAngleRad));
                        Point2d ptArrowHeadBase = new Point2d(cadX + 17.0 * unitFactor * Math.Cos(cadAngleRad),
                                                              cadY + 17.0 * unitFactor * Math.Sin(cadAngleRad));
                        Point2d ptArrowTip = new Point2d(cadX + tipLen * Math.Cos(cadAngleRad),
                                                         cadY + tipLen * Math.Sin(cadAngleRad));
                        Point2d ptArrowTipOuter = new Point2d(cadX + (tipLen + 0.8 * unitFactor) * Math.Cos(cadAngleRad),
                                                              cadY + (tipLen + 0.8 * unitFactor) * Math.Sin(cadAngleRad));

                        // 1. VIỀN LÓT TRẮNG SÁNG CHO MŨI TÊN (Tách biệt hoàn toàn khỏi đường xanh bên dưới)
                        Polyline arrowBorder = new Polyline();
                        arrowBorder.Layer = MarkerLayer;
                        arrowBorder.Color = pureWhite;
                        arrowBorder.AddVertexAt(0, ptArrowStart, 0, 1.8 * unitFactor, 1.8 * unitFactor);
                        arrowBorder.AddVertexAt(1, ptArrowHeadBase, 0, 5.2 * unitFactor, 0.0);
                        arrowBorder.AddVertexAt(2, ptArrowTipOuter, 0, 0, 0);

                        btr.AppendEntity(arrowBorder);
                        tr.AddNewlyCreatedDBObject(arrowBorder, true);

                        // 2. MŨI TÊN ĐỎ CỜ NỔI BẬT (Solid Arrowhead vuốt nhọn về phía trước)
                        Polyline arrow = new Polyline();
                        arrow.Layer = MarkerLayer;
                        arrow.Color = vividRed;
                        arrow.AddVertexAt(0, ptArrowStart, 0, 1.2 * unitFactor, 1.2 * unitFactor);
                        arrow.AddVertexAt(1, ptArrowHeadBase, 0, 4.2 * unitFactor, 0.0);
                        arrow.AddVertexAt(2, ptArrowTip, 0, 0, 0);

                        btr.AppendEntity(arrow);
                        tr.AddNewlyCreatedDBObject(arrow, true);

                        // 3. SỐNG MŨI TÊN TRẮNG (Center Spine)
                        Line spine = new Line(new Point3d(ptArrowStart.X, ptArrowStart.Y, 0), new Point3d(ptArrowTip.X, ptArrowTip.Y, 0));
                        spine.Layer = MarkerLayer;
                        spine.Color = pureWhite;
                        btr.AppendEntity(spine);
                        tr.AddNewlyCreatedDBObject(spine, true);

                        // 4. CÁNH RẺ QUẠT GÓC NHÌN FOV (Viền Đỏ Cờ đậm 0.9m)
                        Polyline cone = new Polyline();
                        cone.Layer = MarkerLayer;
                        cone.Color = vividRed;
                        cone.ConstantWidth = 0.9 * unitFactor;

                        Point2d ptCenter = new Point2d(cadX, cadY);
                        Point2d ptLeft = new Point2d(cadX + coneLen * Math.Cos(cadAngleRad + halfFovRad),
                                                     cadY + coneLen * Math.Sin(cadAngleRad + halfFovRad));
                        Point2d ptRight = new Point2d(cadX + coneLen * Math.Cos(cadAngleRad - halfFovRad),
                                                      cadY + coneLen * Math.Sin(cadAngleRad - halfFovRad));

                        cone.AddVertexAt(0, ptCenter, 0, 0, 0);
                        cone.AddVertexAt(1, ptLeft, 0, 0, 0);
                        cone.AddVertexAt(2, ptRight, 0, 0, 0);
                        cone.Closed = true;

                        btr.AppendEntity(cone);
                        tr.AddNewlyCreatedDBObject(cone, true);

                        // 5. VÒNG CUNG SÓNG RADAR TRUNG GIAN (Trắng sáng cự ly 13m)
                        Polyline arcWave = new Polyline();
                        arcWave.Layer = MarkerLayer;
                        arcWave.Color = pureWhite;
                        arcWave.ConstantWidth = 0.65 * unitFactor;
                        double arcRadius = 13.0 * unitFactor;
                        Point2d ptArcLeft = new Point2d(cadX + arcRadius * Math.Cos(cadAngleRad + halfFovRad),
                                                        cadY + arcRadius * Math.Sin(cadAngleRad + halfFovRad));
                        Point2d ptArcRight = new Point2d(cadX + arcRadius * Math.Cos(cadAngleRad - halfFovRad),
                                                         cadY + arcRadius * Math.Sin(cadAngleRad - halfFovRad));
                        double arcBulge = -Math.Tan(halfFovRad / 2.0);
                        arcWave.AddVertexAt(0, ptArcLeft, arcBulge, 0.65 * unitFactor, 0.65 * unitFactor);
                        arcWave.AddVertexAt(1, ptArcRight, 0, 0, 0);

                        btr.AppendEntity(arcWave);
                        tr.AddNewlyCreatedDBObject(arcWave, true);

                        // 6. NOTE TRÒN TO TÂM VỊ TRÍ (Đỏ cờ viền dày 1.0m, tâm trắng + đen)
                        Polyline outerRing = new Polyline();
                        outerRing.Layer = MarkerLayer;
                        outerRing.Color = vividRed;
                        outerRing.ConstantWidth = 1.0 * unitFactor;
                        Point2d p1 = new Point2d(cadX - rOuter, cadY);
                        Point2d p2 = new Point2d(cadX + rOuter, cadY);
                        outerRing.AddVertexAt(0, p1, 1.0, 1.0 * unitFactor, 1.0 * unitFactor);
                        outerRing.AddVertexAt(1, p2, 1.0, 1.0 * unitFactor, 1.0 * unitFactor);
                        outerRing.Closed = true;

                        btr.AppendEntity(outerRing);
                        tr.AddNewlyCreatedDBObject(outerRing, true);

                        // Vòng đệm trắng bên trong
                        Circle innerCircle = new Circle(center, Vector3d.ZAxis, 1.8 * unitFactor);
                        innerCircle.Layer = MarkerLayer;
                        innerCircle.Color = pureWhite;
                        btr.AppendEntity(innerCircle);
                        tr.AddNewlyCreatedDBObject(innerCircle, true);

                        // Chấm tâm đen tuyền
                        Circle centerDot = new Circle(center, Vector3d.ZAxis, 0.9 * unitFactor);
                        centerDot.Layer = MarkerLayer;
                        centerDot.Color = solidBlack;
                        btr.AppendEntity(centerDot);
                        tr.AddNewlyCreatedDBObject(centerDot, true);

                        // 7. CHỮ HIỂN THỊ GÓC NHÌN (Trắng sáng lớn đậm)
                        string dirLabel = StreetViewWindow.GetDirectionText(heading);
                        DBText txt = new DBText
                        {
                            Layer = MarkerLayer,
                            TextString = $"👁 STREET VIEW: {heading:F0}° ({dirLabel})",
                            Height = 2.4 * unitFactor,
                            Position = new Point3d(cadX + (rOuter + 1.5 * unitFactor), cadY + (rOuter + 1.5 * unitFactor), 0),
                            Color = pureWhite
                        };
                        btr.AppendEntity(txt);
                        tr.AddNewlyCreatedDBObject(txt, true);

                        tr.Commit();
                    }
                    doc.Editor.UpdateScreen();
                }
            }
            catch { }
        }

        public static void RemoveMarker(Document doc)
        {
            if (doc == null || doc.IsDisposed) return;
            try
            {
                using (doc.LockDocument())
                {
                    Database db = doc.Database;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                        foreach (ObjectId id in btr)
                        {
                            if (id.IsValid && !id.IsErased)
                            {
                                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                                if (ent != null && ent.Layer == MarkerLayer)
                                {
                                    ent.UpgradeOpen();
                                    ent.Erase(true);
                                }
                            }
                        }
                        tr.Commit();
                    }
                    doc.Editor.UpdateScreen();
                }
            }
            catch { }
        }

        private static void ShowOrUpdateStreetViewWindow(double lat, double lon, ProvinceCrs crs, double cadX, double cadY, Document doc)
        {
            try
            {
                if (_activeStreetViewWindow != null && _activeStreetViewWindow.IsLoaded)
                {
                    _activeStreetViewWindow.NavigateTo(lat, lon, crs.Name, cadX, cadY);
                }
                else
                {
                    _activeStreetViewWindow = new StreetViewWindow(lat, lon, crs.Name, cadX, cadY);
                    CadApp.ShowModelessWindow(_activeStreetViewWindow);
                }

                _activeStreetViewWindow.PovChangedCallback = (newLat, newLon, newHeading) =>
                {
                    if (doc == null || doc.IsDisposed) return;
                    try
                    {
                        var newCad = VN2000Transform.WGS84ToVN2000(newLat, newLon, crs.CentralMeridian, crs.ScaleFactor);
                        double unitScale = (Math.Abs(cadX) > 10000000 || Math.Abs(cadY) > 10000000) ? 1000.0 : 1.0;
                        double cX = newCad.X * unitScale;
                        double cY = newCad.Y * unitScale;
                        ThrottledUpdateMarker(doc, cX, cY, newHeading, crs);
                    }
                    catch { }
                };

                _activeStreetViewWindow.ClosedCallback = () =>
                {
                    _markerThrottleTimer?.Stop();
                    _markerThrottleTimer = null;
                    RemoveMarker(doc);
                    _activeStreetViewWindow = null;
                };

                // Vẽ ngay marker vị trí ban đầu
                UpdateMarker(doc, cadX, cadY, 0.0, crs);
            }
            catch (Exception)
            {
                string sLat = lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                string sLon = lon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                string url = $"https://www.google.com/maps/@?api=1&map_action=pano&viewpoint={sLat},{sLon}";
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch { }
            }
        }

        public static void ExecuteStreetView(Document doc)
        {
            if (doc == null) return;
            Editor ed = doc.Editor;

            ProvinceCrs crs = CrsManager.EnsureCrsSelected(doc);
            if (crs == null)
            {
                ed.WriteMessage("\n[AVMap] Chưa chọn Hệ tọa độ VN-2000! Vui lòng chọn hệ tọa độ trước.");
                return;
            }

            // Ghi nhận trạng thái nền ban đầu của người dùng
            bool wasRunning = RealtimeMapEngine.IsRunning;
            string prevMapType = GoogleTileClient.ActiveMapType;
            string cleanBaseType = prevMapType?.Replace(",svv", "") ?? "y";

            // Xác định loại nền hiển thị tuyến đường Street View phù hợp nhất với loại nền người dùng đang chọn
            string svMapType;
            if (cleanBaseType == "s" || cleanBaseType == "esri")
            {
                svMapType = "s,svv"; // Ảnh vệ tinh thuần + đường xanh Street View (không nhãn giao thông)
            }
            else if (cleanBaseType == "m" || cleanBaseType == "osm")
            {
                svMapType = "m,svv"; // Bản đồ giao thông + đường xanh Street View
            }
            else
            {
                svMapType = "y,svv"; // Mặc định Hybrid + đường xanh
            }

            ed.WriteMessage("\n[AVMap] Đang nạp mạng lưới đường Google Street View...");

            if (!wasRunning)
            {
                RealtimeMapEngine.Start();
            }

            // Nạp và hiển thị các tuyến đường màu xanh TRƯỚC KHI yêu cầu click
            GoogleTileClient.ActiveMapType = svMapType;
            RealtimeMapEngine.UpdateMapNow(doc, svMapType);

            PointMonitorEventHandler onPointMonitor = (s, e) =>
            {
                if (e.Context != null)
                {
                    e.AppendToolTipText("★ Google Street View (Pegman)\nClick vào đường màu xanh để xem 360° (F3 để bật/tắt bắt điểm)");
                }
            };

            // Tạm thời tắt bắt điểm (OSMODE bit 16384) để tránh bắt nhầm đối tượng khác khi chọn đường
            object oldOsmodeObj = null;
            try
            {
                oldOsmodeObj = CadApp.GetSystemVariable("OSMODE");
                int osmodeVal = Convert.ToInt32(oldOsmodeObj);
                if ((osmodeVal & 16384) == 0)
                {
                    CadApp.SetSystemVariable("OSMODE", osmodeVal | 16384);
                }
            }
            catch { }

            try
            {
                ed.PointMonitor += onPointMonitor;

                while (true)
                {
                    // Nhắc người dùng click chọn điểm trên đường màu xanh (chế độ lặp)
                    PromptPointOptions ppo = new PromptPointOptions("\n[AVMap] Chọn một điểm trên đường màu xanh để xem Street View 360° (ENTER/ESC để dừng): ");
                    ppo.AllowNone = true;
                    PromptPointResult ppr = ed.GetPoint(ppo);

                    if (ppr.Status != PromptStatus.OK) break;

                    Point3d pt = ppr.Value;
                    // Nhận diện đơn vị m hay mm khi click điểm
                    double clickUnitScale = (Math.Abs(pt.X) > 10000000 || Math.Abs(pt.Y) > 10000000) ? 0.001 : 1.0;
                    double ptX_m = pt.X * clickUnitScale;
                    double ptY_m = pt.Y * clickUnitScale;

                    var wgs = VN2000Transform.VN2000ToWGS84(ptX_m, ptY_m, crs.CentralMeridian, crs.ScaleFactor);

                    ed.WriteMessage($"\n[AVMap] Tọa độ CAD: X={pt.X:F2}, Y={pt.Y:F2} -> WGS84: {wgs.Latitude:F6}, {wgs.Longitude:F6}");

                    // Cập nhật vị trí trên cửa sổ hiện hữu hoặc mở mới nếu chưa có
                    ShowOrUpdateStreetViewWindow(wgs.Latitude, wgs.Longitude, crs, pt.X, pt.Y, doc);
                }
            }
            finally
            {
                ed.PointMonitor -= onPointMonitor;

                // Khôi phục lại chế độ bắt điểm OSMODE ban đầu của người dùng
                if (oldOsmodeObj != null)
                {
                    try { CadApp.SetSystemVariable("OSMODE", oldOsmodeObj); } catch { }
                }

                // HOÀN TRẢ BẢN VẼ SẠCH SẼ HOÀN TOÀN:
                cleanBaseType = prevMapType?.Replace(",svv", "") ?? "y";
                GoogleTileClient.ActiveMapType = cleanBaseType;

                if (!wasRunning)
                {
                    // Nếu ban đầu người dùng KHÔNG bật map (chạy độc lập), tắt và xóa sạch nền!
                    RealtimeMapEngine.Stop();
                }
                else
                {
                    // Nếu ban đầu người dùng ĐANG bật map, khôi phục đúng loại map ban đầu của họ (không bị ép sang Hybrid!)
                    RealtimeMapEngine.UpdateMapNow(doc, cleanBaseType);
                }

                // Dọn dẹp layer phụ nếu còn sót
                RemoveCoverageOverlay(doc);
            }
        }

        public static void ToggleCoverageOverlay(Document doc)
        {
            if (doc == null) return;
            Editor ed = doc.Editor;
            ProvinceCrs crs = CrsManager.EnsureCrsSelected(doc);
            if (crs == null) return;

            if (HasCoverageOverlay(doc))
            {
                RemoveCoverageOverlay(doc);
                ed.WriteMessage("\n[AVMap] Đã ẩn mạng lưới đường Google Street View.\n");
            }
            else
            {
                ed.WriteMessage("\n[AVMap] Đang tải mạng lưới đường Google Street View...");
                ShowCoverageOverlay(doc, crs);
                ed.WriteMessage("\n[AVMap] Đã hiển thị mạng lưới đường Google Street View trên nền bản đồ.\n");
            }
        }

        public static bool HasCoverageOverlay(Document doc)
        {
            if (doc == null || doc.IsDisposed) return false;
            try
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (ObjectId id in btr)
                    {
                        if (id.IsValid && !id.IsErased)
                        {
                            if (tr.GetObject(id, OpenMode.ForRead) is RasterImage rImg && rImg.Layer == CoverageLayer)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static void ShowCoverageOverlay(Document doc, ProvinceCrs crs)
        {
            if (isUpdatingCoverage) return;
            isUpdatingCoverage = true;

            try
            {
                Editor ed = doc.Editor;
                double cx = 0, cy = 0, viewHeight = 0, screenW = 1920, screenH = 1080;

                try
                {
                    object scr = CadApp.GetSystemVariable("SCREENSIZE");
                    if (scr is Point2d sp) { screenW = Math.Max(100.0, sp.X); screenH = Math.Max(100.0, sp.Y); }
                }
                catch { }

                double viewWidth = 0;
                try
                {
                    using (ViewTableRecord view = ed.GetCurrentView())
                    {
                        cx = view.CenterPoint.X;
                        cy = view.CenterPoint.Y;
                        viewHeight = view.Height;
                        viewWidth = view.Width > 0 ? view.Width : viewHeight * (screenW / screenH);
                    }
                }
                catch
                {
                    try
                    {
                        object vctr = CadApp.GetSystemVariable("VIEWCTR");
                        if (vctr is Point3d p3) { cx = p3.X; cy = p3.Y; }
                        else if (vctr is Point2d p2) { cx = p2.X; cy = p2.Y; }
                        viewHeight = (double)CadApp.GetSystemVariable("VIEWSIZE");
                        viewWidth = viewHeight * (screenW / screenH);
                    }
                    catch
                    {
                        return;
                    }
                }

                if (viewHeight <= 0 || viewWidth <= 0) return;

                // Tự động nhận diện đơn vị bản vẽ: Mét (m) hay Milimét (mm)
                double unitScale = (Math.Abs(cx) > 10000000 || Math.Abs(cy) > 10000000) ? 0.001 : 1.0;

                double minX = (cx - viewWidth / 2.0) * unitScale;
                double maxX = (cx + viewWidth / 2.0) * unitScale;
                double minY = (cy - viewHeight / 2.0) * unitScale;
                double maxY = (cy + viewHeight / 2.0) * unitScale;
                double viewHeightMeters = viewHeight * unitScale;

                var ptSW = VN2000Transform.VN2000ToWGS84(minX, minY, crs.CentralMeridian, crs.ScaleFactor);
                var ptNE = VN2000Transform.VN2000ToWGS84(maxX, maxY, crs.CentralMeridian, crs.ScaleFactor);

                // Cho phép zoom từ 10 đến 18 để khi zoom xa nhìn toàn huyện (như Hoài Nhơn) vẫn nạp mượt mà chỉ 4-9 gạch ảnh
                int zoom = Math.Max(10, Math.Min(18, (int)Math.Floor(Math.Log(156543.03 * Math.Cos((ptSW.Latitude + ptNE.Latitude) / 2.0 * Math.PI / 180.0) / (viewHeightMeters / Math.Max(1.0, screenH))) / Math.Log(2.0))));

                var (tileMinX, tileMaxY) = VN2000Transform.LatLonToTile(ptSW.Latitude, ptSW.Longitude, zoom);
                var (tileMaxX, tileMinY) = VN2000Transform.LatLonToTile(ptNE.Latitude, ptNE.Longitude, zoom);

                int minTX = Math.Min(tileMinX, tileMaxX);
                int maxTX = Math.Max(tileMinX, tileMaxX);
                int minTY = Math.Min(tileMinY, tileMaxY);
                int maxTY = Math.Max(tileMinY, tileMaxY);

                while ((maxTX - minTX + 1) * (maxTY - minTY + 1) > 25 && zoom > 10)
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

                string imageFile = Task.Run(() => GoogleTileClient.StitchTilesToFileAsync(minTX, maxTX, minTY, maxTY, zoom, "svv")).GetAwaiter().GetResult();
                if (string.IsNullOrEmpty(imageFile) || !File.Exists(imageFile)) return;

                double originX = Math.Min(nwCad.X, seCad.X);
                double originY = Math.Min(nwCad.Y, seCad.Y);
                double widthMeters = Math.Abs(seCad.X - nwCad.X);
                double heightMeters = Math.Abs(nwCad.Y - seCad.Y);

                Database db = doc.Database;
                using (doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    LayerTableRecord ltr;
                    if (!lt.Has(CoverageLayer))
                    {
                        lt.UpgradeOpen();
                        ltr = new LayerTableRecord
                        {
                            Name = CoverageLayer,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, 8), // Màu ghi 8 tối chìm
                            IsOff = false,
                            IsFrozen = false,
                            IsLocked = false
                        };
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }
                    else
                    {
                        ltr = (LayerTableRecord)tr.GetObject(lt[CoverageLayer], OpenMode.ForWrite);
                        ltr.IsOff = false;
                        ltr.IsFrozen = false;
                        ltr.IsLocked = false;
                    }

                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    ObjectId imgDictId = RasterImageDef.GetImageDictionary(db);
                    if (imgDictId.IsNull) imgDictId = RasterImageDef.CreateImageDictionary(db);

                    DBDictionary imgDict = (DBDictionary)tr.GetObject(imgDictId, OpenMode.ForWrite);
                    const string svvDefName = "AVMap_SVV_DEF";

                    ObjectId defId = ObjectId.Null;
                    RasterImageDef imgDef = null;

                    if (imgDict.Contains(svvDefName))
                    {
                        defId = imgDict.GetAt(svvDefName);
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
                        defId = imgDict.SetAt(svvDefName, imgDef);
                        tr.AddNewlyCreatedDBObject(imgDef, true);
                    }

                    // Tái sử dụng RasterImage hiện hữu nếu đã có
                    RasterImage img = null;
                    ObjectId imgId = ObjectId.Null;
                    var extraCoverage = new System.Collections.Generic.List<RasterImage>();

                    foreach (ObjectId id in btr)
                    {
                        if (id.IsValid && !id.IsErased)
                        {
                            if (tr.GetObject(id, OpenMode.ForRead) is RasterImage rImg && rImg.Layer == CoverageLayer)
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
                                    extraCoverage.Add(rImg);
                                }
                            }
                        }
                    }

                    if (extraCoverage.Count > 0)
                    {
                        RasterImage.EnableReactors(false);
                        foreach (var extra in extraCoverage)
                        {
                            try { extra.Erase(true); } catch { }
                        }
                        RasterImage.EnableReactors(true);
                    }

                    double cadWidth = widthMeters / unitScale;
                    double cadHeight = heightMeters / unitScale;
                    Vector3d uVector = new Vector3d(cadWidth, 0, 0);
                    Vector3d vVector = new Vector3d(0, cadHeight, 0);
                    Point3d originPoint = new Point3d(originX / unitScale, originY / unitScale, 0);

                    if (img == null)
                    {
                        img = new RasterImage();
                        img.ImageDefId = defId;
                        img.Layer = CoverageLayer;

                        imgId = btr.AppendEntity(img);
                        tr.AddNewlyCreatedDBObject(img, true);

                        RasterImage.EnableReactors(true);
                        try { img.AssociateRasterDef(imgDef); } catch { }

                        img.Orientation = new CoordinateSystem3d(originPoint, uVector, vVector);
                        img.ImageTransparency = true;
                        img.ShowImage = true;
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
                        img.ImageTransparency = true;
                        img.ShowImage = true;
                        img.SetClipBoundaryToWholeImage();
                    }

                    if (!btr.DrawOrderTableId.IsNull && btr.DrawOrderTableId.IsValid)
                    {
                        try
                        {
                            DrawOrderTable dot = (DrawOrderTable)tr.GetObject(btr.DrawOrderTableId, OpenMode.ForWrite);
                            ObjectIdCollection ids = new ObjectIdCollection { imgId };
                            dot.MoveToTop(ids);
                        }
                        catch { }
                    }

                    // Khóa layer để AutoCAD tuyệt đối không bao giờ hiển thị viền khung chữ nhật khi rê chuột
                    try { ltr.IsLocked = true; } catch { }

                    tr.Commit();
                }

                try { ed.Regen(); } catch { }
                ed.UpdateScreen();
            }
            catch { }
            finally
            {
                isUpdatingCoverage = false;
            }
        }

        public static void RemoveCoverageOverlay(Document doc)
        {
            if (GoogleTileClient.ActiveMapType.Contains("svv"))
            {
                GoogleTileClient.ActiveMapType = GoogleTileClient.ActiveMapType.Replace(",svv", "");
                RealtimeMapEngine.TriggerUpdate();
            }

            try
            {
                Database db = doc.Database;
                using (doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (lt.Has(CoverageLayer))
                    {
                        try
                        {
                            LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[CoverageLayer], OpenMode.ForWrite);
                            ltr.IsLocked = false;
                        }
                        catch { }
                    }

                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    ObjectId imgDictId = RasterImageDef.GetImageDictionary(db);
                    DBDictionary imgDict = imgDictId.IsNull ? null : (DBDictionary)tr.GetObject(imgDictId, OpenMode.ForWrite);

                    RasterImage.EnableReactors(false);
                    try
                    {
                        foreach (ObjectId id in btr)
                        {
                            if (id.IsValid && !id.IsErased)
                            {
                                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                                if (ent is RasterImage && ent.Layer == CoverageLayer)
                                {
                                    ent.UpgradeOpen();
                                    ent.Erase(true);
                                }
                            }
                        }

                        if (imgDict != null && imgDict.Contains("AVMap_SVV_DEF"))
                        {
                            ObjectId oldDefId = imgDict.GetAt("AVMap_SVV_DEF");
                            if (oldDefId.IsValid && !oldDefId.IsErased)
                            {
                                try
                                {
                                    DBObject oldDef = tr.GetObject(oldDefId, OpenMode.ForWrite);
                                    oldDef.Erase(true);
                                }
                                catch { }
                            }
                        }
                    }
                    finally
                    {
                        RasterImage.EnableReactors(true);
                    }

                    tr.Commit();
                }
                try { doc.Editor.Regen(); } catch { }
                doc.Editor.UpdateScreen();
            }
            catch { }
        }
    }
}