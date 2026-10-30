using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AVMap
{
    public partial class CaptureBoxWindow : Window
    {
        private readonly Document doc;
        private readonly ProvinceCrs crs;

        private double minX = 0;
        private double maxX = 0;
        private double minY = 0;
        private double maxY = 0;
        private bool hasArea = false;

        public CaptureBoxWindow(Document document, ProvinceCrs activeCrs)
        {
            InitializeComponent();
            doc = document;
            crs = activeCrs;

            // Thiết lập thư mục lưu mặc định
            string defaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AVMap_BinhDo");
            try
            {
                if (doc != null && !string.IsNullOrEmpty(doc.Name) && File.Exists(doc.Name))
                {
                    defaultFolder = Path.GetDirectoryName(doc.Name);
                }
            }
            catch { }

            if (!Directory.Exists(defaultFolder))
            {
                try { Directory.CreateDirectory(defaultFolder); } catch { }
            }
            txtSaveDirectory.Text = defaultFolder;

            // Khởi tạo nguồn map theo cấu hình đang dùng
            foreach (ComboBoxItem item in cmbMapSource.Items)
            {
                if (item.Tag?.ToString() == GoogleTileClient.ActiveMapType)
                {
                    cmbMapSource.SelectedItem = item;
                    break;
                }
            }

            // Mặc định lấy theo khung nhìn hiện tại nếu có
            LoadCurrentViewCoordinates();
        }

        private void LoadCurrentViewCoordinates()
        {
            if (doc == null) return;
            Editor ed = doc.Editor;
            try
            {
                double cx = 0, cy = 0, viewHeight = 0, screenW = 1920, screenH = 1080;
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

                if (viewHeight > 5)
                {
                    double viewWidth = viewHeight * (screenW / screenH);
                    SetArea(cx - viewWidth / 2.0, cx + viewWidth / 2.0, cy - viewHeight / 2.0, cy + viewHeight / 2.0);
                }
            }
            catch { }
        }

        private void SetArea(double x1, double x2, double y1, double y2)
        {
            minX = Math.Min(x1, x2);
            maxX = Math.Max(x1, x2);
            minY = Math.Min(y1, y2);
            maxY = Math.Max(y1, y2);
            hasArea = true;

            double widthMeters = maxX - minX;
            double heightMeters = maxY - minY;
            double areaSqM = widthMeters * heightMeters;
            double areaHa = areaSqM / 10000.0;

            txtCoord1.Text = $"Điểm 1 (Tây Nam): X={minX:F1}, Y={minY:F1}";
            txtCoord2.Text = $"Điểm 2 (Đông Bắc): X={maxX:F1}, Y={maxY:F1}";
            txtAreaDimensions.Text = $"Kích thước: Rộng {widthMeters:F1}m x Dài {heightMeters:F1}m | Diện tích: {areaSqM:N0} m² ({areaHa:F2} ha)";
            txtStatus.Text = "Đã xác định phạm vi quét. Hãy chọn chất lượng và bấm Bắt đầu!";
        }

        private void BtnPickArea_Click(object sender, RoutedEventArgs e)
        {
            if (doc == null) return;
            Editor ed = doc.Editor;

            this.Hide();
            try
            {
                using (doc.LockDocument())
                {
                    PromptPointOptions ppo = new PromptPointOptions("\n[AVMap - Quét Bình Đồ] Chọn điểm góc thứ 1 trên bản vẽ CAD: ");
                    PromptPointResult ppr1 = ed.GetPoint(ppo);
                    if (ppr1.Status == PromptStatus.OK)
                    {
                        PromptCornerOptions pco = new PromptCornerOptions("\n[AVMap - Quét Bình Đồ] Chọn điểm góc thứ 2 đối diện: ", ppr1.Value);
                        PromptPointResult ppr2 = ed.GetCorner(pco);
                        if (ppr2.Status == PromptStatus.OK)
                        {
                            SetArea(ppr1.Value.X, ppr2.Value.X, ppr1.Value.Y, ppr2.Value.Y);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Lỗi chọn điểm: {ex.Message}";
            }
            finally
            {
                this.Show();
                this.Activate();
            }
        }

        private void BtnUseCurrentView_Click(object sender, RoutedEventArgs e)
        {
            LoadCurrentViewCoordinates();
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục lưu trữ ảnh bình đồ vệ tinh và file tọa độ:";
                if (Directory.Exists(txtSaveDirectory.Text))
                {
                    fbd.SelectedPath = txtSaveDirectory.Text;
                }
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtSaveDirectory.Text = fbd.SelectedPath;
                }
            }
        }

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (!hasArea || (maxX - minX < 5) || (maxY - minY < 5))
            {
                System.Windows.MessageBox.Show("Vui lòng quét chọn 2 điểm trên bản vẽ CAD hoặc lấy theo khung nhìn trước khi xuất ảnh!", "AVMap - Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string saveDir = txtSaveDirectory.Text.Trim();
            if (string.IsNullOrEmpty(saveDir) || !Directory.Exists(saveDir))
            {
                try { Directory.CreateDirectory(saveDir); }
                catch
                {
                    System.Windows.MessageBox.Show("Thư mục lưu trữ không hợp lệ. Vui lòng chọn lại thư mục khác!", "AVMap - Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Xác định chất lượng
            int maxTiles = 64;
            int zoomOffset = 1;
            string qualityLabel = "Cực nét (Ultra HD)";

            if (rbQualityMedium.IsChecked == true)
            {
                maxTiles = 36;
                zoomOffset = 0;
                qualityLabel = "Tiêu chuẩn (Full HD)";
            }
            else if (rbQualityFast.IsChecked == true)
            {
                maxTiles = 16;
                zoomOffset = -1;
                qualityLabel = "Nhanh (Thấp)";
            }

            // Nguồn bản đồ
            ComboBoxItem selItem = cmbMapSource.SelectedItem as ComboBoxItem;
            string mapType = selItem?.Tag?.ToString() ?? "y";

            bool doInsertCad = chkInsertCad.IsChecked == true;
            bool doExportWorld = chkExportWorldFile.IsChecked == true;

            // Khóa giao diện
            btnExecute.IsEnabled = false;
            pbProgress.Visibility = System.Windows.Visibility.Visible;
            pbProgress.IsIndeterminate = true;
            txtStatus.Text = $"Đang kết nối vệ tinh và tải các mảnh gạch ảnh ({qualityLabel})...";

            try
            {
                var result = await Task.Run(() => CaptureService.ProcessCaptureArea(minX, maxX, minY, maxY, crs, mapType, maxTiles, zoomOffset, saveDir, doExportWorld));

                if (result == null || string.IsNullOrEmpty(result.ImagePath) || !File.Exists(result.ImagePath))
                {
                    txtStatus.Text = "Không thể tải hoặc ghép ảnh vệ tinh. Vui lòng kiểm tra lại kết nối mạng!";
                    System.Windows.MessageBox.Show("Không thể tải ảnh vệ tinh cho vùng đã chọn. Vui lòng thử lại!", "AVMap - Lỗi Mạng", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Chèn vào CAD trên UI Thread
                if (doInsertCad && doc != null)
                {
                    txtStatus.Text = "Đang chèn ảnh vào AutoCAD ModelSpace...";
                    CaptureService.InsertCapturedImageToCad(doc, result.ImagePath, result.OriginX, result.OriginY, result.WidthMeters, result.HeightMeters);
                }

                txtStatus.Text = $"✔ Hoàn tất! Đã lưu ảnh: {Path.GetFileName(result.ImagePath)}";
                System.Windows.MessageBox.Show(
                    $"ĐÃ XUẤT ẢNH BÌNH ĐỒ THÀNH CÔNG!\n\n" +
                    $"• Kích thước: {result.WidthMeters:F1}m x {result.HeightMeters:F1}m\n" +
                    $"• Điểm đặt Tây Nam: X={result.OriginX:F2}, Y={result.OriginY:F2}\n" +
                    $"• File ảnh: {result.ImagePath}\n" +
                    (doExportWorld ? $"• World file: {Path.ChangeExtension(result.ImagePath, ".jgw")}\n" : "") +
                    (doInsertCad ? $"• Đã chèn trực tiếp vào ModelSpace trên bản vẽ CAD!" : ""),
                    "AVMap - Hoàn Thành",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Lỗi: {ex.Message}";
                System.Windows.MessageBox.Show($"Đã xảy ra lỗi trong quá trình xử lý: {ex.Message}", "AVMap - Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnExecute.IsEnabled = true;
                pbProgress.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
