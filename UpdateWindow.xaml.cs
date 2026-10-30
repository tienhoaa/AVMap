using System;
using System.Windows;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace AVMap
{
    public partial class UpdateWindow : Window
    {
        private UpdateInfo _info;

        public UpdateWindow(UpdateInfo info)
        {
            InitializeComponent();
            try { this.Icon = IconHelper.LoadIcon("Logo.png"); } catch { }
            _info = info;

            if (_info.IsLatest)
            {
                txtIcon.Text = "✨";
                txtTitle.Text = "AVMAP ĐANG Ở PHIÊN BẢN MỚI NHẤT";
                lblVersionCompare.Text = $"Phiên bản hiện tại: v{UpdateService.CurrentVersion} (Mới nhất)";
                txtBadge.Text = "MỚI NHẤT";
                txtBadge.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                btnUpdateNow.Content = "⚡ Cập Nhật / Làm Mới File";
            }
            else
            {
                txtIcon.Text = "🚀";
                txtTitle.Text = "ĐÃ CÓ BẢN CẬP NHẬT AVMAP MỚI!";
                lblVersionCompare.Text = $"Phiên bản mới: v{_info.NewVersion}  •  Bản đang dùng: v{UpdateService.CurrentVersion}";
                txtBadge.Text = "CÓ BẢN MỚI";
                txtBadge.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                btnUpdateNow.Content = "⚡ Cập Nhật Tự Động Ngay";
            }

            txtChangeLog.Text = _info.ChangeLogText;
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            var confirm = Forms.MessageBox.Show(
                "AutoCAD sẽ được đóng để ghi đè các file cập nhật mới, sau đó tự động khởi động lại.\n\nBạn đã lưu bản vẽ chưa?",
                "Xác Nhận Cập Nhật AVMap",
                Forms.MessageBoxButtons.YesNo,
                Forms.MessageBoxIcon.Question);

            if (confirm == Forms.DialogResult.Yes)
            {
                this.DialogResult = true;
                this.Close();
                UpdateService.ExecuteUpdatePipeline(_info);
            }
        }
    }
}