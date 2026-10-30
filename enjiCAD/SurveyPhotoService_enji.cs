using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Colors;
using ImageMagick;
using CadApp = Gssoft.Gscad.ApplicationServices.Application;

namespace AVMap.Enji
{
    public class PhotoLayoutItem
    {
        public SurveyPhotoItem Photo { get; set; }
        public double WidthMeters { get; set; }
        public double HeightMeters { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
    }

    public static class SurveyPhotoService
    {
        private const string LayerPhoto = "ANH_KHAO_SAT";
        private const string LayerFrame = "ANH_KHAO_SAT_KHUNG";
        private const string LayerLeader = "ANH_KHAO_SAT_LEADER";
        private const string LayerText = "ANH_KHAO_SAT_TEXT";

        private static readonly string surveyCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVMap", "SurveyCache");

        public static void ClearSurveyCache()
        {
            try
            {
                if (Directory.Exists(surveyCache))
                {
                    foreach (var file in Directory.GetFiles(surveyCache))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        public static long GetSurveyCacheSizeBytes()
        {
            try
            {
                if (!Directory.Exists(surveyCache)) return 0;
                long total = 0;
                foreach (var file in Directory.GetFiles(surveyCache))
                {
                    try { total += new FileInfo(file).Length; } catch { }
                }
                return total;
            }
            catch { return 0; }
        }

        static SurveyPhotoService()
        {
            if (!Directory.Exists(surveyCache))
            {
                Directory.CreateDirectory(surveyCache);
            }
        }

        public static void OpenSurveyPhotoDialog(Document doc)
        {
            if (doc == null) return;
            ProvinceCrs crs = CrsManager.EnsureCrsSelected(doc);
            if (crs == null)
            {
                doc.Editor.WriteMessage("\n[AVMap enjiCAD] Bắt buộc phải chọn Hệ tọa độ VN-2000 trước khi chèn ảnh khảo sát.");
                return;
            }

            var win = new SurveyPhotoWindow_enji(doc, crs);
            win.ShowDialog();
        }

        public static void InsertSurveyPhotosCommand(Document doc)
        {
            OpenSurveyPhotoDialog(doc);
        }

        public static SurveyPhotoItem AnalyzePhoto(string filePath, ProvinceCrs crs)
        {
            if (!File.Exists(filePath)) return null;

            try
            {
                using (var image = new MagickImage(filePath))
                {
                    double lat = 0, lon = 0;
                    DateTime? dateTaken = null;
                    TryExtractGpsFromMagick(image, out lat, out lon, out dateTaken);

                    double vnX = 0, vnY = 0;
                    if (Math.Abs(lat) > 0.001 && Math.Abs(lon) > 0.001 && crs != null)
                    {
                        var pt = VN2000Transform.WGS84ToVN2000(lat, lon, crs.CentralMeridian, crs.ScaleFactor);
                        vnX = pt.X;
                        vnY = pt.Y;
                    }

                    return new SurveyPhotoItem
                    {
                        FilePath = filePath,
                        Dimensions = $"{image.Width} x {image.Height}",
                        Format = image.Format.ToString().ToUpper(),
                        Latitude = lat,
                        Longitude = lon,
                        Vn2000X = vnX,
                        Vn2000Y = vnY,
                        DateTaken = dateTaken,
                        WidthPx = (int)image.Width,
                        HeightPx = (int)image.Height
                    };
                }
            }
            catch
            {
                return new SurveyPhotoItem
                {
                    FilePath = filePath,
                    Dimensions = "N/A",
                    Format = Path.GetExtension(filePath).ToUpper().TrimStart('.'),
                    Latitude = 0,
                    Longitude = 0
                };
            }
        }

        /// <summary>
        /// Thuật toán tính toán vị trí bố trí thông minh tự động chống đè/trùng ảnh (Auto Anti-Overlap Layout)
        /// </summary>
        public static List<PhotoLayoutItem> CalculatePhotoLayout(
            List<SurveyPhotoItem> photos,
            double widthLandscape, double heightPortrait,
            double defaultOffsetX, double defaultOffsetY,
            double spacing,
            bool antiOverlap,
            int layoutMode = 0)
        {
            var result = new List<PhotoLayoutItem>();
            if (photos == null || photos.Count == 0) return result;

            // 1. Khởi tạo kích thước và tọa độ ban đầu
            for (int i = 0; i < photos.Count; i++)
            {
                var photo = photos[i];
                int imgW = photo.WidthPx;
                int imgH = photo.HeightPx;
                double w, h;
                if (imgW >= imgH)
                {
                    w = widthLandscape;
                    h = (imgW > 0) ? widthLandscape * (imgH / (double)imgW) : 10.0;
                }
                else
                {
                    h = heightPortrait;
                    w = (imgH > 0) ? heightPortrait * (imgW / (double)imgH) : 6.0;
                }

                double initX, initY;
                if (!antiOverlap)
                {
                    initX = photo.Vn2000X + defaultOffsetX;
                    initY = photo.Vn2000Y + defaultOffsetY;
                }
                else
                {
                    switch (layoutMode)
                    {
                        case 1: // Dạt sang phải (+X)
                            initX = photo.Vn2000X + Math.Abs(defaultOffsetX);
                            initY = photo.Vn2000Y + (i % 3) * 4.0;
                            break;
                        case 2: // Dạt sang trái (-X)
                            initX = photo.Vn2000X - Math.Abs(defaultOffsetX) - w;
                            initY = photo.Vn2000Y + (i % 3) * 4.0;
                            break;
                        case 3: // Dạt lên trên (+Y)
                            initX = photo.Vn2000X + (i % 3) * 4.0;
                            initY = photo.Vn2000Y + Math.Abs(defaultOffsetY);
                            break;
                        case 4: // Dạt xuống dưới (-Y)
                            initX = photo.Vn2000X + (i % 3) * 4.0;
                            initY = photo.Vn2000Y - Math.Abs(defaultOffsetY) - h;
                            break;
                        default: // 0: Tự động tỏa đều thông minh (Golden ratio radial)
                            double angle = i * 2.399963;
                            double dist = Math.Max(defaultOffsetX, defaultOffsetY) + (i % 4) * spacing * 1.5;
                            initX = photo.Vn2000X + Math.Cos(angle) * dist;
                            initY = photo.Vn2000Y + Math.Sin(angle) * dist;
                            break;
                    }
                }

                result.Add(new PhotoLayoutItem
                {
                    Photo = photo,
                    WidthMeters = w,
                    HeightMeters = h,
                    OriginX = initX,
                    OriginY = initY
                });
            }

            if (!antiOverlap || result.Count <= 1) return result;

            // 2. Thuật toán đẩy lùi tương tác AABB (Force-directed relaxation)
            double labelMargin = 2.0;
            int iterations = 120;

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    var a = result[i];
                    double aCx = a.OriginX + a.WidthMeters / 2.0;
                    double aCy = a.OriginY + a.HeightMeters / 2.0;

                    for (int j = i + 1; j < result.Count; j++)
                    {
                        var b = result[j];
                        double bCx = b.OriginX + b.WidthMeters / 2.0;
                        double bCy = b.OriginY + b.HeightMeters / 2.0;

                        double targetDistX = (a.WidthMeters + b.WidthMeters) / 2.0 + spacing;
                        double targetDistY = (a.HeightMeters + b.HeightMeters) / 2.0 + spacing + labelMargin;

                        double distX = Math.Abs(aCx - bCx);
                        double distY = Math.Abs(aCy - bCy);

                        if (distX < targetDistX && distY < targetDistY)
                        {
                            double overlapX = targetDistX - distX;
                            double overlapY = targetDistY - distY;

                            if (overlapX < overlapY)
                            {
                                double sign = (aCx >= bCx) ? 1.0 : -1.0;
                                double shift = overlapX * 0.55;
                                a.OriginX += sign * shift;
                                b.OriginX -= sign * shift;
                            }
                            else
                            {
                                double sign = (aCy >= bCy) ? 1.0 : -1.0;
                                double shift = overlapY * 0.55;
                                a.OriginY += sign * shift;
                                b.OriginY -= sign * shift;
                            }
                        }
                    }

                    double anchorX = a.Photo.Vn2000X + defaultOffsetX;
                    double anchorY = a.Photo.Vn2000Y + defaultOffsetY;
                    a.OriginX += 0.02 * (anchorX - a.OriginX);
                    a.OriginY += 0.02 * (anchorY - a.OriginY);
                }
            }

            // 3. Đảm bảo 100% không còn ảnh nào bị đè lên nhau
            var placed = new List<PhotoLayoutItem>();
            foreach (var item in result)
            {
                double origX = item.OriginX;
                double origY = item.OriginY;
                double spiralR = 0;
                double angle = 0;

                while (HasOverlap(item, placed, spacing, labelMargin))
                {
                    spiralR += 2.0;
                    angle += 0.7;
                    item.OriginX = origX + Math.Cos(angle) * spiralR;
                    item.OriginY = origY + Math.Sin(angle) * spiralR;
                }
                placed.Add(item);
            }

            return result;
        }

        private static bool HasOverlap(PhotoLayoutItem a, List<PhotoLayoutItem> placed, double spacing, double labelMargin)
        {
            double aLeft = a.OriginX;
            double aRight = a.OriginX + a.WidthMeters + spacing;
            double aBottom = a.OriginY - labelMargin;
            double aTop = a.OriginY + a.HeightMeters + spacing;

            foreach (var b in placed)
            {
                double bLeft = b.OriginX;
                double bRight = b.OriginX + b.WidthMeters + spacing;
                double bBottom = b.OriginY - labelMargin;
                double bTop = b.OriginY + b.HeightMeters + spacing;

                if (!(aRight <= bLeft || bRight <= aLeft || aTop <= bBottom || bTop <= aBottom))
                {
                    return true;
                }
            }
            return false;
        }

        public static Point3d GetClosestPointOnRectPerimeter(Point3d ptTarget, double left, double bottom, double width, double height)
        {
            double right = left + width;
            double top = bottom + height;

            double cx = Math.Max(left, Math.Min(right, ptTarget.X));
            double cy = Math.Max(bottom, Math.Min(top, ptTarget.Y));

            if (ptTarget.X < left) cx = left;
            else if (ptTarget.X > right) cx = right;

            if (ptTarget.Y < bottom) cy = bottom;
            else if (ptTarget.Y > top) cy = top;

            if (cx > left && cx < right && cy > bottom && cy < top)
            {
                double dLeft = cx - left;
                double dRight = right - cx;
                double dBottom = cy - bottom;
                double dTop = top - cy;
                double minD = Math.Min(Math.Min(dLeft, dRight), Math.Min(dBottom, dTop));
                if (minD == dLeft) cx = left;
                else if (minD == dRight) cx = right;
                else if (minD == dBottom) cy = bottom;
                else cy = top;
            }

            return new Point3d(cx, cy, 0);
        }

        public static bool InsertSinglePhoto(
            Document doc, SurveyPhotoItem item, ProvinceCrs crs,
            double widthMeters, double heightMeters,
            double originX, double originY,
            bool drawLeader, bool drawRectang, bool drawLabel, bool autoOrient)
        {
            if (doc == null || item == null || !item.HasGps) return false;

            Database db = doc.Database;
            string displayImagePath = item.FilePath;
            string ext = Path.GetExtension(item.FilePath).ToLower();
            int imgW = item.WidthPx;
            int imgH = item.HeightPx;

            try
            {
                if (ext == ".heic" || ext == ".heif" || autoOrient)
                {
                    using (var image = new MagickImage(item.FilePath))
                    {
                        if (autoOrient)
                        {
                            image.AutoOrient();
                        }
                        imgW = (int)image.Width;
                        imgH = (int)image.Height;

                        string convertedJpg = Path.Combine(surveyCache, $"KS_{Path.GetFileNameWithoutExtension(item.FilePath)}_{DateTime.Now.Ticks}.jpg");
                        image.Write(convertedJpg);
                        displayImagePath = convertedJpg;
                    }
                }
            }
            catch
            {
                displayImagePath = item.FilePath;
            }

            Point3d ptGps = new Point3d(item.Vn2000X, item.Vn2000Y, 0);
            Point3d ptOrigin = new Point3d(originX, originY, 0);

            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                EnsureLayers(db, tr);

                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // --- A. Chèn RasterImage ---
                ObjectId imgDictId = RasterImageDef.GetImageDictionary(db);
                if (imgDictId.IsNull) imgDictId = RasterImageDef.CreateImageDictionary(db);
                DBDictionary imgDict = (DBDictionary)tr.GetObject(imgDictId, OpenMode.ForWrite);

                string defName = "KS_" + Path.GetFileNameWithoutExtension(item.FilePath) + "_" + DateTime.Now.Ticks;
                RasterImageDef imgDef = new RasterImageDef();
                imgDef.SourceFileName = displayImagePath;
                imgDef.Load();

                ObjectId defId = imgDict.SetAt(defName, imgDef);
                tr.AddNewlyCreatedDBObject(imgDef, true);

                RasterImage img = new RasterImage();
                img.ImageDefId = defId;
                img.Layer = LayerPhoto;

                ObjectId imgId = btr.AppendEntity(img);
                tr.AddNewlyCreatedDBObject(img, true);

                RasterImage.EnableReactors(true);
                img.AssociateRasterDef(imgDef);

                Vector3d uVector = new Vector3d(widthMeters, 0, 0);
                Vector3d vVector = new Vector3d(0, heightMeters, 0);
                img.Orientation = new CoordinateSystem3d(ptOrigin, uVector, vVector);
                img.ShowImage = true;
                img.SetClipBoundaryToWholeImage();

                // Đẩy ảnh xuống dưới
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

                // --- B. Vẽ Khung Viền Chữ Nhật ---
                if (drawRectang)
                {
                    Polyline rect = new Polyline(4);
                    rect.AddVertexAt(0, new Point2d(ptOrigin.X, ptOrigin.Y), 0, 0, 0);
                    rect.AddVertexAt(1, new Point2d(ptOrigin.X + widthMeters, ptOrigin.Y), 0, 0, 0);
                    rect.AddVertexAt(2, new Point2d(ptOrigin.X + widthMeters, ptOrigin.Y + heightMeters), 0, 0, 0);
                    rect.AddVertexAt(3, new Point2d(ptOrigin.X, ptOrigin.Y + heightMeters), 0, 0, 0);
                    rect.Closed = true;
                    rect.Layer = LayerFrame;
                    btr.AppendEntity(rect);
                    tr.AddNewlyCreatedDBObject(rect, true);
                }

                // --- C. Vẽ Đường Dẫn Leader Chỉ Đúng Điểm Hư Hỏng ---
                if (drawLeader)
                {
                    Point3d ptAttach = GetClosestPointOnRectPerimeter(ptGps, ptOrigin.X, ptOrigin.Y, widthMeters, heightMeters);

                    Leader leader = new Leader();
                    leader.SetDatabaseDefaults();
                    leader.AppendVertex(ptGps);
                    leader.AppendVertex(ptAttach);
                    leader.HasArrowHead = true;
                    leader.Layer = LayerLeader;
                    btr.AppendEntity(leader);
                    tr.AddNewlyCreatedDBObject(leader, true);
                }

                // --- D. Ghi Chú Tên Ảnh & Thời Gian Chụp ---
                if (drawLabel)
                {
                    string labelText = item.FileName;
                    if (item.DateTaken.HasValue)
                    {
                        labelText += $" ({item.DateTaken.Value:dd/MM/yyyy HH:mm})";
                    }

                    DBText txt = new DBText();
                    txt.SetDatabaseDefaults();
                    txt.TextString = labelText;
                    txt.Height = 0.8;
                    txt.Position = new Point3d(ptOrigin.X, ptOrigin.Y - 1.2, 0);
                    txt.Layer = LayerText;
                    btr.AppendEntity(txt);
                    tr.AddNewlyCreatedDBObject(txt, true);
                }

                tr.Commit();
            }

            return true;
        }

