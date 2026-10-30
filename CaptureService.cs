using System;
using System.IO;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AVMap
{
    public class CaptureResult
    {
        public string ImagePath { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
        public double WidthMeters { get; set; }
        public double HeightMeters { get; set; }
    }

    public static class CaptureService
    {
        /// <summary>
        /// Mở hộp thoại UI hiện đại Quét Vùng Bình Đồ Vệ Tinh
        /// </summary>
        public static void OpenCaptureBoxDialog(Document doc)
        {
            if (doc == null) return;
            ProvinceCrs crs = CrsManager.EnsureCrsSelected(doc);
            if (crs == null)
            {
                doc.Editor.WriteMessage("\n[AVMap] Bắt buộc phải chọn Hệ tọa độ VN-2000 trước khi quét bình đồ vị trí.");
                return;
            }

            var win = new CaptureBoxWindow(doc, crs);
            CadApp.ShowModalWindow(win);
        }

        /// <summary>
        /// Chụp theo khung nhìn màn hình hiện tại (Synchronous Command)
        /// </summary>
        public static void CaptureCurrentView(Document doc)
        {
            if (doc == null) return;
            Editor ed = doc.Editor;

            ProvinceCrs crs = CrsManager.EnsureCrsSelected(doc);
            if (crs == null)
            {
                ed.WriteMessage("\n[AVMap] Chưa chọn Hệ tọa độ VN-2000! Không thể chụp ảnh.");
                return;
            }

            double cx = 0, cy = 0;
            double viewHeight = 0;
            double screenW = 1920, screenH = 1080;

            try
            {
                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    cx = view.CenterPoint.X;
                    cy = view.CenterPoint.Y;
                    viewHeight = view.Height;
                    if (view.Width > 0)
                    {
                        screenW = view.Width;
                        screenH = view.Height;
                    }
                }
            }
            catch
            {
                object vctr = CadApp.GetSystemVariable("VIEWCTR");
                if (vctr is Point3d p3) { cx = p3.X; cy = p3.Y; }
                else if (vctr is Point2d p2) { cx = p2.X; cy = p2.Y; }
                viewHeight = (double)CadApp.GetSystemVariable("VIEWSIZE");

                object scr = CadApp.GetSystemVariable("SCREENSIZE");
                if (scr is Point2d sp) { screenW = sp.X; screenH = sp.Y; }
            }

            if (viewHeight <= 0) return;

            double viewWidth = viewHeight * (screenW / screenH);
            double minX = cx - viewWidth / 2.0;
            double maxX = cx + viewWidth / 2.0;
            double minY = cy - viewHeight / 2.0;
            double maxY = cy + viewHeight / 2.0;

            string saveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AVMap_BinhDo");
            var result = ProcessCaptureArea(minX, maxX, minY, maxY, crs, GoogleTileClient.ActiveMapType, 64, 1, saveDir, true);
            if (result != null)
            {
                InsertCapturedImageToCad(doc, result.ImagePath, result.OriginX, result.OriginY, result.WidthMeters, result.HeightMeters);
            }
        }

        /// <summary>
        /// Xử lý tải và ghép ảnh bình đồ từ vệ tinh (Chạy trên luồng nền an toàn)
        /// </summary>
        public static CaptureResult ProcessCaptureArea(
            double minX, double maxX, double minY, double maxY,
            ProvinceCrs crs, string mapType, int maxTiles, int zoomOffset,
            string saveDir, bool exportWorldFile)
        {
            double widthMeters = maxX - minX;
            double heightMeters = maxY - minY;

            int zoom = Math.Max(5, Math.Min(20, CalculateOptimalZoom(heightMeters) + zoomOffset));

            var ptSW = VN2000Transform.VN2000ToWGS84(minX, minY, crs.CentralMeridian, crs.ScaleFactor);
            var ptNE = VN2000Transform.VN2000ToWGS84(maxX, maxY, crs.CentralMeridian, crs.ScaleFactor);

            var (tileMinX, tileMaxY) = VN2000Transform.LatLonToTile(ptSW.Latitude, ptSW.Longitude, zoom);
            var (tileMaxX, tileMinY) = VN2000Transform.LatLonToTile(ptNE.Latitude, ptNE.Longitude, zoom);

            int minTX = Math.Min(tileMinX, tileMaxX);
            int maxTX = Math.Max(tileMinX, tileMaxX);
            int minTY = Math.Min(tileMinY, tileMaxY);
            int maxTY = Math.Max(tileMinY, tileMaxY);

            while ((maxTX - minTX + 1) * (maxTY - minTY + 1) > maxTiles && zoom > 6)
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

            string stitchedTemp = GoogleTileClient.StitchTilesToFileAsync(minTX, maxTX, minTY, maxTY, zoom, mapType).GetAwaiter().GetResult();
            if (string.IsNullOrEmpty(stitchedTemp) || !File.Exists(stitchedTemp))
            {
                return null;
            }

            if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);

            string ext = Path.GetExtension(stitchedTemp);
            string finalImgPath = Path.Combine(saveDir, $"BinhDo_VN2000_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
            File.Copy(stitchedTemp, finalImgPath, true);

            double finalOriginX = nwCad.X;
            double finalOriginY = seCad.Y;
            double finalWidth = seCad.X - nwCad.X;
            double finalHeight = nwCad.Y - seCad.Y;

            if (exportWorldFile)
            {
                CreateWorldFile(finalImgPath, finalOriginX, finalOriginY + finalHeight, finalWidth, finalHeight);
            }

            return new CaptureResult
            {
                ImagePath = finalImgPath,
                OriginX = finalOriginX,
                OriginY = finalOriginY,
                WidthMeters = finalWidth,
                HeightMeters = finalHeight
            };
        }

        public static void CreateWorldFile(string imagePath, double topLeftX, double topLeftY, double widthMeters, double heightMeters)
        {
            try
            {
                using (var bmp = new System.Drawing.Bitmap(imagePath))
                {
                    double pixelSizeX = widthMeters / bmp.Width;
                    double pixelSizeY = -heightMeters / bmp.Height;

                    string ext = Path.GetExtension(imagePath).ToLower();
                    string worldExt = (ext == ".jpg" || ext == ".jpeg") ? ".jgw" : ((ext == ".tif" || ext == ".tiff") ? ".tfw" : ".pgw");
                    string worldFilePath = Path.ChangeExtension(imagePath, worldExt);

                    string[] lines = new string[]
                    {
                        pixelSizeX.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                        "0.000000",
                        "0.000000",
                        pixelSizeY.ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
                        topLeftX.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                        topLeftY.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                    };
                    File.WriteAllLines(worldFilePath, lines);
                }
            }
            catch { }
        }

        public static void InsertCapturedImageToCad(Document doc, string imgPath, double originX, double originY, double widthMeters, double heightMeters)
        {
            if (doc == null) return;
            Database db = doc.Database;
            const string layerName = "AVMAP_BINH_DO";

            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layerName))
                {
                    lt.UpgradeOpen();
                    LayerTableRecord ltr = new LayerTableRecord
                    {
                        Name = layerName,
                        IsLocked = false,
                        IsPlottable = true
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

                ObjectId imgDictId = RasterImageDef.GetImageDictionary(db);
                if (imgDictId.IsNull)
                {
                    imgDictId = RasterImageDef.CreateImageDictionary(db);
                }

                DBDictionary imgDict = (DBDictionary)tr.GetObject(imgDictId, OpenMode.ForWrite);
                string defName = Path.GetFileNameWithoutExtension(imgPath) + "_" + DateTime.Now.Ticks;

                RasterImageDef imgDef = new RasterImageDef();
                imgDef.SourceFileName = imgPath;
                imgDef.Load();

                ObjectId defId = imgDict.SetAt(defName, imgDef);
                tr.AddNewlyCreatedDBObject(imgDef, true);

                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                RasterImage img = new RasterImage();
                img.ImageDefId = defId;
                img.Layer = layerName;

                ObjectId imgId = btr.AppendEntity(img);
                tr.AddNewlyCreatedDBObject(img, true);

                RasterImage.EnableReactors(true);
                img.AssociateRasterDef(imgDef);

                Vector3d uVector = new Vector3d(widthMeters, 0, 0);
                Vector3d vVector = new Vector3d(0, heightMeters, 0);
                img.Orientation = new CoordinateSystem3d(new Point3d(originX, originY, 0), uVector, vVector);
                img.ShowImage = true;
                img.SetClipBoundaryToWholeImage();

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
            try { doc.Editor?.Regen(); } catch { }
            doc.Editor?.UpdateScreen();
        }

        private static int CalculateOptimalZoom(double heightMeters)
        {
            if (heightMeters > 30000) return 12;
            if (heightMeters > 15000) return 14;
            if (heightMeters > 6000) return 15;
            if (heightMeters > 2500) return 16;
            if (heightMeters > 1000) return 17;
            if (heightMeters > 400) return 18;
            return 19;
        }
    }
}
