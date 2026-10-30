using System;
using System.IO;
using System.Threading.Tasks;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Colors;
using CadApp = Gssoft.Gscad.ApplicationServices.Application;

namespace AVMap.Enji
{
    public static class StreetViewService
    {
        private const string CoverageLayer = "_AVMAP_STREETVIEW_COVERAGE_";
        public const string MarkerLayer = "_AVMAP_STREETVIEW_MARKER_";
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
                    Description = "AVMap enjiCAD - Vi tri & Huong nhin Google Street View (Do / Trang / Den)"
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
                    _activeStreetViewWindow.Show();
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
            catch
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
                ed.WriteMessage("\n[AVMap enjiCAD] Chưa chọn Hệ tọa độ VN-2000! Vui lòng chọn hệ tọa độ trước.");
                return;
            }

            // Ghi nhận trạng thái nền ban đầu của người dùng
            bool wasRunning = RealtimeMapEngine.IsRunning;
            string prevMapType = GoogleTileClient.ActiveMapType;

            // Xác định loại nền hiển thị tuyến đường Street View phù hợp nhất với loại nền người dùng đang chọn
            string svMapType;
            if (prevMapType == "s" || prevMapType == "esri")
            {
                svMapType = "s,svv"; // Ảnh vệ tinh thuần + đường xanh Street View
            }
            else if (prevMapType == "m" || prevMapType == "osm")
            {
                svMapType = "m,svv"; // Bản đồ giao thông + đường xanh Street View
            }
            else
            {
                svMapType = "y,svv"; // Mặc định Hybrid + đường xanh
            }

            ed.WriteMessage("\n[AVMap enjiCAD] Đang nạp mạng lưới đường Google Street View...");

            if (!wasRunning)
            {
                RealtimeMapEngine.Start();
            }

            // Nạp và hiển thị các tuyến đường màu xanh TRƯỚC KHI yêu cầu click
            GoogleTileClient.ActiveMapType = svMapType;
            RealtimeMapEngine.UpdateMapNow(doc, svMapType);

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
                while (true)
                {
                    // Nhắc người dùng click chọn điểm trên đường màu xanh
                    PromptPointOptions ppo = new PromptPointOptions("\n[AVMap enjiCAD] Chọn điểm trên đường màu xanh để xem Street View 360° (ENTER/ESC để dừng): ");
                    ppo.AllowNone = true;
                    PromptPointResult ppr = ed.GetPoint(ppo);

                    if (ppr.Status != PromptStatus.OK) break;

                    Point3d pt = ppr.Value;
                    // Nhận diện đơn vị m hay mm khi click điểm
                    double clickUnitScale = (Math.Abs(pt.X) > 10000000 || Math.Abs(pt.Y) > 10000000) ? 0.001 : 1.0;
                    double ptX_m = pt.X * clickUnitScale;
                    double ptY_m = pt.Y * clickUnitScale;

                    var wgs = VN2000Transform.VN2000ToWGS84(ptX_m, ptY_m, crs.CentralMeridian, crs.ScaleFactor);

                    ed.WriteMessage($"\n[AVMap enjiCAD] Tọa độ CAD: X={pt.X:F2}, Y={pt.Y:F2} -> WGS84: {wgs.Latitude:F6}, {wgs.Longitude:F6}");

                    // Cập nhật vị trí trên cửa sổ hiện hữu hoặc mở mới nếu chưa có
                    ShowOrUpdateStreetViewWindow(wgs.Latitude, wgs.Longitude, crs, pt.X, pt.Y, doc);
                }
            }
            finally
            {
                // Khôi phục lại chế độ bắt điểm OSMODE ban đầu của người dùng
                if (oldOsmodeObj != null)
                {
                    try { CadApp.SetSystemVariable("OSMODE", oldOsmodeObj); } catch { }
                }

                // HOÀN TRẢ BẢN VẼ SẠCH SẼ HOÀN TOÀN:
                if (!wasRunning)
                {
                    // Nếu ban đầu người dùng KHÔNG bật map (chạy độc lập), tắt và xóa sạch nền!
                    RealtimeMapEngine.Stop();
                }
                else
                {
                    // Nếu ban đầu người dùng ĐANG bật map, khôi phục đúng loại map ban đầu của họ (không bị ép sang Hybrid!)
                    GoogleTileClient.ActiveMapType = prevMapType;
                    RealtimeMapEngine.UpdateMapNow(doc, prevMapType);
                }

                // Dọn dẹp layer phụ nếu còn sót
                RemoveCoverageOverlay(doc);
            }
        }

        private static void RemoveCoverageOverlay(Document doc)
        {
            try
            {
                Database db = doc.Database;
                using (doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (ObjectId id in btr)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent is RasterImage && ent.Layer == CoverageLayer)
                        {
                            ent.UpgradeOpen();
                            ent.Erase(true);
                        }
                    }
                    tr.Commit();
                }
                doc.Editor.UpdateScreen();
            }
            catch { }
        }
    }
}
