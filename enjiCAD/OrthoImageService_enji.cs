using System;
using System.IO;
using System.Globalization;
using System.Windows.Forms;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Colors;
using CadApp = Gssoft.Gscad.ApplicationServices.Application;

namespace AVMap.Enji
{
    public static class OrthoImageService
    {
        public const string OrthoLayer = "HINH_BAY_CHUP";

        public static void InsertOrthoImage(Document doc)
        {
            if (doc == null) return;
            Editor ed = doc.Editor;

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Chọn ảnh trực giao bay chụp UAV / Flycam / Vệ tinh (enjiCAD)";
                ofd.Filter = "Ảnh Bay Chụp / Trực Giao (*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.ecw)|*.tif;*.tiff;*.png;*.jpg;*.jpeg;*.ecw|Tất cả định dạng (*.*)|*.*";
                ofd.Multiselect = true;

                if (ofd.ShowDialog() != DialogResult.OK) return;

                int successCount = 0;
                foreach (string imagePath in ofd.FileNames)
                {
                    if (InsertSingleOrtho(doc, imagePath))
                    {
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    ed.UpdateScreen();
                    ed.WriteMessage($"\n[AVMap enjiCAD] Đã chèn thành công {successCount} ảnh bay chụp vào bản vẽ (Layer: {OrthoLayer})!\n");
                }
            }
        }

