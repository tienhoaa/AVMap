using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Colors;
using Color = Autodesk.AutoCAD.Colors.Color;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AVMap
{
    public static class ContourService
    {
        public const string LayerContourIndex = "DONG_MUC_CHINH";
        public const string LayerContourInter = "DONG_MUC_PHU";
        public const string LayerContourText = "DONG_MUC_TEXT";

        private static readonly HttpClient client = new HttpClient();
        private static readonly string demCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVMap", "DemCache");

        static ContourService()
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "AVMap_AutoCAD_Civil/1.0 (contact: support@opencartis.com)");
            if (!Directory.Exists(demCache)) Directory.CreateDirectory(demCache);
        }

        public static void GenerateContoursCommand(Document doc)
        {
            if (doc == null) return;
            ContourWindow win = new ContourWindow(doc);
            CadApp.ShowModalWindow(win);
        }

        public static void ExecuteContourWorkflow(
            Document doc, double minX, double maxX, double minY, double maxY, ProvinceCrs crs,
            double interval, bool addLabels, bool indexOnly, double labelSpacing, double textHeight, bool smoothing)
        {
            if (doc == null || crs == null) return;
            Editor ed = doc.Editor;

            double widthM = maxX - minX;
            double heightM = maxY - minY;

            ed.WriteMessage($"\n=======================================================");
            ed.WriteMessage($"\n[AVMap] Đang tải mô hình độ cao số DEM toàn cầu (AWS Terrarium / SRTM / Copernicus)...");
            ed.WriteMessage($"\n        Phạm vi: Rộng {widthM:N0}m x Dài {heightM:N0}m | Khoảng cao đều: {interval}m/đường...");

            var ptSW = VN2000Transform.VN2000ToWGS84(minX, minY, crs.CentralMeridian, crs.ScaleFactor);
            var ptNE = VN2000Transform.VN2000ToWGS84(maxX, maxY, crs.CentralMeridian, crs.ScaleFactor);

            double s = Math.Min(ptSW.Latitude, ptNE.Latitude);
            double w = Math.Min(ptSW.Longitude, ptNE.Longitude);
            double n = Math.Max(ptSW.Latitude, ptNE.Latitude);
            double e = Math.Max(ptSW.Longitude, ptNE.Longitude);

            double maxSpanM = Math.Max(widthM, heightM);
            int zoom = 14;
            if (maxSpanM > 10000) zoom = 12;
            else if (maxSpanM > 4000) zoom = 13;

            try
            {
                var grid = Task.Run(() => BuildElevationGridAsync(s, w, n, e, zoom)).GetAwaiter().GetResult();
                if (grid == null)
                {
                    ed.WriteMessage("\n[AVMap Lỗi] Không thể tải dữ liệu độ cao vệ tinh. Vui lòng kiểm tra kết nối mạng.");
                    return;
                }

                DrawContoursFromGrid(doc, grid, minX, maxX, minY, maxY, interval, crs, addLabels, indexOnly, labelSpacing, textHeight, smoothing);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[AVMap Lỗi Đồng Mức]: {ex.Message}");
            }
        }

        private class ElevationGrid
        {
            public double[,] Heights;
            public int Width;
            public int Height;
            public double MinHeight;
            public double MaxHeight;
            public double LatNorth;
            public double LatSouth;
            public double LonWest;
            public double LonEast;
        }

        private static async Task<ElevationGrid> BuildElevationGridAsync(double south, double west, double north, double east, int zoom)
        {
            var (tileMinX, tileMaxY) = VN2000Transform.LatLonToTile(south, west, zoom);
            var (tileMaxX, tileMinY) = VN2000Transform.LatLonToTile(north, east, zoom);

            int minTX = Math.Min(tileMinX, tileMaxX);
            int maxTX = Math.Max(tileMinX, tileMaxX);
            int minTY = Math.Min(tileMinY, tileMaxY);
            int maxTY = Math.Max(tileMinY, tileMaxY);

            // Giới hạn số mảnh tải an toàn
            while ((maxTX - minTX + 1) * (maxTY - minTY + 1) > 25 && zoom > 6)
            {
                zoom--;
                var t1 = VN2000Transform.LatLonToTile(south, west, zoom);
                var t2 = VN2000Transform.LatLonToTile(north, east, zoom);
                minTX = Math.Min(t1.tileX, t2.tileX);
                maxTX = Math.Max(t1.tileX, t2.tileX);
                minTY = Math.Min(t1.tileY, t2.tileY);
                maxTY = Math.Max(t1.tileY, t2.tileY);
            }

            int cols = maxTX - minTX + 1;
            int rows = maxTY - minTY + 1;
            int tileSize = 256;
            int fullW = cols * tileSize;
            int fullH = rows * tileSize;

            double[,] heights = new double[fullW, fullH];
            double minElev = 99999, maxElev = -99999;

            for (int ty = minTY; ty <= maxTY; ty++)
            {
                for (int tx = minTX; tx <= maxTX; tx++)
                {
                    byte[] bytes = await DownloadTerrariumTileAsync(zoom, tx, ty);
                    if (bytes != null && bytes.Length > 0)
                    {
                        using (var ms = new MemoryStream(bytes))
                        using (var bmp = new Bitmap(ms))
                        {
                            int offX = (tx - minTX) * tileSize;
                            int offY = (ty - minTY) * tileSize;

                            for (int py = 0; py < bmp.Height; py++)
                            {
                                for (int px = 0; px < bmp.Width; px++)
                                {
                                    System.Drawing.Color c = bmp.GetPixel(px, py);
                                    double h = (c.R * 256.0 + c.G + c.B / 256.0) - 32768.0;

                                    int gx = offX + px;
                                    int gy = offY + py;
                                    if (gx < fullW && gy < fullH)
                                    {
                                        heights[gx, gy] = h;
                                        if (h < minElev) minElev = h;
                                        if (h > maxElev) maxElev = h;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            var nwPt = VN2000Transform.TileToLatLon(minTX, minTY, zoom);
            var sePt = VN2000Transform.TileToLatLon(maxTX + 1, maxTY + 1, zoom);

            return new ElevationGrid
            {
                Heights = heights,
                Width = fullW,
                Height = fullH,
                MinHeight = minElev,
                MaxHeight = maxElev,
                LatNorth = nwPt.Latitude,
                LatSouth = sePt.Latitude,
                LonWest = nwPt.Longitude,
                LonEast = sePt.Longitude
            };
        }

        private static async Task<byte[]> DownloadTerrariumTileAsync(int z, int x, int y)
        {
            string cacheFile = Path.Combine(demCache, $"{z}_{x}_{y}.png");
            if (File.Exists(cacheFile) && new FileInfo(cacheFile).Length > 0)
            {
                try { return File.ReadAllBytes(cacheFile); } catch { }
            }

            string[] mirrors = new[]
            {
                $"https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{z}/{x}/{y}.png",
                $"https://tile.nextzen.org/tilezen/elevation/v1/terrarium/{z}/{x}/{y}.png"
            };

            foreach (var url in mirrors)
            {
                try
                {
                    using (var response = await client.GetAsync(url))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            byte[] data = await response.Content.ReadAsByteArrayAsync();
                            if (data != null && data.Length > 0)
                            {
                                try { File.WriteAllBytes(cacheFile, data); } catch { }
                                return data;
                            }
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        private static void DrawContoursFromGrid(
            Document doc, ElevationGrid grid,
            double minCadX, double maxCadX, double minCadY, double maxCadY,
            double interval, ProvinceCrs crs,
            bool addLabels, bool indexOnly, double labelSpacing, double userTextHeight, bool smoothing)
        {
            Editor ed = doc.Editor;
            Database db = doc.Database;

            double startLevel = Math.Ceiling(grid.MinHeight / interval) * interval;
            double endLevel = Math.Floor(grid.MaxHeight / interval) * interval;

            if (startLevel > endLevel)
            {
                ed.WriteMessage("\n[AVMap] Không tìm thấy khoảng biến thiên cao độ hợp lệ trong khu vực này.");
                return;
            }

            double indexInterval = interval * 5.0; // Đường đồng mức chính mỗi 5 khoảng
            int totalContours = 0;
            int totalLabels = 0;

            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                EnsureLayers(db, tr);

                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                int stepGrid = 2; // Tối ưu bước nhảy lưới
                int w = grid.Width;
                int h = grid.Height;

                for (double level = startLevel; level <= endLevel; level += interval)
                {
                    bool isIndex = (Math.Abs(level % indexInterval) < 0.001 || Math.Abs((level % indexInterval) - indexInterval) < 0.001);
                    string targetLayer = isIndex ? LayerContourIndex : LayerContourInter;
                    double widthLine = isIndex ? 0.35 : 0.10;

                    var segments = new List<Tuple<Point2d, Point2d>>();

                    for (int gy = 0; gy < h - stepGrid; gy += stepGrid)
                    {
                        for (int gx = 0; gx < w - stepGrid; gx += stepGrid)
                        {
                            double v0 = grid.Heights[gx, gy];
                            double v1 = grid.Heights[gx + stepGrid, gy];
                            double v2 = grid.Heights[gx + stepGrid, gy + stepGrid];
                            double v3 = grid.Heights[gx, gy + stepGrid];

                            int idx = (v0 >= level ? 1 : 0) | (v1 >= level ? 2 : 0) | (v2 >= level ? 4 : 0) | (v3 >= level ? 8 : 0);
                            if (idx == 0 || idx == 15) continue;

                            Point2d p0 = GridToCad(gx, gy, grid, crs);
                            Point2d p1 = GridToCad(gx + stepGrid, gy, grid, crs);
                            Point2d p2 = GridToCad(gx + stepGrid, gy + stepGrid, grid, crs);
                            Point2d p3 = GridToCad(gx, gy + stepGrid, grid, crs);

                            if (Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X)) < minCadX ||
                                Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X)) > maxCadX ||
                                Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y)) < minCadY ||
                                Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y)) > maxCadY)
                            {
                                continue;
                            }

                            Point2d e0 = Interp(p0, p1, v0, v1, level);
                            Point2d e1 = Interp(p1, p2, v1, v2, level);
                            Point2d e2 = Interp(p3, p2, v3, v2, level);
                            Point2d e3 = Interp(p0, p3, v0, v3, level);

                            switch (idx)
                            {
                                case 1: segments.Add(Tuple.Create(e3, e0)); break;
                                case 2: segments.Add(Tuple.Create(e0, e1)); break;
                                case 3: segments.Add(Tuple.Create(e3, e1)); break;
                                case 4: segments.Add(Tuple.Create(e1, e2)); break;
                                case 5: segments.Add(Tuple.Create(e3, e2)); segments.Add(Tuple.Create(e0, e1)); break;
                                case 6: segments.Add(Tuple.Create(e0, e2)); break;
                                case 7: segments.Add(Tuple.Create(e3, e2)); break;
                                case 8: segments.Add(Tuple.Create(e2, e3)); break;
                                case 9: segments.Add(Tuple.Create(e0, e2)); break;
                                case 10: segments.Add(Tuple.Create(e0, e3)); segments.Add(Tuple.Create(e1, e2)); break;
                                case 11: segments.Add(Tuple.Create(e1, e2)); break;
                                case 12: segments.Add(Tuple.Create(e1, e3)); break;
                                case 13: segments.Add(Tuple.Create(e0, e1)); break;
                                case 14: segments.Add(Tuple.Create(e3, e0)); break;
                            }
                        }
                    }

                    if (segments.Count == 0) continue;

                    var polylines = ChainSegments(segments);

                    foreach (var rawPts in polylines)
                    {
                        if (rawPts.Count < 2) continue;

                        List<Point2d> polyPts = smoothing ? SmoothPoints(rawPts) : rawPts;

                        Polyline pl = new Polyline(polyPts.Count);
                        for (int i = 0; i < polyPts.Count; i++)
                        {
                            pl.AddVertexAt(i, polyPts[i], 0, 0, 0);
                        }
                        pl.Elevation = level; // Gán đúng cao độ 3D
                        pl.ConstantWidth = widthLine;
                        pl.Layer = targetLayer;

                        btr.AppendEntity(pl);
                        tr.AddNewlyCreatedDBObject(pl, true);
                        totalContours++;

                        double totalLen = 0;
                        for (int k = 1; k < polyPts.Count; k++)
                        {
                            totalLen += polyPts[k].GetDistanceTo(polyPts[k - 1]);
                        }

                        bool shouldLabel = addLabels && (indexOnly ? isIndex : true);
                        if (shouldLabel && totalLen >= Math.Max(30.0, labelSpacing * 0.4))
                        {
                            double txtHeight = userTextHeight;
                            double labelStep = labelSpacing;
                            List<double> labelDists = new List<double>();

                            if (totalLen < labelStep * 1.5)
                            {
                                labelDists.Add(totalLen * 0.5);
                            }
                            else
                            {
                                for (double d = labelStep * 0.6; d < totalLen - labelStep * 0.3; d += labelStep)
                                {
                                    labelDists.Add(d);
                                }
                            }

                            foreach (double targetD in labelDists)
                            {
                                double curD = 0;
                                for (int k = 1; k < polyPts.Count; k++)
                                {
                                    double dSeg = polyPts[k].GetDistanceTo(polyPts[k - 1]);
                                    if (curD + dSeg >= targetD || k == polyPts.Count - 1)
                                    {
                                        double remain = targetD - curD;
                                        double ratio = dSeg > 1e-4 ? (remain / dSeg) : 0.5;
                                        ratio = Math.Max(0.0, Math.Min(1.0, ratio));

                                        Point2d p1 = polyPts[k - 1];
                                        Point2d p2 = polyPts[k];
                                        Point2d ptLabel = new Point2d(p1.X + ratio * (p2.X - p1.X), p1.Y + ratio * (p2.Y - p1.Y));

                                        double dx = p2.X - p1.X;
                                        double dy = p2.Y - p1.Y;
                                        double angle = Math.Atan2(dy, dx);
                                        if (angle > Math.PI / 2.0) angle -= Math.PI;
                                        else if (angle < -Math.PI / 2.0) angle += Math.PI;

                                        try
                                        {
                                            Point3d pt3d = new Point3d(ptLabel.X, ptLabel.Y, level);
                                            DBText txt = new DBText();
                                            txt.SetDatabaseDefaults();
                                            txt.TextString = $"{level:0.##}";
                                            txt.Height = txtHeight;
                                            txt.Layer = LayerContourText;
                                            txt.Rotation = angle;
                                            txt.HorizontalMode = TextHorizontalMode.TextCenter;
                                            txt.VerticalMode = TextVerticalMode.TextVerticalMid;
                                            txt.Position = pt3d;
                                            txt.AlignmentPoint = pt3d;
                                            txt.AdjustAlignment(db);

                                            btr.AppendEntity(txt);
                                            tr.AddNewlyCreatedDBObject(txt, true);
                                            totalLabels++;
                                        }
                                        catch { }
                                        break;
                                    }
                                    curD += dSeg;
                                }
                            }
                        }
                    }
                }

                tr.Commit();
            }

            ed.UpdateScreen();
            ed.WriteMessage($"\n=======================================================");
            ed.WriteMessage($"\n  TỰ ĐỘNG SINH ĐƯỜNG ĐỒNG MỨC ĐỊA HÌNH THÀNH CÔNG!");
            ed.WriteMessage($"\n  ✔ Khoảng cao độ: {grid.MinHeight:F1}m đến {grid.MaxHeight:F1}m (Chênh cao: {(grid.MaxHeight - grid.MinHeight):F1}m)");
            ed.WriteMessage($"\n  ✔ Khoảng cao đều: {interval}m/đường | Đồng mức chính mỗi: {indexInterval}m");
            ed.WriteMessage($"\n  ✔ Tổng số đường đồng mức: {totalContours} đường (kèm {totalLabels} nhãn cao độ)");
            ed.WriteMessage($"\n=======================================================\n");
        }

        private static List<Point2d> SmoothPoints(List<Point2d> pts)
        {
            if (pts == null || pts.Count < 3) return pts;
            var smoothed = new List<Point2d>();
            smoothed.Add(pts[0]);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Point2d p0 = pts[i];
                Point2d p1 = pts[i + 1];
                Point2d q = new Point2d(0.75 * p0.X + 0.25 * p1.X, 0.75 * p0.Y + 0.25 * p1.Y);
                Point2d r = new Point2d(0.25 * p0.X + 0.75 * p1.X, 0.25 * p0.Y + 0.75 * p1.Y);
                smoothed.Add(q);
                smoothed.Add(r);
            }
            smoothed.Add(pts[pts.Count - 1]);
            return smoothed;
        }

        private static Point2d Interp(Point2d p1, Point2d p2, double v1, double v2, double target)
        {
            if (Math.Abs(v2 - v1) < 1e-6) return p1;
            double t = (target - v1) / (v2 - v1);
            t = Math.Max(0.0, Math.Min(1.0, t));
            return new Point2d(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
        }

        private static Point2d GridToCad(int gx, int gy, ElevationGrid grid, ProvinceCrs crs)
        {
            double lon = grid.LonWest + ((double)gx / grid.Width) * (grid.LonEast - grid.LonWest);
            double lat = grid.LatNorth - ((double)gy / grid.Height) * (grid.LatNorth - grid.LatSouth);
            var pt = VN2000Transform.WGS84ToVN2000(lat, lon, crs.CentralMeridian, crs.ScaleFactor);
            return new Point2d(pt.X, pt.Y);
        }

        private static List<List<Point2d>> ChainSegments(List<Tuple<Point2d, Point2d>> segments)
        {
            var result = new List<List<Point2d>>();
            if (segments.Count == 0) return result;

            var remaining = new List<Tuple<Point2d, Point2d>>(segments);
            double tol = 0.5;

            while (remaining.Count > 0)
            {
                var current = new List<Point2d>();
                var first = remaining[0];
                remaining.RemoveAt(0);

                current.Add(first.Item1);
                current.Add(first.Item2);

                bool extended = true;
                while (extended)
                {
                    extended = false;
                    Point2d endPt = current[current.Count - 1];

                    for (int i = 0; i < remaining.Count; i++)
                    {
                        var seg = remaining[i];
                        if (endPt.GetDistanceTo(seg.Item1) <= tol)
                        {
                            current.Add(seg.Item2);
                            remaining.RemoveAt(i);
                            extended = true;
                            break;
                        }
                        else if (endPt.GetDistanceTo(seg.Item2) <= tol)
                        {
                            current.Add(seg.Item1);
                            remaining.RemoveAt(i);
                            extended = true;
                            break;
                        }
                    }

                    if (!extended)
                    {
                        Point2d startPt = current[0];
                        for (int i = 0; i < remaining.Count; i++)
                        {
                            var seg = remaining[i];
                            if (startPt.GetDistanceTo(seg.Item2) <= tol)
                            {
                                current.Insert(0, seg.Item1);
                                remaining.RemoveAt(i);
                                extended = true;
                                break;
                            }
                            else if (startPt.GetDistanceTo(seg.Item1) <= tol)
                            {
                                current.Insert(0, seg.Item2);
                                remaining.RemoveAt(i);
                                extended = true;
                                break;
                            }
                        }
                    }
                }

                if (current.Count >= 2)
                {
                    result.Add(current);
                }
            }

            return result;
        }

        private static void EnsureLayers(Database db, Transaction tr)
        {
            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

            CreateOrUpdateLayer(lt, tr, LayerContourIndex, Color.FromColorIndex(ColorMethod.ByAci, 30));   // Orange đất (Đồng mức chính)
            CreateOrUpdateLayer(lt, tr, LayerContourInter, Color.FromColorIndex(ColorMethod.ByAci, 252));  // Gray nhạt (Đồng mức phụ)
            CreateOrUpdateLayer(lt, tr, LayerContourText, Color.FromColorIndex(ColorMethod.ByAci, 30));     // Orange đất (Đồng điệu với đường chính)
        }

        private static void CreateOrUpdateLayer(LayerTable lt, Transaction tr, string layerName, Color color)
        {
            if (!lt.Has(layerName))
            {
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = layerName,
                    Color = color,
                    IsOff = false,
                    IsFrozen = false,
                    IsLocked = false
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
            else
            {
                try
                {
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForWrite);
                    ltr.Color = color;
                }
                catch { }
            }
        }
    }
}
