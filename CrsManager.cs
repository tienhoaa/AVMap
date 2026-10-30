using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace AVMap
{
    public static class CrsManager
    {
        private const string NodKey = "AVMAP_CRS_CONFIG";
        private static ProvinceCrs _memoryCrs = null;

        public static ProvinceCrs GetActiveCrs(Document doc = null)
        {
            if (doc == null) doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return _memoryCrs;

            try
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DBDictionary nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                    if (nod.Contains(NodKey))
                    {
                        ObjectId xrecId = nod.GetAt(NodKey);
                        Xrecord xrec = (Xrecord)tr.GetObject(xrecId, OpenMode.ForRead);
                        ResultBuffer rb = xrec.Data;
                        if (rb != null)
                        {
                            TypedValue[] values = rb.AsArray();
                            if (values.Length >= 4)
                            {
                                int epsg = (int)values[0].Value;
                                string name = (string)values[1].Value;
                                double cm = (double)values[2].Value;
                                double sf = (double)values[3].Value;

                                ProvinceCrs docCrs = new ProvinceCrs
                                {
                                    EpsgCode = epsg,
                                    Name = name,
                                    CentralMeridian = cm,
                                    ScaleFactor = sf
                                };
                                return docCrs;
                            }
                        }
                    }
                }
            }
            catch { }

            // Nếu bản vẽ này chưa từng lưu CRS trong file DWG,
            // nhưng trong phiên làm việc hiện tại người dùng đã chọn hệ tọa độ (_memoryCrs):
            // Tự động kế thừa và lưu luôn vào file DWG này để người dùng không phải chọn lại từng file!
            if (_memoryCrs != null)
            {
                try { SetActiveCrs(doc, _memoryCrs); } catch { }
                return _memoryCrs;
            }

            return null;
        }

        public static void SetActiveCrs(Document doc, ProvinceCrs crs)
        {
            if (crs == null) return;
            _memoryCrs = crs;
            try { VN2000Catalog.SaveToRecent(crs); } catch { }
            if (doc == null) doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DBDictionary nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
                    Xrecord xrec;
                    if (nod.Contains(NodKey))
                    {
                        xrec = (Xrecord)tr.GetObject(nod.GetAt(NodKey), OpenMode.ForWrite);
                    }
                    else
                    {
                        xrec = new Xrecord();
                        nod.SetAt(NodKey, xrec);
                        tr.AddNewlyCreatedDBObject(xrec, true);
                    }

                    xrec.Data = new ResultBuffer(
                        new TypedValue((int)DxfCode.Int32, crs.EpsgCode),
                        new TypedValue((int)DxfCode.Text, crs.Name),
                        new TypedValue((int)DxfCode.Real, crs.CentralMeridian),
                        new TypedValue((int)DxfCode.Real, crs.ScaleFactor)
                    );

                    tr.Commit();
                }
                doc.Editor.WriteMessage($"\n[AVMap] Đã thiết lập Hệ tọa độ: {crs.Name} - Kinh tuyến trục {crs.CentralMeridian}°.");
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage($"\n[AVMap] Lỗi lưu hệ tọa độ: {ex.Message}");
            }
        }

        public static ProvinceCrs EnsureCrsSelected(Document doc)
        {
            var crs = GetActiveCrs(doc);
            if (crs != null) return crs;

            // Chưa chọn hệ tọa độ -> hiển thị hộp thoại chọn
            return ShowCrsSelectionDialog(doc);
        }

        public static ProvinceCrs ShowCrsSelectionDialog(Document doc)
        {
            var currentCrs = GetActiveCrs(doc);
            var dialog = new CrsWindow(currentCrs);
            Autodesk.AutoCAD.ApplicationServices.Application.ShowModalWindow(dialog);
            if (dialog.DialogResult == true && dialog.SelectedCrs != null)
            {
                SetActiveCrs(doc, dialog.SelectedCrs);
                return dialog.SelectedCrs;
            }
            return null;
        }
    }
}
