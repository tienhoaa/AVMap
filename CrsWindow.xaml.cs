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

        public CrsWindow() : this(null)
        {
        }

        public CrsWindow(ProvinceCrs currentActiveCrs)
        {
            InitializeComponent();
            try { this.Icon = IconHelper.LoadIcon("Logo.png"); } catch { }
            allProvinces = VN2000Catalog.Provinces;

            ProvinceCrs matchedItem = null;
            if (currentActiveCrs != null)
            {
                // Ưu tiên khớp theo tên chính xác
                matchedItem = allProvinces.FirstOrDefault(p => !string.IsNullOrEmpty(currentActiveCrs.Name) && 
                    p.Name.Equals(currentActiveCrs.Name, StringComparison.OrdinalIgnoreCase));

                // Nếu chưa khớp, tìm theo mã EPSG và Kinh tuyến trục
                if (matchedItem == null)
                {
                    matchedItem = allProvinces.FirstOrDefault(p => p.EpsgCode == currentActiveCrs.EpsgCode && 
                        Math.Abs(p.CentralMeridian - currentActiveCrs.CentralMeridian) < 0.001);
                }

                // Nếu vẫn chưa khớp (ví dụ bản vẽ chỉ lưu KTT), tìm theo KTT
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

            dgCrs.ItemsSource = allProvinces;

            if (currentActiveCrs != null && matchedItem != null)
            {
                lblCurrentCrsText.Text = $"{matchedItem.Name}  •  Kinh tuyến trục {matchedItem.CentralMeridian}° (Múi 3°)";
                lblCurrentCrsText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 128, 61));
                pnlCurrentCrs.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 253, 244));
                pnlCurrentCrs.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(134, 239, 172));
                badgeCurrentCrs.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 252, 231));
                badgeCurrentCrs.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(134, 239, 172));
                lblCurrentCrsBadge.Text = "✔ ĐANG SỬ DỤNG";
                lblCurrentCrsBadge.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 101, 52));

                dgCrs.SelectedItem = matchedItem;
                dgCrs.ScrollIntoView(matchedItem);
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

                if (allProvinces.Count > 0) dgCrs.SelectedIndex = 0;
            }

            if (lblStatus != null)
            {
                lblStatus.Text = $"Đã nạp {allProvinces.Count} hệ tọa độ chuẩn tỉnh thành Việt Nam (Thông tư 25/2014/TT-BTNMT & TT 02/2023/TT-BTNMT)";
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
                return;
            }

            string keywordNorm = NormalizeSearchText(keywordRaw);

            var filtered = allProvinces.Where(p =>
            {
                string pCode = p.EpsgCode.ToString();
                string pNameNorm = NormalizeSearchText(p.Name);
                string pProvNorm = NormalizeSearchText(p.Province);
                string pMeridian = p.CentralMeridian.ToString(CultureInfo.InvariantCulture);
                string pDescNorm = NormalizeSearchText(p.Description);

                return pCode.Contains(keywordNorm) ||
                       pNameNorm.Contains(keywordNorm) ||
                       pProvNorm.Contains(keywordNorm) ||
                       pMeridian.Contains(keywordNorm) ||
                       pDescNorm.Contains(keywordNorm);
            }).ToList();

            dgCrs.ItemsSource = filtered;
            if (filtered.Count > 0) dgCrs.SelectedIndex = 0;
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
            if (dgCrs.SelectedItem is ProvinceCrs crs)
            {
                SelectedCrs = crs;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một hệ tọa độ từ danh sách!", "AVMap", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
