using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Gssoft.Gscad.ApplicationServices;
using ImageMagick;

namespace AVMap.Enji
{
    public class SurveyPhotoItem : INotifyPropertyChanged
    {
        private bool isSelected = true;

        public bool IsSelected
        {
            get => isSelected;
            set { isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public string FilePath { get; set; }
        public string FileName => Path.GetFileName(FilePath);
        public string Dimensions { get; set; }
        public string Format { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Vn2000X { get; set; }
        public double Vn2000Y { get; set; }
        public DateTime? DateTaken { get; set; }
        public int WidthPx { get; set; }
        public int HeightPx { get; set; }

        public bool HasGps => Math.Abs(Latitude) > 0.001 && Math.Abs(Longitude) > 0.001;
        public string GpsCoords => HasGps ? $"{Latitude:F6}, {Longitude:F6}" : "Không có";
        public string Vn2000Coords => HasGps ? $"X={Vn2000X:F2}, Y={Vn2000Y:F2}" : "--";
        public string DateTakenStr => DateTaken.HasValue ? DateTaken.Value.ToString("dd/MM/yyyy HH:mm") : "--";
        public string StatusText => HasGps ? "✔ Có GPS (Hợp lệ)" : "⚠ Không có GPS";
        public string StatusColor => HasGps ? "#16A34A" : "#EA580C";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class SurveyPhotoWindow_enji : Window
    {
        private readonly Document doc;
        private readonly ProvinceCrs crs;
        private readonly ObservableCollection<SurveyPhotoItem> photoItems = new ObservableCollection<SurveyPhotoItem>();

        public SurveyPhotoWindow_enji(Document document, ProvinceCrs activeCrs)
        {
            InitializeComponent();
            try { this.Icon = IconHelper.LoadIcon("Logo.png"); } catch { }
            doc = document;
            crs = activeCrs;
            dgPhotos.ItemsSource = photoItems;

            InitDefaultSaveDir();
            UpdateCacheButtonLabel();
        }

        private void InitDefaultSaveDir()
        {
            string defaultFolder = null;
            try
            {
                string dwgFile = doc?.Database?.Filename;
                if (!string.IsNullOrEmpty(dwgFile) && File.Exists(dwgFile))
                {
                    defaultFolder = Path.Combine(Path.GetDirectoryName(dwgFile), "AnhKhaoSat");
                }
            }
            catch { }

            if (string.IsNullOrEmpty(defaultFolder))
            {
                if (Directory.Exists(@"D:\"))
                    defaultFolder = @"D:\AVMap_AnhKhaoSat";
                else
                    defaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AVMap_AnhKhaoSat");
            }

            txtSaveDir.Text = defaultFolder;
        }

        private void UpdateCacheButtonLabel()
        {
            long bytes = CloudPhotoDownloader.GetCacheSizeBytes() + SurveyPhotoService.GetSurveyCacheSizeBytes();
            double mb = bytes / (1024.0 * 1024.0);
            if (mb > 0.1)
                btnClearCache.Content = $"🧹 Dọn Cache ({mb:F1} MB)";
            else
                btnClearCache.Content = "🧹 Dọn Cache (0 MB)";
        }

        private void BtnBrowseSaveDir_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục lưu ảnh tải về từ Google Drive / Google Photos:";
                if (Directory.Exists(txtSaveDir.Text)) fbd.SelectedPath = txtSaveDir.Text;
                if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    txtSaveDir.Text = fbd.SelectedPath;
                }
            }
        }

        private void BtnClearCache_Click(object sender, RoutedEventArgs e)
        {
            long before = CloudPhotoDownloader.GetCacheSizeBytes() + SurveyPhotoService.GetSurveyCacheSizeBytes();
            double mb = before / (1024.0 * 1024.0);
            CloudPhotoDownloader.ClearCache();
            SurveyPhotoService.ClearSurveyCache();
            UpdateCacheButtonLabel();
            System.Windows.MessageBox.Show($"Đã dọn dẹp sạch {mb:F1} MB bộ nhớ đệm tạm thời!", "AVMap - Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục chứa ảnh chụp hiện trường (iPhone HEIC / Android JPG):";
                if (fbd.ShowDialog() != System.Windows.Forms.DialogResult.OK || !Directory.Exists(fbd.SelectedPath)) return;

                txtLocalPath.Text = fbd.SelectedPath;
                string[] exts = new[] { "*.jpg", "*.jpeg", "*.png", "*.heic", "*.heif", "*.tif", "*.tiff" };
                List<string> files = new List<string>();
                foreach (var ext in exts)
                {
                    files.AddRange(Directory.GetFiles(fbd.SelectedPath, ext, SearchOption.TopDirectoryOnly));
                    files.AddRange(Directory.GetFiles(fbd.SelectedPath, ext.ToUpper(), SearchOption.TopDirectoryOnly));
                }

                var distinct = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                await ScanFilesAsync(distinct);
            }
        }

        private async void BtnBrowseFiles_Click(object sender, RoutedEventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Chọn các file ảnh khảo sát thực địa:";
                ofd.Filter = "Ảnh chụp hiện trường (*.jpg;*.jpeg;*.png;*.heic;*.heif;*.tif)|*.jpg;*.jpeg;*.png;*.heic;*.heif;*.tif|Tất cả tập tin (*.*)|*.*";
                ofd.Multiselect = true;
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                txtLocalPath.Text = $"Đã chọn {ofd.FileNames.Length} tệp ảnh";
                await ScanFilesAsync(ofd.FileNames.ToList());
            }
        }

        private async void BtnDownloadCloud_Click(object sender, RoutedEventArgs e)
        {
            string url = txtCloudUrl.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                System.Windows.MessageBox.Show("Vui lòng dán link album Google Photos hoặc Google Drive!", "AVMap - Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            pbProgress.Visibility = System.Windows.Visibility.Visible;
            pbProgress.IsIndeterminate = false;
            pbProgress.Value = 0;
            txtStatus.Text = "Đang kết nối Cloud và tải ảnh...";

            string saveDir = txtSaveDir.Text.Trim();
            if (string.IsNullOrEmpty(saveDir))
            {
                InitDefaultSaveDir();
                saveDir = txtSaveDir.Text.Trim();
            }
            if (!Directory.Exists(saveDir))
            {
                try { Directory.CreateDirectory(saveDir); } catch { }
            }

            try
            {
                var files = await CloudPhotoDownloader.DownloadPhotosFromUrlAsync(url, saveDir, (cur, tot, msg) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtStatus.Text = msg;
                        if (tot > 0)
                        {
                            pbProgress.Maximum = tot;
                            pbProgress.Value = cur;
                        }
                    });
                });

                // Tự động dọn dẹp sạch cache thừa trong AppData
                CloudPhotoDownloader.ClearCache();
                UpdateCacheButtonLabel();

                if (files.Count == 0)
                {
                    txtStatus.Text = "Không tìm thấy hoặc không thể tải ảnh từ link này.";
                    System.Windows.MessageBox.Show("Không tìm thấy ảnh hoặc link chưa được chia sẻ công khai. Vui lòng kiểm tra lại quyền truy cập link!", "AVMap - Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                await ScanFilesAsync(files);
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Lỗi tải Cloud: {ex.Message}";
                System.Windows.MessageBox.Show($"Lỗi tải ảnh từ Cloud: {ex.Message}", "AVMap - Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                pbProgress.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private async Task ScanFilesAsync(List<string> filePaths)
        {
            if (filePaths == null || filePaths.Count == 0) return;

            photoItems.Clear();
            pbProgress.Visibility = System.Windows.Visibility.Visible;
            pbProgress.Maximum = filePaths.Count;
            pbProgress.Value = 0;
            txtStatus.Text = $"Đang đọc dữ liệu GPS từ {filePaths.Count} ảnh...";

            var scanned = await Task.Run(() =>
            {
                List<SurveyPhotoItem> list = new List<SurveyPhotoItem>();
                int count = 0;
                foreach (var file in filePaths)
                {
                    count++;
                    try
                    {
                        var item = SurveyPhotoService.AnalyzePhoto(file, crs);
                        if (item != null) list.Add(item);
                    }
                    catch { }
                }
                return list;
            });

            foreach (var it in scanned)
            {
                photoItems.Add(it);
            }

            pbProgress.Visibility = System.Windows.Visibility.Collapsed;
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int total = photoItems.Count;
            int withGps = photoItems.Count(p => p.HasGps);
            int noGps = total - withGps;
            txtSummary.Text = $"Tổng số: {total} ảnh | Có GPS: {withGps} ảnh | Không có GPS: {noGps} ảnh";
            txtStatus.Text = $"Đã quét xong {total} ảnh. Sẵn sàng chèn vào enjiCAD!";
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in photoItems) item.IsSelected = true;
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in photoItems) item.IsSelected = false;
        }

        private void BtnSelectOnlyGps_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in photoItems) item.IsSelected = item.HasGps;
        }

        private async void BtnInsert_Click(object sender, RoutedEventArgs e)
        {
            var selected = photoItems.Where(p => p.IsSelected && p.HasGps).ToList();
            if (selected.Count == 0)
            {
                System.Windows.MessageBox.Show("Vui lòng chọn ít nhất 1 ảnh có tọa độ GPS hợp lệ để chèn vào enjiCAD!", "AVMap - Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double widthLandscape = 15.0;
            double.TryParse(txtWidthLandscape.Text, out widthLandscape);
            if (widthLandscape <= 1.0) widthLandscape = 15.0;

            double heightPortrait = 9.0;
            double.TryParse(txtHeightPortrait.Text, out heightPortrait);
            if (heightPortrait <= 1.0) heightPortrait = 9.0;

            double offsetVal = 15.0;
            double.TryParse(txtOffset.Text, out offsetVal);
            if (offsetVal <= 0.0) offsetVal = 15.0;

            double spacingVal = 3.0;
            if (txtSpacing != null) double.TryParse(txtSpacing.Text, out spacingVal);
            if (spacingVal < 0.5) spacingVal = 3.0;

            bool antiOverlap = chkAutoAntiOverlap?.IsChecked == true;
            int layoutMode = cmbLayoutMode?.SelectedIndex ?? 0;

            bool drawLeader = chkDrawLeader.IsChecked == true;
            bool drawRectang = chkDrawRectang.IsChecked == true;
            bool drawLabel = chkDrawLabel.IsChecked == true;
            bool autoOrient = chkAutoOrient.IsChecked == true;

            btnInsert.IsEnabled = false;
            pbProgress.Visibility = System.Windows.Visibility.Visible;
            pbProgress.Maximum = selected.Count;
            pbProgress.Value = 0;
            txtStatus.Text = "Đang tối ưu vị trí ảnh chống đè trùng nhau...";

            // 1. Tự động tính toán vị trí bố trí chống đè/trùng ảnh
            var layoutItems = await Task.Run(() =>
            {
                return SurveyPhotoService.CalculatePhotoLayout(
                    selected, widthLandscape, heightPortrait,
                    offsetVal, offsetVal, spacingVal, antiOverlap, layoutMode);
            });

            int successCount = 0;
            int index = 0;

            foreach (var item in layoutItems)
            {
                index++;
                txtStatus.Text = $"Đang chèn ảnh {index}/{layoutItems.Count}: {item.Photo.FileName}...";
                pbProgress.Value = index;

                bool ok = await Task.Run(() =>
                {
                    return SurveyPhotoService.InsertSinglePhoto(
                        doc, item.Photo, crs, item.WidthMeters, item.HeightMeters,
                        item.OriginX, item.OriginY, drawLeader, drawRectang, drawLabel, autoOrient);
                });

                if (ok) successCount++;
            }

            pbProgress.Visibility = System.Windows.Visibility.Collapsed;
            btnInsert.IsEnabled = true;
            txtStatus.Text = $"✔ Hoàn thành! Đã chèn {successCount}/{selected.Count} ảnh vào bản vẽ.";

            System.Windows.MessageBox.Show(
                $"HOÀN THÀNH CHÈN ẢNH KHẢO SÁT HIỆN TRƯỜNG!\n\n" +
                $"• Số ảnh đã chèn: {successCount} ảnh\n" +
                $"• Mũi tên Leader: {(drawLeader ? "Đã cắm đúng vị trí hư hỏng" : "Tắt")}\n" +
                $"• Khung viền Rectang: {(drawRectang ? "Có" : "Không")}\n" +
                $"• Chiều ảnh: Đã xoay chuẩn 100% theo cảm biến điện thoại!",
                "AVMap enjiCAD - Thành Công",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Tự động dọn sạch các file cache tạm phát sinh trong AppData
            try
            {
                SurveyPhotoService.ClearSurveyCache();
                CloudPhotoDownloader.ClearCache();
            }
            catch { }

            this.DialogResult = true;
            this.Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