        private static bool InsertSingleOrtho(Document doc, string imagePath)
        {
            Editor ed = doc.Editor;
            Database db = doc.Database;

            if (!File.Exists(imagePath))
            {
                ed.WriteMessage($"\n[AVMap enjiCAD] File không tồn tại: {imagePath}");
                return false;
            }

            double originX, originY, totalWidth, totalHeight;
            if (!TryGetCoordinates(imagePath, out originX, out originY, out totalWidth, out totalHeight))
            {
                string worldFile = PromptForWorldFile(imagePath);
                if (string.IsNullOrEmpty(worldFile) || !ParseWorldFile(imagePath, worldFile, out originX, out originY, out totalWidth, out totalHeight))
                {
                    ed.WriteMessage($"\n[AVMap enjiCAD Lỗi] Không tìm thấy file tọa độ thế giới (.tfw, .pgw, .jgw, .wld) cho ảnh: {Path.GetFileName(imagePath)}");
                    return false;
                }
            }

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 1. Tạo layer HINH_BAY_CHUP nếu chưa có
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (!lt.Has(OrthoLayer))
                    {
                        lt.UpgradeOpen();
                        LayerTableRecord ltr = new LayerTableRecord
                        {
                            Name = OrthoLayer,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, 7),
                            IsOff = false,
                            IsFrozen = false,
                            IsLocked = false
                        };
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }
                    else
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[OrthoLayer], OpenMode.ForWrite);
                        ltr.IsOff = false;
                        ltr.IsFrozen = false;
                    }

                    // 2. Image Dictionary
                    ObjectId imgDictId = RasterImageDef.GetImageDictionary(db);
                    if (imgDictId.IsNull)
                    {
                        imgDictId = RasterImageDef.CreateImageDictionary(db);
                    }

                    DBDictionary imgDict = (DBDictionary)tr.GetObject(imgDictId, OpenMode.ForWrite);
                    string defName = Path.GetFileNameWithoutExtension(imagePath) + "_" + DateTime.Now.Ticks;

                    RasterImageDef imgDef = new RasterImageDef();
                    imgDef.SourceFileName = imagePath;
                    imgDef.Load();

                    ObjectId defId = imgDict.SetAt(defName, imgDef);
                    tr.AddNewlyCreatedDBObject(imgDef, true);

                    // 3. RasterImage Entity
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    RasterImage img = new RasterImage();
                    img.ImageDefId = defId;
                    img.Layer = OrthoLayer;

                    ObjectId imgId = btr.AppendEntity(img);
                    tr.AddNewlyCreatedDBObject(img, true);

                    RasterImage.EnableReactors(true);
                    img.AssociateRasterDef(imgDef);

                    Vector3d uVector = new Vector3d(totalWidth, 0, 0);
                    Vector3d vVector = new Vector3d(0, totalHeight, 0);
                    img.Orientation = new CoordinateSystem3d(new Point3d(originX, originY, 0), uVector, vVector);
                    img.ShowImage = true;
                    img.SetClipBoundaryToWholeImage();

                    // Đẩy ảnh xuống dưới cùng để nét vẽ CAD hiển thị rõ
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

                try { ed.Regen(); } catch { }
                ed.UpdateScreen();
                ed.WriteMessage($"\n[AVMap enjiCAD] Đã chèn ảnh: {Path.GetFileName(imagePath)}");
                ed.WriteMessage($"\n        Tọa độ gốc: X={originX:F2}, Y={originY:F2} | Rộng: {totalWidth:F2}m x Cao: {totalHeight:F2}m");
                return true;
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[AVMap enjiCAD Lỗi Chèn Ảnh]: {ex.Message}");
                return false;
            }
        }

        private static bool TryGetCoordinates(string imagePath, out double originX, out double originY, out double totalWidth, out double totalHeight)
        {
            originX = 0; originY = 0; totalWidth = 0; totalHeight = 0;
            string dir = Path.GetDirectoryName(imagePath);
            string baseName = Path.GetFileNameWithoutExtension(imagePath);
            string ext = Path.GetExtension(imagePath).ToLower();

            string[] searchExts;
            if (ext == ".tif" || ext == ".tiff") searchExts = new[] { ".tfw", ".tifw", ".wld" };
            else if (ext == ".png") searchExts = new[] { ".pgw", ".pngw", ".wld" };
            else if (ext == ".jpg" || ext == ".jpeg") searchExts = new[] { ".jgw", ".jpgw", ".wld" };
            else searchExts = new[] { ".wld", ".tfw", ".pgw", ".jgw" };

            foreach (var e in searchExts)
            {
                string candidate = Path.Combine(dir, baseName + e);
                if (File.Exists(candidate))
                {
                    return ParseWorldFile(imagePath, candidate, out originX, out originY, out totalWidth, out totalHeight);
                }
                candidate = Path.Combine(dir, baseName + e.ToUpper());
                if (File.Exists(candidate))
                {
                    return ParseWorldFile(imagePath, candidate, out originX, out originY, out totalWidth, out totalHeight);
                }
            }

            if (ext == ".tif" || ext == ".tiff")
            {
                if (TryReadGeoTiff(imagePath, out originX, out originY, out totalWidth, out totalHeight))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ParseWorldFile(string imagePath, string worldFilePath, out double originX, out double originY, out double totalWidth, out double totalHeight)
        {
            originX = 0; originY = 0; totalWidth = 0; totalHeight = 0;
            try
            {
                string[] lines = File.ReadAllLines(worldFilePath);
                if (lines.Length < 6) return false;

                double A = double.Parse(lines[0].Trim(), CultureInfo.InvariantCulture);
                double D = double.Parse(lines[1].Trim(), CultureInfo.InvariantCulture);
                double B = double.Parse(lines[2].Trim(), CultureInfo.InvariantCulture);
                double E = double.Parse(lines[3].Trim(), CultureInfo.InvariantCulture);
                double C = double.Parse(lines[4].Trim(), CultureInfo.InvariantCulture);
                double F = double.Parse(lines[5].Trim(), CultureInfo.InvariantCulture);

                int imgW = 0, imgH = 0;
                using (var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var img = System.Drawing.Image.FromStream(fs, false, false))
                {
                    imgW = img.Width;
                    imgH = img.Height;
                }

                double pixelSizeX = Math.Abs(A);
                double pixelSizeY = Math.Abs(E);

                totalWidth = pixelSizeX * imgW;
                totalHeight = pixelSizeY * imgH;

                originX = C;
                originY = F - totalHeight;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadGeoTiff(string imagePath, out double originX, out double originY, out double totalWidth, out double totalHeight)
        {
            originX = 0; originY = 0; totalWidth = 0; totalHeight = 0;
            try
            {
                using (var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var reader = new BinaryReader(fs))
                    {
                        byte[] byteOrder = reader.ReadBytes(2);
                        bool isLittleEndian = (byteOrder[0] == 'I' && byteOrder[1] == 'I');
                        ushort magic = ReadUInt16(reader, isLittleEndian);
                        if (magic != 42) return false;

                        uint ifdOffset = ReadUInt32(reader, isLittleEndian);
                        fs.Seek(ifdOffset, SeekOrigin.Begin);

                        ushort numEntries = ReadUInt16(reader, isLittleEndian);
                        double scaleX = 0, scaleY = 0;
                        double tieX = 0, tieY = 0;
                        bool hasScale = false, hasTie = false;

                        for (int i = 0; i < numEntries; i++)
                        {
                            ushort tag = ReadUInt16(reader, isLittleEndian);
                            ushort type = ReadUInt16(reader, isLittleEndian);
                            uint count = ReadUInt32(reader, isLittleEndian);
                            uint valueOrOffset = ReadUInt32(reader, isLittleEndian);

                            if (tag == 33550) // ModelPixelScaleTag
                            {
                                long curPos = fs.Position;
                                fs.Seek(valueOrOffset, SeekOrigin.Begin);
                                scaleX = ReadDouble(reader, isLittleEndian);
                                scaleY = ReadDouble(reader, isLittleEndian);
                                hasScale = true;
                                fs.Seek(curPos, SeekOrigin.Begin);
                            }
                            else if (tag == 33922) // ModelTiepointTag
                            {
                                long curPos = fs.Position;
                                fs.Seek(valueOrOffset, SeekOrigin.Begin);
                                double iPix = ReadDouble(reader, isLittleEndian);
                                double jPix = ReadDouble(reader, isLittleEndian);
                                double kPix = ReadDouble(reader, isLittleEndian);
                                tieX = ReadDouble(reader, isLittleEndian);
                                tieY = ReadDouble(reader, isLittleEndian);
                                hasTie = true;
                                fs.Seek(curPos, SeekOrigin.Begin);
                            }
                        }

                        if (hasScale && hasTie)
                        {
                            int imgW = 0, imgH = 0;
                            using (var img = System.Drawing.Image.FromStream(fs, false, false))
                            {
                                imgW = img.Width;
                                imgH = img.Height;
                            }

                            totalWidth = scaleX * imgW;
                            totalHeight = scaleY * imgH;
                            originX = tieX;
                            originY = tieY - totalHeight;
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static string PromptForWorldFile(string imagePath)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = $"Chọn file tọa độ (.tfw, .pgw, .jgw, .wld) cho ảnh {Path.GetFileName(imagePath)}";
                ofd.Filter = "File tọa độ (*.tfw;*.pgw;*.jgw;*.wld)|*.tfw;*.pgw;*.jgw;*.wld|Tất cả định dạng (*.*)|*.*";
                ofd.InitialDirectory = Path.GetDirectoryName(imagePath);
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    return ofd.FileName;
                }
            }
            return null;
        }

        private static ushort ReadUInt16(BinaryReader r, bool le)
        {
            byte[] b = r.ReadBytes(2);
            if (!le) Array.Reverse(b);
            return BitConverter.ToUInt16(b, 0);
        }

        private static uint ReadUInt32(BinaryReader r, bool le)
        {
            byte[] b = r.ReadBytes(4);
            if (!le) Array.Reverse(b);
            return BitConverter.ToUInt32(b, 0);
        }

        private static double ReadDouble(BinaryReader r, bool le)
        {
            byte[] b = r.ReadBytes(8);
            if (!le) Array.Reverse(b);
            return BitConverter.ToDouble(b, 0);
        }
    }
}
