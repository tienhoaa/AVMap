using System;
using System.Windows;

namespace AVMap
{
    public partial class StreetViewAiSettingsWindow : Window
    {
        public StreetViewAiSettingsWindow()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            var s = StreetViewAiSettings.Instance;

            if (s.Provider == "Clipdrop")
            {
                rbClipdrop.IsChecked = true;
                txtApiKey.Text = s.ClipdropApiKey;
            }
            else if (s.Provider == "GoogleGemini")
            {
                rbGoogle.IsChecked = true;
                txtApiKey.Text = s.GoogleApiKey;
            }
            else
            {
                rbBuiltIn.IsChecked = true;
            }

            chkRemoveRoadWatermark.IsChecked = s.RemoveRoadWatermark;
            chkAddStamp.IsChecked = s.AddSurveyStamp;
            chkIncludeTimestamp.IsChecked = s.IncludeTimestamp;
            txtProjectName.Text = s.ProjectName;

            UpdateApiKeyVisibility();
        }

        private void Provider_Changed(object sender, RoutedEventArgs e)
        {
            UpdateApiKeyVisibility();
        }

        private void UpdateApiKeyVisibility()
        {
            if (gridApiKey == null) return;

            if (rbClipdrop?.IsChecked == true)
            {
                gridApiKey.Visibility = Visibility.Visible;
                lblKeyTitle.Text = "CLIPDROP API KEY (Lấy tại clipdrop.co/apis):";
                txtApiKey.Text = StreetViewAiSettings.Instance.ClipdropApiKey;
            }
            else if (rbGoogle?.IsChecked == true)
            {
                gridApiKey.Visibility = Visibility.Visible;
                lblKeyTitle.Text = "GOOGLE AI KEY (Lấy miễn phí tại aistudio.google.com):";
                txtApiKey.Text = StreetViewAiSettings.Instance.GoogleApiKey;
            }
            else
            {
                gridApiKey.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var s = StreetViewAiSettings.Instance;

            if (rbClipdrop.IsChecked == true)
            {
                s.Provider = "Clipdrop";
                s.ClipdropApiKey = txtApiKey.Text.Trim();
            }
            else if (rbGoogle.IsChecked == true)
            {
                s.Provider = "GoogleGemini";
                s.GoogleApiKey = txtApiKey.Text.Trim();
            }
            else
            {
                s.Provider = "BuiltIn";
            }

            s.RemoveRoadWatermark = chkRemoveRoadWatermark.IsChecked == true;
            s.AddSurveyStamp = chkAddStamp.IsChecked == true;
            s.IncludeTimestamp = chkIncludeTimestamp.IsChecked == true;
            s.ProjectName = string.IsNullOrWhiteSpace(txtProjectName.Text) ? "KHAO SAT TUYEN" : txtProjectName.Text.Trim();

            s.Save();
            MessageBox.Show("Đã lưu cấu hình xử lý ảnh thành công!", "AVMap", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