        private static bool TryExtractGpsFromMagick(MagickImage image, out double lat, out double lon, out DateTime? dateTaken)
        {
            lat = 0; lon = 0; dateTaken = null;
            try
            {
                var profile = image.GetExifProfile();
                if (profile == null) return false;

                var valLat = profile.GetValue(ExifTag.GPSLatitude);
                var valLatRef = profile.GetValue(ExifTag.GPSLatitudeRef);
                var valLon = profile.GetValue(ExifTag.GPSLongitude);
                var valLonRef = profile.GetValue(ExifTag.GPSLongitudeRef);

                if (valLat == null || valLon == null) return false;

                lat = ConvertRationalsToDouble(valLat.Value as Rational[]);
                lon = ConvertRationalsToDouble(valLon.Value as Rational[]);

                string latRef = valLatRef?.Value as string;
                if (!string.IsNullOrEmpty(latRef) && latRef.Trim().StartsWith("S", StringComparison.OrdinalIgnoreCase))
                {
                    lat = -lat;
                }

                string lonRef = valLonRef?.Value as string;
                if (!string.IsNullOrEmpty(lonRef) && lonRef.Trim().StartsWith("W", StringComparison.OrdinalIgnoreCase))
                {
                    lon = -lon;
                }

                var valDate = profile.GetValue(ExifTag.DateTimeOriginal);
                if (valDate != null && valDate.Value is string sDate)
                {
                    DateTime dt;
                    if (DateTime.TryParseExact(sDate.Trim(), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    {
                        dateTaken = dt;
                    }
                }

                return (Math.Abs(lat) > 0.001 && Math.Abs(lon) > 0.001);
            }
            catch
            {
                return false;
            }
        }

        private static double ConvertRationalsToDouble(Rational[] rationals)
        {
            if (rationals == null || rationals.Length < 3) return 0;
            double deg = rationals[0].ToDouble();
            double min = rationals[1].ToDouble();
            double sec = rationals[2].ToDouble();
            return deg + (min / 60.0) + (sec / 3600.0);
        }

        private static void EnsureLayers(Database db, Transaction tr)
        {
            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

            CreateOrUpdateLayer(lt, tr, LayerPhoto, Color.FromColorIndex(ColorMethod.ByAci, 7));       // White
            CreateOrUpdateLayer(lt, tr, LayerFrame, Color.FromColorIndex(ColorMethod.ByAci, 7));       // White (Rectang)
            CreateOrUpdateLayer(lt, tr, LayerLeader, Color.FromColorIndex(ColorMethod.ByAci, 2));      // Yellow (Leader)
            CreateOrUpdateLayer(lt, tr, LayerText, Color.FromColorIndex(ColorMethod.ByAci, 3));        // Green (Text)
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
                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[layerName], OpenMode.ForWrite);
                ltr.IsOff = false;
                ltr.IsFrozen = false;
            }
        }
    }
}
