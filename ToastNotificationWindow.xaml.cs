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
                this.Left = workArea.Right - this.ActualWidth - 20;
                this.Top = workArea.Bottom - this.ActualHeight - 20;

                _autoCloseTimer = new DispatcherTimer();
                _autoCloseTimer.Interval = TimeSpan.FromSeconds(12);
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
                try { this.Close(); } catch { }
                UpdateService.ShowModalDialog(new UpdateWindow(_info));
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