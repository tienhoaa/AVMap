using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;
using System.Text;

namespace AVMap
{
    public partial class CrsWindow : Window
    {
        public ProvinceCrs SelectedCrs { get; private set; }
        private List<ProvinceCrs> allProvinces;
        private List<ProvinceCrs> recentProvinces;

        public CrsWindow() : this(null)
        {
        }

        public CrsWindow(ProvinceCrs currentActiveCrs)
        {
            InitializeComponent();
            try { this.Icon = IconHelper.LoadIcon("Logo.png"); } catch { }

            allProvinces = VN2000Catalog.Provinces;
            recentProvinces = VN2000Catalog.GetRecentProvinces();

            ProvinceCrs matchedItem = null;
            if (currentActiveCrs != null)
            {
                // Khớp theo tên chính xác
                matchedItem = allProvinces.FirstOrDefault(p => !string.IsNullOrEmpty(currentActiveCrs.Name) && 
                    p.Name.Equals(currentActiveCrs.Name, StringComparison.OrdinalIgnoreCase));

                // Khớp theo mã EPSG, Kinh tuyến trục và ScaleFactor
                if (matchedItem == null)
                {
                    matchedItem = allProvinces.FirstOrDefault(p => p.EpsgCode == currentActiveCrs.EpsgCode && 
                        Math.Abs(p.CentralMeridian - currentActiveCrs.CentralMeridian) < 0.001 &&
                        Math.Abs(p.ScaleFactor - currentActiveCrs.ScaleFactor) < 0.0001);
                }

                // Khớp theo KTT
                if (matchedItem == null && currentActiveCrs.CentralMeridian > 0)
                {
                    matchedItem = allProvinces.FirstOrDefault(p => Math.Abs(p.CentralMeridian - currentActiveCrs.CentralMeridian) < 0.001);
                }

                foreach (var p in allProvinces)
                {
                    p.IsActive = (matchedItem != null && ReferenceEquals(p, matchedItem));
                }
            }
            else
            {
                foreach (var p in allProvinces) p.IsActive = false;
            }

            // Nạp dữ liệu 2 bảng
            dgRecent.ItemsSource = recentProvinces;
            dgCrs.ItemsSource = allProvinces;

            if (currentActiveCrs != null && matchedItem != null)
            {
                lblCurrentCrsText.Text = $"{matchedItem.Name}  •  Kinh tuyến trục {matchedItem.CentralMeridian}° ({matchedItem.ZoneText})";
                lblCurrentCrsText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 128, 61));
                pnlCurrentCrs.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 253, 244));
                pnlCurrentCrs.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(134, 239, 172));
                badgeCurrentCrs.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 252, 231));
                badgeCurrentCrs.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(134, 239, 172));
                lblCurrentCrsBadge.Text = "✔ ĐANG SỬ DỤNG";
                lblCurrentCrsBadge.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 101, 52));

                // Ưu tiên chọn trong bảng Recent nếu có
                var recentMatch = recentProvinces.FirstOrDefault(r => r.EpsgCode == matchedItem.EpsgCode && Math.Abs(r.CentralMeridian - matchedItem.CentralMeridian) < 0.001);
                if (recentMatch != null)
                {
                    dgRecent.SelectedItem = recentMatch;
                }
                else
                {
                    dgCrs.SelectedItem = matchedItem;
                    dgCrs.ScrollIntoView(matchedItem);
                }
            }
            else
            {
                lblCurrentCrsText.Text = "Bản vẽ này chưa thiết lập hệ tọa độ! Vui lòng chọn tỉnh thành tương ứng dự án bên dưới.";
                lblCurrentCrsText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 83, 9));
                pnlCurrentCrs.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 243, 199));
                pnlCurrentCrs.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(252, 211, 77));
                badgeCurrentCrs.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 243, 199));
                badgeCurrentCrs.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 191, 36));
                lblCurrentCrsBadge.Text = "⚠ CHƯA THIẾT LẬP";
                lblCurrentCrsBadge.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 83, 9));

                if (recentProvinces != null && recentProvinces.Count > 0)
                {
                    dgRecent.SelectedIndex = 0;
                }
                else if (allProvinces.Count > 0)
                {
                    dgCrs.SelectedIndex = 0;
                }
            }

            UpdateCountLabels();
        }

        private void UpdateCountLabels()
        {
            if (lblCountInfo != null)
            {
                lblCountInfo.Text = $"Hiển thị: {allProvinces.Count} hệ tọa độ";
            }
            if (lblStatus != null)
            {
                lblStatus.Text = $"Đã nạp {allProvinces.Count} hệ tọa độ chuẩn (Gồm 34 tỉnh sáp nhập mới TT 24/2025 & Múi 6° TM-6)";
            }
        }

        private static string NormalizeSearchText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC)
                     .Replace('đ', 'd').Replace('Đ', 'D')
                     .ToLowerInvariant().Trim();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string keywordRaw = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(keywordRaw))
            {
                dgCrs.ItemsSource = allProvinces;
                if (lblCountInfo != null) lblCountInfo.Text = $"Hiển thị: {allProvinces.Count} hệ tọa độ";
                return;
            }

            string keywordNorm = NormalizeSearchText(keywordRaw);

            var filtered = allProvinces.Where(p =>
            {
                string pCode = p.EpsgCode.ToString();
                string pNameNorm = NormalizeSearchText(p.Name);
                string pProvNorm = NormalizeSearchText(p.Province);
                string pZoneNorm = NormalizeSearchText(p.ZoneText);
                string pMeridian = p.CentralMeridian.ToString(CultureInfo.InvariantCulture);
                string pDescNorm = NormalizeSearchText(p.Description);

                return pCode.Contains(keywordNorm) ||
                       pNameNorm.Contains(keywordNorm) ||
                       pProvNorm.Contains(keywordNorm) ||
                       pZoneNorm.Contains(keywordNorm) ||
                       pMeridian.Contains(keywordNorm) ||
                       pDescNorm.Contains(keywordNorm);
            }).ToList();

            dgCrs.ItemsSource = filtered;
            if (lblCountInfo != null) lblCountInfo.Text = $"Kết quả tìm kiếm: {filtered.Count}/{allProvinces.Count}";
            if (filtered.Count > 0) dgCrs.SelectedIndex = 0;
        }

        private void DgRecent_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgRecent.SelectedItem != null && dgCrs != null)
            {
                dgCrs.UnselectAll();
            }
        }

        private void DgCrs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgCrs.SelectedItem != null && dgRecent != null)
            {
                dgRecent.UnselectAll();
            }
        }

        private void DgRecent_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectAndClose();
        }

        private void DgCrs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectAndClose();
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            SelectAndClose();
        }

        private void SelectAndClose()
        {
            ProvinceCrs crs = dgRecent.SelectedItem as ProvinceCrs ?? dgCrs.SelectedItem as ProvinceCrs;

            if (crs != null)
            {
                SelectedCrs = crs;
                // Lưu vào danh sách Đã sử dụng gần đây (Lịch sử)
                VN2000Catalog.SaveToRecent(crs);

                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một hệ tọa độ từ bảng Lịch sử hoặc bảng Danh mục!", "AVMap", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
