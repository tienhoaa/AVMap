using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Colors;

namespace AVMap
{
    public class ExportKmzOptions
    {
        public bool GroupByLayer { get; set; } = true;
        public bool PreserveColors { get; set; } = true;
        public bool HatchTransparent { get; set; } = true;
        public bool AutoBrightenDark { get; set; } = true;
        public double LineWidth { get; set; } = 2.5;
    }

    public class ImportKmzOptions
    {
        public bool KeepFolders { get; set; } = true;
        public string TargetLayer { get; set; } = "GOOGLE_EARTH_IMPORT";
        public bool ImportColors { get; set; } = true;
        public bool ClosePolygons { get; set; } = true;
        public bool ImportLabels { get; set; } = true;
        public bool ZoomExtents { get; set; } = true;
    }

    public static class KmzService
    {
        private static readonly XNamespace KmlNs = "http://www.opengis.net/kml/2.2";

        /// <summary>
        /// Xuất các đối tượng CAD ra file KMZ hoặc KML với đầy đủ tùy chọn chuyên nghiệp
        /// </summary>
        public static int ExportToKmzWithOptions(Document doc, IEnumerable<ObjectId> targetIds, string outFilePath, ProvinceCrs crs, ExportKmzOptions options)
        {
            if (doc == null || targetIds == null || crs == null) return 0;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            if (options == null) options = new ExportKmzOptions();

            var layerFolders = new Dictionary<string, List<XElement>>(StringComparer.OrdinalIgnoreCase);
            var styleDict = new Dictionary<string, string>();
            var stylesList = new List<XElement>();
            int totalEntities = 0;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                foreach (ObjectId id in targetIds)
                {
                    if (!id.IsValid || id.IsErased) continue;
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.IsErased) continue;

                    string layerName = ent.Layer ?? "0";
                    string folderKey = options.GroupByLayer ? layerName : "Bản vẽ CAD";
                    if (!layerFolders.ContainsKey(folderKey))
                    {
                        layerFolders[folderKey] = new List<XElement>();
                    }

                    // 1. Xác định màu sắc thực tế của đối tượng
                    System.Drawing.Color actualColor = System.Drawing.Color.White;
                    if (options.PreserveColors)
                    {
                        if (ent.Color.IsByLayer && lt.Has(layerName))
                        {
                            LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForRead);
                            actualColor = ltr.Color.ColorValue;
                        }
                        else
                        {
                            actualColor = ent.Color.ColorValue;
                        }
                    }

                    // Tự động làm sáng nếu màu quá tối để nổi trên ảnh vệ tinh
                    if (options.AutoBrightenDark && actualColor.R < 35 && actualColor.G < 35 && actualColor.B < 35)
                    {
                        actualColor = System.Drawing.Color.FromArgb(235, 235, 235);
                    }

                    // KML color format: aabbggrr
                    string kmlColorHex = string.Format("ff{0:x2}{1:x2}{2:x2}", actualColor.B, actualColor.G, actualColor.R);
                    string lineStyleId = $"style_line_{kmlColorHex}_{options.LineWidth:F1}";

                    if (!styleDict.ContainsKey(lineStyleId))
                    {
                        styleDict[lineStyleId] = lineStyleId;
                        stylesList.Add(new XElement(KmlNs + "Style",
                            new XAttribute("id", lineStyleId),
                            new XElement(KmlNs + "LineStyle",
                                new XElement(KmlNs + "color", kmlColorHex),
                                new XElement(KmlNs + "width", options.LineWidth.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                            ),
                            new XElement(KmlNs + "PolyStyle",
                                new XElement(KmlNs + "fill", "0"),
                                new XElement(KmlNs + "outline", "1"),
                                new XElement(KmlNs + "color", kmlColorHex)
                            ),
                            new XElement(KmlNs + "IconStyle",
                                new XElement(KmlNs + "color", kmlColorHex),
                                new XElement(KmlNs + "scale", "0.9"),
                                new XElement(KmlNs + "Icon",
                                    new XElement(KmlNs + "href", "http://maps.google.com/mapfiles/kml/shapes/placemark_circle.png")
                                )
                            ),
                            new XElement(KmlNs + "LabelStyle",
                                new XElement(KmlNs + "color", kmlColorHex),
                                new XElement(KmlNs + "scale", "0.85")
                            )
                        ));
                    }

                    string hatchStyleId = $"style_hatch_{kmlColorHex}";
                    if (!styleDict.ContainsKey(hatchStyleId))
                    {
                        styleDict[hatchStyleId] = hatchStyleId;
                        string hatchAlpha = options.HatchTransparent ? "80" : "d0";
                        stylesList.Add(new XElement(KmlNs + "Style",
                            new XAttribute("id", hatchStyleId),
                            new XElement(KmlNs + "LineStyle",
                                new XElement(KmlNs + "color", kmlColorHex),
                                new XElement(KmlNs + "width", "1.5")
                            ),
                            new XElement(KmlNs + "PolyStyle",
                                new XElement(KmlNs + "fill", "1"),
                                new XElement(KmlNs + "outline", "1"),
                                new XElement(KmlNs + "color", string.Format("{0}{1:x2}{2:x2}{3:x2}", hatchAlpha, actualColor.B, actualColor.G, actualColor.R))
                            )
                        ));
                    }

                    XElement placemark = ConvertEntityToPlacemark(ent, tr, crs, lineStyleId, hatchStyleId, layerName);
                    if (placemark != null)
                    {
                        layerFolders[folderKey].Add(placemark);
                        totalEntities++;
                    }
                }

                tr.Commit();
            }

            if (totalEntities == 0) return 0;

            var folderElements = new List<XElement>();
            foreach (var kvp in layerFolders.OrderBy(k => k.Key))
            {
                if (kvp.Value.Count > 0)
                {
                    folderElements.Add(new XElement(KmlNs + "Folder",
                        new XElement(KmlNs + "name", kvp.Key),
                        new XElement(KmlNs + "open", "1"),
                        kvp.Value
                    ));
                }
            }

