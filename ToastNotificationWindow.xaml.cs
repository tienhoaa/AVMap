using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace AVMap
{
    public partial class ToastNotificationWindow : Window
    {
        private UpdateInfo _info;
        private DispatcherTimer _autoCloseTimer;

        public ToastNotificationWindow(UpdateInfo info)
        {
            InitializeComponent();
            _info = info;
            txtVersion.Text = $"(v{info.NewVersion})";

            this.Loaded += (s, e) =>
            {
                var workArea = SystemParameters.WorkArea;
                double w = (this.ActualWidth > 50) ? this.ActualWidth : (this.Width > 50 ? this.Width : 380);
                double h = (this.ActualHeight > 30) ? this.ActualHeight : (this.Height > 30 ? this.Height : 105);
                this.Left = workArea.Right - w - 24;
                this.Top = workArea.Bottom - h - 24;

                _autoCloseTimer = new DispatcherTimer();
                _autoCloseTimer.Interval = TimeSpan.FromSeconds(15);
                _autoCloseTimer.Tick += (ts, te) =>
                {
                    _autoCloseTimer.Stop();
                    try { this.Close(); } catch { }
                };
                _autoCloseTimer.Start();
            };

            this.MouseLeftButtonUp += (s, e) =>
            {
                _autoCloseTimer?.Stop();
                this.Hide();
                try
                {
                    var win = new UpdateWindow(_info);
                    win.Topmost = true;
                    win.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    win.ShowDialog();
                }
                catch { }
                try { this.Close(); } catch { }
            };
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            _autoCloseTimer?.Stop();
            try { this.Close(); } catch { }
        }
    }
}