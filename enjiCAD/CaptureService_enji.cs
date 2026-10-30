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
    public static class CaptureService
    {
        public static void CaptureCurrentView(Document doc)
        {
            if (doc == null) return;
            Editor ed = doc.Editor;

            ProvinceCrs crs = CrsManager.EnsureCrsSelected(doc);
            if (crs == null)
            {
                ed.WriteMessage("\n[AVMap enjiCAD] Chưa chọn Hệ tọa độ VN-2000! Không thể chụp ảnh.");
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

            CaptureAreaSync(doc, minX, maxX, minY, maxY, crs, "BinhDo_KhungNhin");
        }

        public static void CaptureWindowArea(Document doc)
        {
            if (doc == null) return;
            Editor ed = doc.Editor;

            ProvinceCrs crs = CrsManager.EnsureCrsSelected(doc);
            if (crs == null)
            {
                ed.WriteMessage("\n[AVMap enjiCAD] Bắt buộc phải chọn Hệ tọa độ VN-2000 trước khi làm bình đồ vị trí.");
                return;
            }

            PromptPointOptions ppo1 = new PromptPointOptions("\n[AVMap enjiCAD - Bình Đồ Vị Trí] Chọn góc thứ nhất của vùng cần chụp: ");
            PromptPointResult ppr1 = ed.GetPoint(ppo1);
            if (ppr1.Status != PromptStatus.OK) return;

            PromptCornerOptions pco = new PromptCornerOptions("\n[AVMap enjiCAD - Bình Đồ Vị Trí] Chọn góc đối diện bao quanh khu đất / tuyến đường: ", ppr1.Value);
            PromptPointResult ppr2 = ed.GetCorner(pco);
            if (ppr2.Status != PromptStatus.OK) return;

            Point3d pt1 = ppr1.Value;
            Point3d pt2 = ppr2.Value;

            double minX = Math.Min(pt1.X, pt2.X);
            double maxX = Math.Max(pt1.X, pt2.X);
            double minY = Math.Min(pt1.Y, pt2.Y);
            double maxY = Math.Max(pt1.Y, pt2.Y);

            CaptureAreaSync(doc, minX, maxX, minY, maxY, crs, "BinhDo_ViTri");
        }

        private static void CaptureAreaSync(Document doc, double minX, double maxX, double minY, double maxY, ProvinceCrs crs, string filePrefix)
        {
            Editor ed = doc.Editor;
            double unitScale = (Math.Abs(minX) > 10000000 || Math.Abs(minY) > 10000000) ? 0.001 : 1.0;
            double minX_m = minX * unitScale;
            double maxX_m = maxX * unitScale;
            double minY_m = minY * unitScale;
            double maxY_m = maxY * unitScale;

            double widthMeters = maxX_m - minX_m;
            double heightMeters = maxY_m - minY_m;

            if (widthMeters <= 5 || heightMeters <= 5)
            {
                ed.WriteMessage("\n[AVMap enjiCAD] Vùng chọn quá nhỏ (dưới 5m).");
                return;
            }

            ed.WriteMessage($"\n[AVMap enjiCAD] Đang khởi tạo bình đồ: Rộng {widthMeters:F0}m x Cao {heightMeters:F0}m...");

            // Tính mức zoom tối ưu dựa trên chiều lớn nhất (tránh ảnh quá tải nếu chọn tuyến đường dài)
            double maxDimMeters = Math.Max(widthMeters, heightMeters);
            int zoom = CalculateOptimalZoom(maxDimMeters);

            var ptSW = VN2000Transform.VN2000ToWGS84(minX_m, minY_m, crs.CentralMeridian, crs.ScaleFactor);
            var ptNE = VN2000Transform.VN2000ToWGS84(maxX_m, maxY_m, crs.CentralMeridian, crs.ScaleFactor);

            var (tileMinX, tileMaxY) = VN2000Transform.LatLonToTile(ptSW.Latitude, ptSW.Longitude, zoom);
            var (tileMaxX, tileMinY) = VN2000Transform.LatLonToTile(ptNE.Latitude, ptNE.Longitude, zoom);

            int minTX = Math.Min(tileMinX, tileMaxX);
            int maxTX = Math.Max(tileMinX, tileMaxX);
            int minTY = Math.Min(tileMinY, tileMaxY);
            int maxTY = Math.Max(tileMinY, tileMaxY);

            // Đảm bảo không vượt quá 20 mảnh mỗi chiều và tổng không quá 49 mảnh để GoogleTileClient ghép ảnh an toàn
            while (((maxTX - minTX + 1) * (maxTY - minTY + 1) > 49 || (maxTX - minTX + 1) > 20 || (maxTY - minTY + 1) > 20) && zoom > 4)
            {
                zoom--;
                var t1 = VN2000Transform.LatLonToTile(ptSW.Latitude, ptSW.Longitude, zoom);
                var t2 = VN2000Transform.LatLonToTile(ptNE.Latitude, ptNE.Longitude, zoom);
                minTX = Math.Min(t1.tileX, t2.tileX);
                maxTX = Math.Max(t1.tileX, t2.tileX);
                minTY = Math.Min(t1.tileY, t2.tileY);
                maxTY = Math.Max(t1.tileY, t2.tileY);
            }

            int totalTiles = (maxTX - minTX + 1) * (maxTY - minTY + 1);
            ed.WriteMessage($"\n[AVMap enjiCAD] Đang tải {totalTiles} mảnh ảnh vệ tinh (Độ nét Zoom {zoom})...");

            var nwGeo = VN2000Transform.TileToLatLon(minTX, minTY, zoom);
            var seGeo = VN2000Transform.TileToLatLon(maxTX + 1, maxTY + 1, zoom);

            var nwCad = VN2000Transform.WGS84ToVN2000(nwGeo.Latitude, nwGeo.Longitude, crs.CentralMeridian, crs.ScaleFactor);
            var seCad = VN2000Transform.WGS84ToVN2000(seGeo.Latitude, seGeo.Longitude, crs.CentralMeridian, crs.ScaleFactor);

            string mapType = GoogleTileClient.ActiveMapType;
            if (string.IsNullOrEmpty(mapType) || mapType.Contains("svv"))
            {
                mapType = "y"; // Mặc định ảnh vệ tinh Hybrid có nhãn địa danh/đường bộ
            }

            string imageFile = null;
            try
            {
                imageFile = Task.Run(() => GoogleTileClient.StitchTilesToFileAsync(minTX, maxTX, minTY, maxTY, zoom, mapType)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[AVMap enjiCAD] Lỗi khi tải ảnh: {ex.Message}");
                return;
            }

            if (string.IsNullOrEmpty(imageFile) || !File.Exists(imageFile))
            {
                ed.WriteMessage("\n[AVMap enjiCAD] Không thể tải dữ liệu ảnh từ Google. Vui lòng kiểm tra lại kết nối mạng.");
                return;
            }

            // Lưu file ảnh vào thư mục an toàn
            string dwgDir = null;
            try
            {
                if (!string.IsNullOrEmpty(doc.Database?.Filename))
                {
                    dwgDir = Path.GetDirectoryName(doc.Database.Filename);
                }
            }
            catch { }

            if (string.IsNullOrEmpty(dwgDir) || !Directory.Exists(dwgDir))
            {
                dwgDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AVMap_BinhDo");
            }
            else
            {
                dwgDir = Path.Combine(dwgDir, "AVMap_BinhDo");
            }

            try
            {
                if (!Directory.Exists(dwgDir)) Directory.CreateDirectory(dwgDir);
            }
            catch
            {
                dwgDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AVMap_BinhDo");
                if (!Directory.Exists(dwgDir)) Directory.CreateDirectory(dwgDir);
            }

            string ext = Path.GetExtension(imageFile);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            string finalImgPath = Path.Combine(dwgDir, $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
            File.Copy(imageFile, finalImgPath, true);

            double originX_m = Math.Min(nwCad.X, seCad.X);
            double originY_m = Math.Min(nwCad.Y, seCad.Y);
            double widthM = Math.Abs(seCad.X - nwCad.X);
            double heightM = Math.Abs(nwCad.Y - seCad.Y);

            // Xuất World File Georeference (.pgw / .jgw) chuẩn GIS
            CreateWorldFile(finalImgPath, originX_m, originY_m + heightM, widthM, heightM);

            // Tọa độ CAD thực tế (theo đơn vị bản vẽ mm hoặc m)
            double finalOriginX = originX_m / unitScale;
            double finalOriginY = originY_m / unitScale;
            double cadWidth = widthM / unitScale;
            double cadHeight = heightM / unitScale;

            InsertPermanentRasterImage(doc, finalImgPath, finalOriginX, finalOriginY, cadWidth, cadHeight);

            ed.WriteMessage($"\n[AVMap enjiCAD] ✔ HOÀN TẤT BÌNH ĐỒ VỊ TRÍ!");
            ed.WriteMessage($"\n• Tọa độ gốc Tây Nam: X={finalOriginX:F2}, Y={finalOriginY:F2}");
            ed.WriteMessage($"\n• Kích thước: Rộng {widthM:F1}m x Cao {heightM:F1}m");
            ed.WriteMessage($"\n• File ảnh đã lưu tại: {finalImgPath}");
            ed.WriteMessage($"\n• Ảnh đã được chèn vào ModelSpace (Layer: AVMAP_BINH_DO).\n");
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

        private static void InsertPermanentRasterImage(Document doc, string imgPath, double originX, double originY, double cadWidth, double cadHeight)
        {
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
                        Color = Color.FromColorIndex(ColorMethod.ByAci, 7),
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
                try { img.AssociateRasterDef(imgDef); } catch { }

                Vector3d uVector = new Vector3d(cadWidth, 0, 0);
                Vector3d vVector = new Vector3d(0, cadHeight, 0);
                Point3d originPoint = new Point3d(originX, originY, 0);

                img.Orientation = new CoordinateSystem3d(originPoint, uVector, vVector);
                img.ShowImage = true;
                img.ImageTransparency = false;
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

            try { doc.Editor.UpdateScreen(); } catch { }
        }

        private static int CalculateOptimalZoom(double maxDimMeters)
        {
            if (maxDimMeters > 30000) return 12;
            if (maxDimMeters > 15000) return 14;
            if (maxDimMeters > 6000) return 15;
            if (maxDimMeters > 2500) return 16;
            if (maxDimMeters > 1000) return 17;
            if (maxDimMeters > 400) return 18;
            return 19;
        }
    }
}