            XDocument kmlDoc = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(KmlNs + "kml",
                    new XElement(KmlNs + "Document",
                        new XElement(KmlNs + "name", Path.GetFileNameWithoutExtension(outFilePath)),
                        new XElement(KmlNs + "open", "1"),
                        new XElement(KmlNs + "description", $"Xuất từ AutoCAD bằng AVMap. Hệ tọa độ VN-2000 ({crs.Name})."),
                        stylesList,
                        folderElements
                    )
                )
            );

            bool isKmz = outFilePath.EndsWith(".kmz", StringComparison.OrdinalIgnoreCase);
            if (isKmz)
            {
                string tempKml = Path.Combine(Path.GetTempPath(), $"doc_{DateTime.Now.Ticks}.kml");
                kmlDoc.Save(tempKml);

                if (File.Exists(outFilePath)) File.Delete(outFilePath);
                using (ZipArchive zip = ZipFile.Open(outFilePath, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(tempKml, "doc.kml");
                }
                try { File.Delete(tempKml); } catch { }
            }
            else
            {
                kmlDoc.Save(outFilePath);
            }

            ed.WriteMessage($"\n[AVMap] Đã xuất thành công {totalEntities} đối tượng ra Google Earth: {Path.GetFileName(outFilePath)}");
            return totalEntities;
        }

        public static void ExportToKmz(Document doc, SelectionSet sset, string outFilePath, ProvinceCrs crs, bool fillClosedPolylines = false)
        {
            if (doc == null || sset == null || crs == null) return;
            var ids = sset.GetObjectIds();
            var options = new ExportKmzOptions { HatchTransparent = true, LineWidth = 2.5 };
            ExportToKmzWithOptions(doc, ids, outFilePath, crs, options);
        }

        private static XElement ConvertEntityToPlacemark(Entity ent, Transaction tr, ProvinceCrs crs, string lineStyleId, string hatchStyleId, string layerName, int depth = 0)
        {
            // 1. Lightweight Polyline
            if (ent is Polyline pl)
            {
                var coordsSb = new StringBuilder();
                int numVerts = pl.NumberOfVertices;
                if (numVerts < 2) return null;

                for (int i = 0; i < numVerts; i++)
                {
                    Point2d pt = pl.GetPoint2dAt(i);
                    var wgs = VN2000Transform.VN2000ToWGS84(pt.X, pt.Y, crs.CentralMeridian, crs.ScaleFactor);
                    coordsSb.Append($"{wgs.Longitude:F7},{wgs.Latitude:F7},0 ");

                    if (i < numVerts - 1)
                    {
                        if (pl.GetSegmentType(i) == SegmentType.Arc)
                        {
                            bool sampled = false;
                            try
                            {
                                CircularArc2d arc = pl.GetArcSegment2dAt(i);
                                double b = pl.GetBulgeAt(i);
                                double theta = 4.0 * Math.Atan(b);
                                Point2d nextPt = pl.GetPoint2dAt(i + 1);
                                double d = pt.GetDistanceTo(nextPt);
                                double radius = d * (1.0 + b * b) / (4.0 * Math.Abs(b));
                                double arcLength = radius * Math.Abs(theta);

                                int stepsByDist = (int)Math.Ceiling(arcLength / 2.0);
                                int stepsByAngle = (int)Math.Ceiling(Math.Abs(theta) / (Math.PI / 72.0));
                                int steps = Math.Max(30, Math.Min(600, Math.Max(stepsByDist, stepsByAngle)));

                                Point2d[] samples = arc.GetSamplePoints(steps);
                                if (samples != null && samples.Length > 2)
                                {
                                    for (int s = 1; s < samples.Length - 1; s++)
                                    {
                                        var swgs = VN2000Transform.VN2000ToWGS84(samples[s].X, samples[s].Y, crs.CentralMeridian, crs.ScaleFactor);
                                        coordsSb.Append($"{swgs.Longitude:F7},{swgs.Latitude:F7},0 ");
                                    }
                                    sampled = true;
                                }
                            }
                            catch { }

                            if (!sampled)
                            {
                                Point2d nextPt = pl.GetPoint2dAt(i + 1);
                                AppendArcSamplePoints(pt, nextPt, pl.GetBulgeAt(i), crs, coordsSb);
                            }
                        }
                    }
                }

                if (pl.Closed)
                {
                    if (pl.GetSegmentType(numVerts - 1) == SegmentType.Arc)
                    {
                        bool sampled = false;
                        try
                        {
                            CircularArc2d arc = pl.GetArcSegment2dAt(numVerts - 1);
                            double b = pl.GetBulgeAt(numVerts - 1);
                            double theta = 4.0 * Math.Atan(b);
                            Point2d lastPt = pl.GetPoint2dAt(numVerts - 1);
                            Point2d firstPt = pl.GetPoint2dAt(0);
                            double d = lastPt.GetDistanceTo(firstPt);
                            double radius = d * (1.0 + b * b) / (4.0 * Math.Abs(b));
                            double arcLength = radius * Math.Abs(theta);

                            int stepsByDist = (int)Math.Ceiling(arcLength / 2.0);
                            int stepsByAngle = (int)Math.Ceiling(Math.Abs(theta) / (Math.PI / 72.0));
                            int steps = Math.Max(30, Math.Min(600, Math.Max(stepsByDist, stepsByAngle)));

                            Point2d[] samples = arc.GetSamplePoints(steps);
                            if (samples != null && samples.Length > 2)
                            {
                                for (int s = 1; s < samples.Length - 1; s++)
                                {
                                    var swgs = VN2000Transform.VN2000ToWGS84(samples[s].X, samples[s].Y, crs.CentralMeridian, crs.ScaleFactor);
                                    coordsSb.Append($"{swgs.Longitude:F7},{swgs.Latitude:F7},0 ");
                                }
                                sampled = true;
                            }
                        }
                        catch { }

                        if (!sampled)
                        {
                            Point2d lastPt = pl.GetPoint2dAt(numVerts - 1);
                            Point2d firstPt = pl.GetPoint2dAt(0);
                            AppendArcSamplePoints(lastPt, firstPt, pl.GetBulgeAt(numVerts - 1), crs, coordsSb);
                        }
                    }

                    Point2d firstPtFinal = pl.GetPoint2dAt(0);
                    var firstWgs = VN2000Transform.VN2000ToWGS84(firstPtFinal.X, firstPtFinal.Y, crs.CentralMeridian, crs.ScaleFactor);
                    coordsSb.Append($"{firstWgs.Longitude:F7},{firstWgs.Latitude:F7},0");
                }

                return new XElement(KmlNs + "Placemark",
                    new XElement(KmlNs + "name", layerName),
                    new XElement(KmlNs + "styleUrl", $"#{lineStyleId}"),
                    new XElement(KmlNs + "LineString",
                        new XElement(KmlNs + "altitudeMode", "clampToGround"),
                        new XElement(KmlNs + "coordinates", coordsSb.ToString().Trim())
                    )
                );
            }
            // 2. Heavyweight 2D Polyline
            else if (ent is Polyline2d pl2d)
            {
                var coordsSb = new StringBuilder();
                var vertices = new List<(Point2d pt, double bulge)>();

                foreach (ObjectId vId in pl2d)
                {
                    Vertex2d v = tr.GetObject(vId, OpenMode.ForRead) as Vertex2d;
                    if (v != null && v.VertexType != Vertex2dType.SplineControlVertex)
                    {
                        vertices.Add((new Point2d(v.Position.X, v.Position.Y), v.Bulge));
                    }
                }

                int numVerts = vertices.Count;
                if (numVerts < 2) return null;

                for (int i = 0; i < numVerts; i++)
                {
                    Point2d pt = vertices[i].pt;
                    var wgs = VN2000Transform.VN2000ToWGS84(pt.X, pt.Y, crs.CentralMeridian, crs.ScaleFactor);
                    coordsSb.Append($"{wgs.Longitude:F7},{wgs.Latitude:F7},0 ");

                    if (i < numVerts - 1)
                    {
                        double bulge = vertices[i].bulge;
                        if (Math.Abs(bulge) > 1e-6)
                        {
                            Point2d nextPt = vertices[i + 1].pt;
                            AppendArcSamplePoints(pt, nextPt, bulge, crs, coordsSb);
                        }
                    }
                }

                if (pl2d.Closed)
                {
                    double closingBulge = vertices[numVerts - 1].bulge;
                    Point2d lastPt = vertices[numVerts - 1].pt;
                    Point2d firstPt = vertices[0].pt;

                    if (Math.Abs(closingBulge) > 1e-6)
                    {
                        AppendArcSamplePoints(lastPt, firstPt, closingBulge, crs, coordsSb);
                    }

                    var firstWgs = VN2000Transform.VN2000ToWGS84(firstPt.X, firstPt.Y, crs.CentralMeridian, crs.ScaleFactor);
                    coordsSb.Append($"{firstWgs.Longitude:F7},{firstWgs.Latitude:F7},0");
                }

                return new XElement(KmlNs + "Placemark",
                    new XElement(KmlNs + "name", layerName),
                    new XElement(KmlNs + "styleUrl", $"#{lineStyleId}"),
                    new XElement(KmlNs + "LineString",
                        new XElement(KmlNs + "altitudeMode", "clampToGround"),
                        new XElement(KmlNs + "coordinates", coordsSb.ToString().Trim())
                    )
                );
            }
            // 3. 3D Polyline
            else if (ent is Polyline3d pl3d)
            {
                var coordsSb = new StringBuilder();
                Point3d? firstPt = null;

                foreach (ObjectId vId in pl3d)
                {
                    PolylineVertex3d v = tr.GetObject(vId, OpenMode.ForRead) as PolylineVertex3d;
                    if (v != null)
                    {
                        if (firstPt == null) firstPt = v.Position;
                        var wgs = VN2000Transform.VN2000ToWGS84(v.Position.X, v.Position.Y, crs.CentralMeridian, crs.ScaleFactor);
                        coordsSb.Append($"{wgs.Longitude:F7},{wgs.Latitude:F7},0 ");
                    }
                }

                if (pl3d.Closed && firstPt != null)
                {
                    var firstWgs = VN2000Transform.VN2000ToWGS84(firstPt.Value.X, firstPt.Value.Y, crs.CentralMeridian, crs.ScaleFactor);
                    coordsSb.Append($"{firstWgs.Longitude:F7},{firstWgs.Latitude:F7},0");
                }

                return new XElement(KmlNs + "Placemark",
                    new XElement(KmlNs + "name", layerName),
                    new XElement(KmlNs + "styleUrl", $"#{lineStyleId}"),
                    new XElement(KmlNs + "LineString",
                        new XElement(KmlNs + "altitudeMode", "clampToGround"),
                        new XElement(KmlNs + "coordinates", coordsSb.ToString().Trim())
                    )
                );
            }
            // 4. Spline (đường cong tự nhiên trắc địa / tim tuyến cao cấp)
            else if (ent is Spline spline)
            {
                var coordsSb = new StringBuilder();
                try
                {
                    double startDist = spline.GetDistanceAtParameter(spline.StartParam);
                    double endDist = spline.GetDistanceAtParameter(spline.EndParam);
                    double length = Math.Abs(endDist - startDist);
                    if (length > 0)
                    {
                        int steps = Math.Max(16, Math.Min(300, (int)(length / 2.0))); // mỗi 2m một điểm
                        for (int s = 0; s <= steps; s++)
                        {
                            double dist = length * (s / (double)steps);
                            Point3d pt = spline.GetPointAtDist(dist);
                            var wgs = VN2000Transform.VN2000ToWGS84(pt.X, pt.Y, crs.CentralMeridian, crs.ScaleFactor);
                            coordsSb.Append($"{wgs.Longitude:F7},{wgs.Latitude:F7},0 ");
                        }

                        return new XElement(KmlNs + "Placemark",
                            new XElement(KmlNs + "name", layerName),
                            new XElement(KmlNs + "styleUrl", $"#{lineStyleId}"),
                            new XElement(KmlNs + "LineString",
                                new XElement(KmlNs + "altitudeMode", "clampToGround"),
                                new XElement(KmlNs + "coordinates", coordsSb.ToString().Trim())
                            )
                        );
                    }
                }
                catch { }
            }
            // 5. Line
            else if (ent is Line line)
            {
                var wgsStart = VN2000Transform.VN2000ToWGS84(line.StartPoint.X, line.StartPoint.Y, crs.CentralMeridian, crs.ScaleFactor);
                var wgsEnd = VN2000Transform.VN2000ToWGS84(line.EndPoint.X, line.EndPoint.Y, crs.CentralMeridian, crs.ScaleFactor);

                return new XElement(KmlNs + "Placemark",
                    new XElement(KmlNs + "name", layerName),
                    new XElement(KmlNs + "styleUrl", $"#{lineStyleId}"),
                    new XElement(KmlNs + "LineString",
                        new XElement(KmlNs + "altitudeMode", "clampToGround"),
                        new XElement(KmlNs + "coordinates", $"{wgsStart.Longitude:F7},{wgsStart.Latitude:F7},0 {wgsEnd.Longitude:F7},{wgsEnd.Latitude:F7},0")
                    )
                );
            }
            // 6. Hatch
            else if (ent is Hatch hatch)
            {
                var polygonElements = new List<XElement>();
                int numLoops = hatch.NumberOfLoops;

                for (int l = 0; l < numLoops; l++)
                {
                    HatchLoop loop = hatch.GetLoopAt(l);
                    var loopPts = ExtractHatchLoopPoints(loop);
                    if (loopPts == null || loopPts.Count < 3) continue;

                    var coordsSb = new StringBuilder();
                    foreach (var pt in loopPts)
                    {
                        var wgs = VN2000Transform.VN2000ToWGS84(pt.X, pt.Y, crs.CentralMeridian, crs.ScaleFactor);
                        coordsSb.Append($"{wgs.Longitude:F7},{wgs.Latitude:F7},0 ");
                    }
                    var firstWgs = VN2000Transform.VN2000ToWGS84(loopPts[0].X, loopPts[0].Y, crs.CentralMeridian, crs.ScaleFactor);
                    coordsSb.Append($"{firstWgs.Longitude:F7},{firstWgs.Latitude:F7},0");

                    polygonElements.Add(new XElement(KmlNs + "Polygon",
                        new XElement(KmlNs + "altitudeMode", "clampToGround"),
                        new XElement(KmlNs + "outerBoundaryIs",
                            new XElement(KmlNs + "LinearRing",
                                new XElement(KmlNs + "coordinates", coordsSb.ToString().Trim())
                            )
                        )
                    ));
                }

                if (polygonElements.Count == 0) return null;

                XElement geomElement = polygonElements.Count == 1 ? polygonElements[0] : new XElement(KmlNs + "MultiGeometry", polygonElements);

                return new XElement(KmlNs + "Placemark",
                    new XElement(KmlNs + "name", layerName + " [Hatch]"),
                    new XElement(KmlNs + "styleUrl", $"#{hatchStyleId}"),
                    geomElement
                );
            }
            // 7. Circle
            else if (ent is Circle circle)
            {
                var coordsSb = new StringBuilder();
                double radius = circle.Radius;
                Point3d center = circle.Center;
                double circumference = 2.0 * Math.PI * radius;
                int steps = Math.Max(72, Math.Min(720, (int)Math.Ceiling(circumference / 2.0))); // mỗi 2m 1 điểm
                for (int i = 0; i <= steps; i++)
                {
                    double angle = (i / (double)steps) * Math.PI * 2.0;
                    double cx = center.X + radius * Math.Cos(angle);
                    double cy = center.Y + radius * Math.Sin(angle);
                    var wgs = VN2000Transform.VN2000ToWGS84(cx, cy, crs.CentralMeridian, crs.ScaleFactor);
                    coordsSb.Append($"{wgs.Longitude:F7},{wgs.Latitude:F7},0 ");
                }

                return new XElement(KmlNs + "Placemark",
                    new XElement(KmlNs + "name", layerName),
                    new XElement(KmlNs + "styleUrl", $"#{lineStyleId}"),
                    new XElement(KmlNs + "LineString",
                        new XElement(KmlNs + "altitudeMode", "clampToGround"),
                        new XElement(KmlNs + "coordinates", coordsSb.ToString().Trim())
                    )
                );
            }
            // 8. Arc
            else if (ent is Arc arc)
            {
                var coordsSb = new StringBuilder();
                double radius = arc.Radius;
                Point3d center = arc.Center;
                double startAng = arc.StartAngle;
                double endAng = arc.EndAngle;
                if (endAng < startAng) endAng += Math.PI * 2;
                double sweep = endAng - startAng;
                double arcLength = radius * sweep;

                int stepsByDist = (int)Math.Ceiling(arcLength / 2.0); // mỗi 2m một điểm
                int stepsByAngle = (int)Math.Ceiling(sweep / (Math.PI / 72.0)); // mỗi 2.5 độ một điểm
                int steps = Math.Max(30, Math.Min(600, Math.Max(stepsByDist, stepsByAngle)));

                for (int i = 0; i <= steps; i++)
                {
                    double angle = startAng + sweep * (i / (double)steps);
                    double cx = center.X + radius * Math.Cos(angle);
                    double cy = center.Y + radius * Math.Sin(angle);
                    var wgs = VN2000Transform.VN2000ToWGS84(cx, cy, crs.CentralMeridian, crs.ScaleFactor);
                    coordsSb.Append($"{wgs.Longitude:F7},{wgs.Latitude:F7},0 ");
                }

                return new XElement(KmlNs + "Placemark",
                    new XElement(KmlNs + "name", layerName),
                    new XElement(KmlNs + "styleUrl", $"#{lineStyleId}"),
                    new XElement(KmlNs + "LineString",
                        new XElement(KmlNs + "altitudeMode", "clampToGround"),
                        new XElement(KmlNs + "coordinates", coordsSb.ToString().Trim())
                    )
                );
            }
            // 9. DBText
            else if (ent is DBText dbTxt)
            {
                var wgs = VN2000Transform.VN2000ToWGS84(dbTxt.Position.X, dbTxt.Position.Y, crs.CentralMeridian, crs.ScaleFactor);
                string textStr = CleanCadText(dbTxt.TextString);
                return new XElement(KmlNs + "Placemark",
                    new XElement(KmlNs + "name", textStr),
                    new XElement(KmlNs + "styleUrl", $"#{lineStyleId}"),
                    new XElement(KmlNs + "Point",
                        new XElement(KmlNs + "altitudeMode", "clampToGround"),
                        new XElement(KmlNs + "coordinates", $"{wgs.Longitude:F7},{wgs.Latitude:F7},0")
                    )
                );
            }
            // 10. MText
            else if (ent is MText mTxt)
            {
                var wgs = VN2000Transform.VN2000ToWGS84(mTxt.Location.X, mTxt.Location.Y, crs.CentralMeridian, crs.ScaleFactor);
                string textStr = CleanCadText(mTxt.Text);
                return new XElement(KmlNs + "Placemark",
                    new XElement(KmlNs + "name", textStr),
                    new XElement(KmlNs + "styleUrl", $"#{lineStyleId}"),
                    new XElement(KmlNs + "Point",
                        new XElement(KmlNs + "altitudeMode", "clampToGround"),
                        new XElement(KmlNs + "coordinates", $"{wgs.Longitude:F7},{wgs.Latitude:F7},0")
                    )
                );
            }
            // 11. DBPoint
            else if (ent is DBPoint pt)
            {
                var wgs = VN2000Transform.VN2000ToWGS84(pt.Position.X, pt.Position.Y, crs.CentralMeridian, crs.ScaleFactor);
                return new XElement(KmlNs + "Placemark",
                    new XElement(KmlNs + "name", layerName),
                    new XElement(KmlNs + "styleUrl", $"#{lineStyleId}"),
                    new XElement(KmlNs + "Point",
                        new XElement(KmlNs + "altitudeMode", "clampToGround"),
                        new XElement(KmlNs + "coordinates", $"{wgs.Longitude:F7},{wgs.Latitude:F7},0")
                    )
                );
            }
            // 12. BlockReference (Khối block: mốc tọa độ, cọc GPMB, ký hiệu, hố ga, cây xanh, nhà cửa, kết cấu...)
            else if (ent is BlockReference blkRef)
            {
                return ConvertBlockReferenceToPlacemark(blkRef, tr, crs, lineStyleId, hatchStyleId, layerName, depth);
            }

            return null;
        }

        private static XElement ConvertBlockReferenceToPlacemark(
            BlockReference blkRef, 
            Transaction tr, 
            ProvinceCrs crs, 
            string lineStyleId, 
            string hatchStyleId, 
            string layerName, 
            int depth = 0)
        {
            if (blkRef == null || !blkRef.ObjectId.IsValid || blkRef.IsErased) return null;

            // 1. Lấy tên Block thực tế (xử lý cả Dynamic Block hoặc Anonymous Block dạng *U...)
            string blockName = blkRef.Name;
            try
            {
                if (blkRef.IsDynamicBlock || (blockName != null && blockName.StartsWith("*U", StringComparison.OrdinalIgnoreCase)))
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blkRef.DynamicBlockTableRecord, OpenMode.ForRead);
                    blockName = btr.Name;
                }
            }
            catch { }
            if (string.IsNullOrEmpty(blockName)) blockName = "Block";

            // 2. Trích xuất toàn bộ thuộc tính Attributes (Tag - Value)
            var attrDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (blkRef.AttributeCollection != null && blkRef.AttributeCollection.Count > 0)
                {
                    foreach (ObjectId attId in blkRef.AttributeCollection)
                    {
                        if (!attId.IsValid || attId.IsErased) continue;
                        AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                        if (att != null && !att.IsErased)
                        {
                            string tag = att.Tag?.Trim();
                            string val = CleanCadText(att.TextString?.Trim());
                            if (!string.IsNullOrEmpty(tag))
                            {
                                attrDict[tag] = val;
                            }
                        }
                    }
                }
            }
            catch { }

            // 3. Xác định tên hiển thị tốt nhất cho Placemark trên Google Earth
            string displayName = blockName;
            string[] priorityTags = new[] { "NAME", "TEN", "TEN_DIEM", "TENDIEM", "SO_HIEU", "SOHIEU", "KY_HIEU", "KYHIEU", "ID", "CODE", "LABEL", "STT", "DIEM", "TEXT" };
            foreach (string tag in priorityTags)
            {
                if (attrDict.TryGetValue(tag, out string tagVal) && !string.IsNullOrWhiteSpace(tagVal))
                {
                    displayName = tagVal;
                    break;
                }
            }
            if (displayName == blockName && attrDict.Count > 0)
            {
                var firstVal = attrDict.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                if (!string.IsNullOrEmpty(firstVal))
                {
                    displayName = $"{blockName} ({firstVal})";
                }
            }

            // 4. Tạo mô tả chi tiết Description (HTML bảng thuộc tính hiện trong Popup Google Earth)
            var descSb = new StringBuilder();
            descSb.Append("<div style='font-family:Arial,sans-serif;font-size:13px;line-height:1.5;'>");
            descSb.Append("<p style='margin:0 0 8px 0;'>");
            descSb.Append($"<b>Khối (Block):</b> <span style='color:#0284C7;font-weight:bold;'>{blockName}</span><br/>");
            descSb.Append($"<b>Layer:</b> {layerName}<br/>");
            descSb.Append($"<b>Tọa độ VN-2000:</b> X = {blkRef.Position.X:F3}, Y = {blkRef.Position.Y:F3}");
            if (Math.Abs(blkRef.Position.Z) > 0.001)
            {
                descSb.Append($", Z = {blkRef.Position.Z:F3}m");
            }
            descSb.Append("</p>");

            if (attrDict.Count > 0)
            {
                descSb.Append("<table border='1' cellspacing='0' cellpadding='5' style='border-collapse:collapse;width:100%;font-size:12px;border:1px solid #CBD5E1;'>");
                descSb.Append("<tr style='background:#F1F5F9;text-align:left;'><th>Thuộc tính</th><th>Giá trị</th></tr>");
                foreach (var kvp in attrDict)
                {
                    descSb.Append($"<tr><td style='color:#475569;font-weight:bold;'>{kvp.Key}</td><td>{kvp.Value}</td></tr>");
                }
                descSb.Append("</table>");
            }
            descSb.Append("</div>");

            // 5. Trích xuất hình học của Block
            var geomElements = new List<XElement>();

            // A. Luôn tạo điểm Point tại vị trí chèn của Block (để hiện Icon ghim vị trí mốc/cọc trên Google Earth)
            Point3d insPt = blkRef.Position;
            var wgsIns = VN2000Transform.VN2000ToWGS84(insPt.X, insPt.Y, crs.CentralMeridian, crs.ScaleFactor);
            XElement pointGeom = new XElement(KmlNs + "Point",
                new XElement(KmlNs + "altitudeMode", "clampToGround"),
                new XElement(KmlNs + "coordinates", $"{wgsIns.Longitude:F7},{wgsIns.Latitude:F7},0")
            );
            geomElements.Add(pointGeom);

            // B. Rã các đối tượng nét vẽ hình học bên trong Block (Linework, Polylines, Circles, Hatches)
            if (depth < 2)
            {
                try
                {
                    DBObjectCollection exploded = new DBObjectCollection();
                    blkRef.Explode(exploded);

                    foreach (DBObject obj in exploded)
                    {
                        if (obj is Entity subEnt)
                        {
                            try
                            {
                                // Bỏ qua AttributeDefinition vì đã đọc từ AttributeReference thực tế
                                if (subEnt is AttributeDefinition) continue;

                                XElement subPlacemark = ConvertEntityToPlacemark(subEnt, tr, crs, lineStyleId, hatchStyleId, layerName, depth + 1);
                                if (subPlacemark != null)
                                {
                                    foreach (var el in subPlacemark.Elements())
                                    {
                                        string localName = el.Name.LocalName;
                                        if (localName == "LineString" || localName == "Polygon" || localName == "MultiGeometry")
                                        {
                                            geomElements.Add(el);
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                subEnt.Dispose();
                            }
                        }
                        else
                        {
                            obj?.Dispose();
                        }
                    }
                }
                catch { }
            }

            XElement finalGeometry = geomElements.Count == 1 
                ? geomElements[0] 
                : new XElement(KmlNs + "MultiGeometry", geomElements);

            return new XElement(KmlNs + "Placemark",
                new XElement(KmlNs + "name", displayName),
                new XElement(KmlNs + "description", new XCData(descSb.ToString())),
                new XElement(KmlNs + "styleUrl", $"#{lineStyleId}"),
                finalGeometry
            );
        }

        /// <summary>
        /// Thuật toán nội suy cung tròn Arc từ Bulge theo chuẩn hình học DXF/CAD với độ chính xác tuyệt đối.
        /// Bulge = tan(theta / 4).
        /// </summary>
        public static List<Point2d> SampleArcPoints2d(Point2d p1, Point2d p2, double bulge)
        {
            var result = new List<Point2d>();
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-6 || Math.Abs(bulge) < 1e-6) return result;

            double k = (1.0 - bulge * bulge) / (4.0 * bulge);
            double mx = (p1.X + p2.X) * 0.5;
            double my = (p1.Y + p2.Y) * 0.5;
            double cx = mx - k * dy;
            double cy = my + k * dx;

            double radius = Math.Sqrt((p1.X - cx) * (p1.X - cx) + (p1.Y - cy) * (p1.Y - cy));
            double startAngle = Math.Atan2(p1.Y - cy, p1.X - cx);
            double sweepAngle = 4.0 * Math.Atan(bulge);

            // Mật độ mẫu siêu mịn theo chiều dài cung thực tế:
            // 1. Theo chiều dài: cứ 1.5m - 2m lấy 1 điểm (đảm bảo cung lớn hàng trăm mét vẫn mượt tuyệt đối)
            double arcLength = radius * Math.Abs(sweepAngle);
            int stepsByDist = (int)Math.Ceiling(arcLength / 2.0);

            // 2. Theo góc quét: tối thiểu 1 điểm cho mỗi 2.5 độ
            int stepsByAngle = (int)Math.Ceiling(Math.Abs(sweepAngle) / (Math.PI / 72.0));

            // Tối thiểu 30 điểm cho mỗi cung tròn, tối đa 600 điểm
            int steps = Math.Max(30, Math.Min(600, Math.Max(stepsByDist, stepsByAngle)));

            for (int s = 1; s < steps; s++)
            {
                double t = s / (double)steps;
                double ang = startAngle + t * sweepAngle;
                double sx = cx + radius * Math.Cos(ang);
                double sy = cy + radius * Math.Sin(ang);
                result.Add(new Point2d(sx, sy));
            }
            return result;
        }

        private static void AppendArcSamplePoints(Point2d p1, Point2d p2, double bulge, ProvinceCrs crs, StringBuilder sb)
        {
            var pts = SampleArcPoints2d(p1, p2, bulge);
            foreach (var pt in pts)
            {
                var swgs = VN2000Transform.VN2000ToWGS84(pt.X, pt.Y, crs.CentralMeridian, crs.ScaleFactor);
                sb.Append($"{swgs.Longitude:F7},{swgs.Latitude:F7},0 ");
            }
        }

        private static List<Point2d> ExtractHatchLoopPoints(HatchLoop loop)
        {
            var pts = new List<Point2d>();
            if (loop.IsPolyline)
            {
                var bvc = loop.Polyline;
                int count = bvc.Count;
                for (int i = 0; i < count; i++)
                {
                    BulgeVertex bv = bvc[i];
                    pts.Add(bv.Vertex);
                    if (Math.Abs(bv.Bulge) > 1e-6)
                    {
                        Point2d nextV = bvc[(i + 1) % count].Vertex;
                        var arcPts = SampleArcPoints2d(bv.Vertex, nextV, bv.Bulge);
                        pts.AddRange(arcPts);
                    }
                }
            }
            else if (loop.Curves != null)
            {
                foreach (Curve2d curve in loop.Curves)
                {
                    if (curve is LineSegment2d lineSeg)
                    {
                        pts.Add(lineSeg.StartPoint);
                    }
                    else if (curve is CircularArc2d arcSeg)
                    {
                        try
                        {
                            double sweep = Math.Abs(arcSeg.EndAngle - arcSeg.StartAngle);
                            double arcLen = arcSeg.Radius * sweep;
                            int steps = Math.Max(24, Math.Min(300, (int)Math.Ceiling(arcLen / 2.0)));
                            Point2d[] samples = arcSeg.GetSamplePoints(steps);
                            if (samples != null && samples.Length > 0)
                            {
                                for (int s = 0; s < samples.Length - 1; s++)
                                {
                                    pts.Add(samples[s]);
                                }
                            }
                        }
                        catch
                        {
                            if (curve.HasStartPoint) pts.Add(curve.StartPoint);
                        }
                    }
                    else
                    {
                        try
                        {
                            Point2d[] samples = curve.GetSamplePoints(16);
                            if (samples != null && samples.Length > 0)
                            {
                                for (int s = 0; s < samples.Length - 1; s++)
                                {
                                    pts.Add(samples[s]);
                                }
                            }
                        }
                        catch
                        {
                            if (curve.HasStartPoint) pts.Add(curve.StartPoint);
                        }
                    }
                }
            }
            return pts;
        }

        /// <summary>
        /// Làm sạch mã định dạng MText và tự động chuyển đổi font TCVN3 sang Unicode tiếng Việt chuẩn
        /// </summary>
        public static string CleanCadText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            // 1. Loại bỏ định dạng MText AutoCAD (\P, \A1;, \C1;, \f...;)
            string clean = raw.Replace("\\P", " ").Replace("\\p", " ");
            clean = Regex.Replace(clean, @"\\[A-Za-z0-9]+(\.[0-9]+)?;?", "");
            clean = Regex.Replace(clean, @"\\f[^;]+;", "");
            clean = clean.Replace("{", "").Replace("}", "").Trim();

            // 2. Chuyển đổi TCVN3 (ABC) sang Unicode nếu phát hiện ký tự TCVN3
            clean = ConvertTcvn3ToUnicode(clean);

            return clean;
        }

        private static string ConvertTcvn3ToUnicode(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            bool hasTcvn = false;
            foreach (char c in input)
            {
                int code = (int)c;
                if (code == 167 || code == 168 || code == 169 || code == 170 || code == 171 || code == 172 || code == 173 || code == 174 ||
                    (code >= 181 && code <= 254))
                {
                    hasTcvn = true;
                    break;
                }
            }
            if (!hasTcvn) return input;

            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                int code = (int)input[i];
                if (Tcvn3Map.TryGetValue(code, out string uStr))
                {
                    sb.Append(uStr);
                }
                else
                {
                    sb.Append(input[i]);
                }
            }
            return sb.ToString();
        }

        private static readonly Dictionary<int, string> Tcvn3Map = new Dictionary<int, string>
        {
            { 167, "Đ" }, { 174, "đ" },
            { 168, "ă" }, { 169, "â" }, { 170, "ê" }, { 171, "ô" }, { 172, "ơ" }, { 173, "ư" },
            { 181, "à" }, { 182, "ả" }, { 183, "ã" }, { 184, "á" }, { 185, "ạ" },
            { 187, "ằ" }, { 188, "ẳ" }, { 189, "ẵ" }, { 190, "ắ" }, { 198, "ặ" },
            { 199, "ầ" }, { 200, "ẩ" }, { 201, "ẫ" }, { 202, "ấ" }, { 203, "ậ" },
            { 204, "è" }, { 206, "ẻ" }, { 207, "ẽ" }, { 208, "é" }, { 209, "ẹ" },
            { 210, "ề" }, { 211, "ể" }, { 212, "ễ" }, { 213, "ế" }, { 214, "ệ" },
            { 215, "ì" }, { 216, "ỉ" }, { 220, "ĩ" }, { 221, "í" }, { 222, "ị" },
            { 223, "ò" }, { 225, "ỏ" }, { 226, "õ" }, { 227, "ó" }, { 228, "ọ" },
            { 229, "ồ" }, { 230, "ổ" }, { 231, "ỗ" }, { 232, "ố" }, { 233, "ộ" },
            { 234, "ờ" }, { 235, "ở" }, { 236, "ỡ" }, { 237, "ớ" }, { 238, "ợ" },
            { 239, "ù" }, { 241, "ủ" }, { 242, "ũ" }, { 243, "ú" }, { 244, "ụ" },
            { 245, "ừ" }, { 246, "ử" }, { 247, "ữ" }, { 248, "ứ" }, { 249, "ự" },
            { 250, "ỳ" }, { 251, "ỷ" }, { 252, "ỹ" }, { 253, "ý" }, { 254, "ỵ" }
        };

        /// <summary>
        /// Nhập file KMZ hoặc KML vào bản vẽ AutoCAD với đầy đủ tùy chọn (Màu sắc, Layer, Nhãn Text)
        /// </summary>
        public static int ImportFromKmzWithOptions(Document doc, string inFilePath, ProvinceCrs crs, ImportKmzOptions options)
        {
            if (doc == null || !File.Exists(inFilePath) || crs == null) return 0;
            Database db = doc.Database;
            if (options == null) options = new ImportKmzOptions();

            string kmlContent = "";
            bool isKmz = inFilePath.EndsWith(".kmz", StringComparison.OrdinalIgnoreCase);

            if (isKmz)
            {
                using (ZipArchive zip = ZipFile.OpenRead(inFilePath))
                {
                    ZipArchiveEntry kmlEntry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".kml", StringComparison.OrdinalIgnoreCase));
                    if (kmlEntry == null)
                    {
                        doc.Editor.WriteMessage("\n[AVMap] Không tìm thấy file KML bên trong tập tin KMZ.");
                        return 0;
                    }
                    using (var reader = new StreamReader(kmlEntry.Open(), Encoding.UTF8))
                    {
                        kmlContent = reader.ReadToEnd();
                    }
                }
            }
            else
            {
                kmlContent = File.ReadAllText(inFilePath, Encoding.UTF8);
            }

            XDocument docXml = XDocument.Parse(kmlContent);
            int importedCount = 0;

            // Đọc bảng Style màu sắc
            var styleColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            foreach (var styleEl in docXml.Descendants(KmlNs + "Style"))
            {
                string sId = styleEl.Attribute("id")?.Value;
                if (!string.IsNullOrEmpty(sId))
                {
                    string colorHex = styleEl.Element(KmlNs + "LineStyle")?.Element(KmlNs + "color")?.Value
                                   ?? styleEl.Element(KmlNs + "PolyStyle")?.Element(KmlNs + "color")?.Value;
                    if (!string.IsNullOrEmpty(colorHex) && colorHex.Length == 8)
                    {
                        Color c = ParseKmlColor(colorHex);
                        if (c != null) styleColors[sId] = c;
                    }
                }
            }

            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

                foreach (var placemark in docXml.Descendants(KmlNs + "Placemark"))
                {
                    string pName = placemark.Element(KmlNs + "name")?.Value?.Trim() ?? "";
                    
                    // Xác định Layer đích
                    string targetLayerName = options.TargetLayer;
                    if (options.KeepFolders)
                    {
                        string folderName = placemark.Ancestors(KmlNs + "Folder").FirstOrDefault()?.Element(KmlNs + "name")?.Value?.Trim();
                        if (!string.IsNullOrEmpty(folderName)) targetLayerName = CleanLayerName(folderName);
                        else if (!string.IsNullOrEmpty(pName)) targetLayerName = CleanLayerName(pName);
                    }
                    EnsureLayerExists(lt, tr, targetLayerName);

                    // Xác định màu sắc
                    Color entityColor = Color.FromColorIndex(ColorMethod.ByAci, 7); // Mặc định trắng
                    if (options.ImportColors)
                    {
                        string sUrl = placemark.Element(KmlNs + "styleUrl")?.Value?.Trim()?.TrimStart('#');
                        if (!string.IsNullOrEmpty(sUrl) && styleColors.ContainsKey(sUrl))
                        {
                            entityColor = styleColors[sUrl];
                        }
                        else
                        {
                            string inlineColor = placemark.Element(KmlNs + "Style")?.Element(KmlNs + "LineStyle")?.Element(KmlNs + "color")?.Value;
                            if (!string.IsNullOrEmpty(inlineColor) && inlineColor.Length == 8)
                            {
                                entityColor = ParseKmlColor(inlineColor);
                            }
                        }
                    }

                    // A. Điểm Point
                    var pointEl = placemark.Element(KmlNs + "Point");
                    if (pointEl != null)
                    {
                        string coordStr = pointEl.Element(KmlNs + "coordinates")?.Value?.Trim();
                        if (!string.IsNullOrEmpty(coordStr))
                        {
                            var parts = coordStr.Split(new[] { ' ', '\n', '\r', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 &&
                                double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon) &&
                                double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat))
                            {
                                var cadPt = VN2000Transform.WGS84ToVN2000(lat, lon, crs.CentralMeridian, crs.ScaleFactor);
                                DBPoint p = new DBPoint(new Point3d(cadPt.X, cadPt.Y, 0));
                                p.Layer = targetLayerName;
                                p.Color = entityColor;
                                btr.AppendEntity(p);
                                tr.AddNewlyCreatedDBObject(p, true);
                                importedCount++;

                                if (options.ImportLabels && !string.IsNullOrEmpty(pName))
                                {
                                    DBText txt = new DBText
                                    {
                                        Position = new Point3d(cadPt.X + 1.0, cadPt.Y + 1.0, 0),
                                        Height = 2.0,
                                        TextString = pName,
                                        Layer = targetLayerName,
                                        Color = entityColor
                                    };
                                    btr.AppendEntity(txt);
                                    tr.AddNewlyCreatedDBObject(txt, true);
                                }
                            }
                        }
                    }

                    // B. Tuyến LineString
                    var lineEl = placemark.Element(KmlNs + "LineString");
                    if (lineEl != null)
                    {
                        string coordStr = lineEl.Element(KmlNs + "coordinates")?.Value?.Trim();
                        if (CreatePolylineFromCoordinates(coordStr, crs, false, targetLayerName, entityColor, btr, tr))
                        {
                            importedCount++;
                        }
                    }

                    // C. Đa giác Polygon
                    var polyEl = placemark.Element(KmlNs + "Polygon");
                    if (polyEl != null)
                    {
                        string coordStr = polyEl.Descendants(KmlNs + "coordinates").FirstOrDefault()?.Value?.Trim();
                        if (CreatePolylineFromCoordinates(coordStr, crs, options.ClosePolygons, targetLayerName, entityColor, btr, tr))
                        {
                            importedCount++;
                        }
                    }
                }

                tr.Commit();
            }

            if (options.ZoomExtents && importedCount > 0)
            {
                try
                {
                    doc.SendStringToExecute("._zoom _e ", true, false, false);
                }
                catch { }
            }

            doc.Editor.WriteMessage($"\n[AVMap] Đã nhập thành công {importedCount} đối tượng vào ModelSpace theo hệ {crs.Name}.");
            return importedCount;
        }

        public static void ImportFromKmz(Document doc, string inFilePath, ProvinceCrs crs)
        {
            ImportFromKmzWithOptions(doc, inFilePath, crs, new ImportKmzOptions());
        }

        private static bool CreatePolylineFromCoordinates(string rawCoords, ProvinceCrs crs, bool isClosed, string layer, Color color, BlockTableRecord btr, Transaction tr)
        {
            if (string.IsNullOrEmpty(rawCoords)) return false;
            string[] points = rawCoords.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (points.Length < 2) return false;

            Polyline pl = new Polyline();
            int vIndex = 0;

            foreach (var ptStr in points)
            {
                var parts = ptStr.Split(',');
                if (parts.Length >= 2 &&
                    double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon) &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat))
                {
                    var cadPt = VN2000Transform.WGS84ToVN2000(lat, lon, crs.CentralMeridian, crs.ScaleFactor);
                    pl.AddVertexAt(vIndex++, new Point2d(cadPt.X, cadPt.Y), 0, 0, 0);
                }
            }

            if (pl.NumberOfVertices >= 2)
            {
                pl.Closed = isClosed;
                pl.Layer = layer;
                pl.Color = color;
                btr.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);
                return true;
            }
            else
            {
                pl.Dispose();
                return false;
            }
        }

        private static Color ParseKmlColor(string kmlHex)
        {
            try
            {
                // KML format: aabbggrr
                byte b = Convert.ToByte(kmlHex.Substring(2, 2), 16);
                byte g = Convert.ToByte(kmlHex.Substring(4, 2), 16);
                byte r = Convert.ToByte(kmlHex.Substring(6, 2), 16);
                return Color.FromRgb(r, g, b);
            }
            catch
            {
                return Color.FromColorIndex(ColorMethod.ByAci, 7);
            }
        }

        private static string CleanLayerName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "GOOGLE_EARTH";
            char[] invalidChars = { '<', '>', '/', '\\', ':', ';', '?', '*', '|', '=', '`' };
            string clean = new string(raw.Where(c => !invalidChars.Contains(c)).ToArray()).Trim();
            return string.IsNullOrEmpty(clean) ? "GOOGLE_EARTH" : clean;
        }

        private static void EnsureLayerExists(LayerTable lt, Transaction tr, string layerName)
        {
            if (!lt.Has(layerName))
            {
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, 7),
                    IsOff = false,
                    IsFrozen = false,
                    IsLocked = false
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }
    }
}
