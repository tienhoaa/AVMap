using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AVMap
{
    public class LayerDisplayItem
    {
        public string Name { get; set; }
        public bool IsSelected { get; set; } = true;
        public string ColorHex { get; set; } = "#38BDF8";
        public ObjectId LayerId { get; set; }
    }

    public partial class KmzWindow : Window
    {
        private Document currentDoc;
        private ProvinceCrs activeCrs;
        private ObservableCollection<LayerDisplayItem> layersCollection = new ObservableCollection<LayerDisplayItem>();
        private ObjectIdCollection preselectedIds = new ObjectIdCollection();

        public KmzWindow(Document doc, int initialTab = 0)
        {
            InitializeComponent();
            currentDoc = doc;

            if (tabMain != null && initialTab >= 0 && initialTab < tabMain.Items.Count)
            {
                tabMain.SelectedIndex = initialTab;
            }

            // 1. Lấy hệ tọa độ VN-2000 đang dùng
            activeCrs = CrsManager.GetActiveCrs(doc);
            if (activeCrs == null)
            {
                activeCrs = CrsManager.ShowCrsSelectionDialog(doc);
            }
            UpdateCrsDisplay();

            // 2. Nạp danh sách Layer từ bản vẽ
            LoadDrawingLayers();

            // 3. Đường dẫn xuất mặc định
            string docName = !string.IsNullOrEmpty(doc?.Name) ? Path.GetFileNameWithoutExtension(doc.Name) : "BanVe";
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            txtExportPath.Text = Path.Combine(desktopDir, $"{docName}_GoogleEarth.kmz");
        }

        private void UpdateCrsDisplay()
        {
            if (activeCrs != null)
            {
                lblActiveCrs.Text = $"{activeCrs.Name} (KTT {activeCrs.CentralMeridian}°)";
            }
            else
            {
                lblActiveCrs.Text = "Chưa chọn VN-2000";
            }
        }

        private void LoadDrawingLayers()
        {
            layersCollection.Clear();
            if (currentDoc == null) return;
            Database db = currentDoc.Database;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId lid in lt)
                    {
                        LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lid, OpenMode.ForRead);
                        var col = ltr.Color.ColorValue;
                        string hex = $"#{col.R:X2}{col.G:X2}{col.B:X2}";

                        layersCollection.Add(new LayerDisplayItem
                        {
                            Name = ltr.Name,
                            IsSelected = true,
                            ColorHex = hex,
                            LayerId = lid
                        });
                    }
                    tr.Commit();
                }
            }
            catch { }

            lstLayers.ItemsSource = layersCollection;
            lblLayerCount.Text = $"{layersCollection.Count} layers";
        }

        private void BtnChangeCrs_Click(object sender, RoutedEventArgs e)
        {
            ProvinceCrs newCrs = CrsManager.ShowCrsSelectionDialog(currentDoc);
            if (newCrs != null)
            {
                activeCrs = newCrs;
                UpdateCrsDisplay();
            }
        }

        private void RbScope_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlLayerList == null) return;
            pnlLayerList.Visibility = (rbScopeLayers.IsChecked == true) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void BtnSelectAllLayers_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in layersCollection) item.IsSelected = true;
            lstLayers.Items.Refresh();
        }

        private void BtnDeselectAllLayers_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in layersCollection) item.IsSelected = false;
            lstLayers.Items.Refresh();
        }

        private void BtnPick2Points_Click(object sender, RoutedEventArgs e)
        {
            if (currentDoc == null) return;
            Editor ed = currentDoc.Editor;

            this.Hide();
            try
            {
                using (currentDoc.LockDocument())
                {
                    PromptPointOptions ppo = new PromptPointOptions("\n[AVMap - Xuất Google Earth] Chọn điểm góc thứ 1 trên bản vẽ: ");
                    PromptPointResult ppr1 = ed.GetPoint(ppo);
                    if (ppr1.Status == PromptStatus.OK)
                    {
                        PromptCornerOptions pco = new PromptCornerOptions("\n[AVMap - Xuất Google Earth] Chọn điểm góc thứ 2 đối diện: ", ppr1.Value);
                        PromptPointResult ppr2 = ed.GetCorner(pco);
                        if (ppr2.Status == PromptStatus.OK)
                        {
                            PromptSelectionResult psr = ed.SelectCrossingWindow(ppr1.Value, ppr2.Value);
                            if (psr.Status == PromptStatus.OK && psr.Value != null)
                            {
                                preselectedIds = new ObjectIdCollection(psr.Value.GetObjectIds());
                                rbScopeSelection.Content = $"Theo vùng quét ({preselectedIds.Count} đối tượng)";
                                rbScopeSelection.IsChecked = true;
                            }
                        }
                    }
                }
            }
            finally
            {
                this.ShowDialog();
            }
        }

        private void BtnPickObjects_Click(object sender, RoutedEventArgs e)
        {
            if (currentDoc == null) return;
            Editor ed = currentDoc.Editor;

            this.Hide();
            try
            {
                using (currentDoc.LockDocument())
                {
                    PromptSelectionOptions pso = new PromptSelectionOptions
                    {
                        MessageForAdding = "\n[AVMap - Xuất Google Earth] Quét chọn các đối tượng trên bản vẽ (Line, Polyline, Block, Text... nhấn ENTER để hoàn tất): "
                    };
                    PromptSelectionResult psr = ed.GetSelection(pso);
                    if (psr.Status == PromptStatus.OK && psr.Value != null)
                    {
                        preselectedIds = new ObjectIdCollection(psr.Value.GetObjectIds());
                        rbScopeSelection.Content = $"Theo vùng chọn ({preselectedIds.Count} đối tượng)";
                        rbScopeSelection.IsChecked = true;
                    }
                }
            }
            finally
            {
                this.ShowDialog();
            }
        }

        private void BtnBrowseExport_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog
            {
                Title = "Chọn nơi lưu file Google Earth (KMZ / KML)",
                Filter = "Google Earth KMZ (*.kmz)|*.kmz|Google Earth KML (*.kml)|*.kml",
                FileName = Path.GetFileName(txtExportPath.Text)
            };
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtExportPath.Text = sfd.FileName;
            }
        }

        private void BtnStartExport_Click(object sender, RoutedEventArgs e)
        {
            if (activeCrs == null)
            {
                activeCrs = CrsManager.ShowCrsSelectionDialog(currentDoc);
                if (activeCrs == null) return;
                UpdateCrsDisplay();
            }

            string outPath = txtExportPath.Text.Trim();
            if (string.IsNullOrEmpty(outPath))
            {
                System.Windows.MessageBox.Show("Vui lòng chọn đường dẫn lưu file KMZ / KML.", "AVMap", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Editor ed = currentDoc.Editor;
            Database db = currentDoc.Database;
            var targetIds = new List<ObjectId>();

            // Thu thập danh sách ObjectId cần xuất
            if (rbScopeSelection.IsChecked == true && preselectedIds != null && preselectedIds.Count > 0)
            {
                foreach (ObjectId id in preselectedIds) targetIds.Add(id);
            }
            else if (rbScopeLayers.IsChecked == true)
            {
                var selectedLayerNames = new HashSet<string>(layersCollection.Where(l => l.IsSelected).Select(l => l.Name), StringComparer.OrdinalIgnoreCase);
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (ObjectId id in btr)
                    {
                        if (id.IsValid && !id.IsErased)
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent != null && selectedLayerNames.Contains(ent.Layer))
                            {
                                targetIds.Add(id);
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            else
            {
                // Toàn bộ ModelSpace
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (ObjectId id in btr)
                    {
                        if (id.IsValid && !id.IsErased)
                        {
                            targetIds.Add(id);
                        }
                    }
                    tr.Commit();
                }
            }

            if (targetIds.Count == 0)
            {
                System.Windows.MessageBox.Show("Không tìm thấy đối tượng nào thỏa mãn điều kiện để xuất.", "AVMap", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Đọc độ dày nét viền
            double lineWidth = 2.5;
            if (cboLineWidth.SelectedIndex == 0) lineWidth = 1.5;
            else if (cboLineWidth.SelectedIndex == 1) lineWidth = 2.0;
            else if (cboLineWidth.SelectedIndex == 2) lineWidth = 2.5;
            else if (cboLineWidth.SelectedIndex == 3) lineWidth = 3.0;
            else if (cboLineWidth.SelectedIndex == 4) lineWidth = 4.0;

            var options = new ExportKmzOptions
            {
                GroupByLayer = chkGroupLayers.IsChecked == true,
                PreserveColors = chkPreserveColor.IsChecked == true,
                HatchTransparent = chkHatchTransparent.IsChecked == true,
                AutoBrightenDark = chkAutoBrighten.IsChecked == true,
                LineWidth = lineWidth
            };

            int count = KmzService.ExportToKmzWithOptions(currentDoc, targetIds, outPath, activeCrs, options);

            if (count > 0)
            {
                if (chkAutoOpenEarth.IsChecked == true && File.Exists(outPath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
                    }
                    catch { }
                }

                System.Windows.MessageBox.Show(
                    $"Xuất dữ liệu sang Google Earth thành công!\n\n• Số lượng đối tượng: {count}\n• Hệ tọa độ: {activeCrs.Name}\n• Tệp đã lưu: {outPath}",
                    "AVMap - Xuất Google Earth Thành Công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                this.Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Không có đối tượng hình học nào được tạo ra trong file KMZ.", "AVMap", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnBrowseImport_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Chọn file Google Earth để nhập vào AutoCAD",
                Filter = "Google Earth Files (*.kmz;*.kml)|*.kmz;*.kml|Tất cả tệp (*.*)|*.*"
            };
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtImportPath.Text = ofd.FileName;
                try
                {
                    FileInfo fi = new FileInfo(ofd.FileName);
                    lblImportInfo.Text = $"Tệp: {fi.Name} ({fi.Length / 1024.0:F1} KB) - Sẵn sàng chuyển đổi sang VN-2000.";
                }
                catch { }
            }
        }

        private void BtnStartImport_Click(object sender, RoutedEventArgs e)
        {
            if (activeCrs == null)
            {
                activeCrs = CrsManager.ShowCrsSelectionDialog(currentDoc);
                if (activeCrs == null) return;
                UpdateCrsDisplay();
            }

            string inPath = txtImportPath.Text.Trim();
            if (string.IsNullOrEmpty(inPath) || !File.Exists(inPath))
            {
                System.Windows.MessageBox.Show("Vui lòng chọn tệp KMZ hoặc KML hợp lệ.", "AVMap", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var options = new ImportKmzOptions
            {
                KeepFolders = rbImportKeepFolders.IsChecked == true,
                TargetLayer = txtSingleLayer.Text.Trim(),
                ImportColors = chkImportColors.IsChecked == true,
                ClosePolygons = chkImportClosed.IsChecked == true,
                ImportLabels = chkImportLabels.IsChecked == true,
                ZoomExtents = chkZoomExtents.IsChecked == true
            };

            int imported = KmzService.ImportFromKmzWithOptions(currentDoc, inPath, activeCrs, options);

            if (imported > 0)
            {
                System.Windows.MessageBox.Show(
                    $"Nhập dữ liệu từ Google Earth thành công!\n\n• Đã tạo: {imported} đối tượng hình học vào ModelSpace\n• Hệ tọa độ nội suy: {activeCrs.Name} (KTT {activeCrs.CentralMeridian}°)",
                    "AVMap - Nhập Google Earth Thành Công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                this.Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Không tìm thấy đối tượng hợp lệ nào trong tệp KMZ/KML để nhập vào CAD.", "AVMap", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
