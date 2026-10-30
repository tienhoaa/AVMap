using System;
using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;

namespace AVMap
{
    public partial class HelpWindow : Window
    {
        private const string REG_KEY = @"Software\AVMap";
        private const string REG_VAL = "ShowWelcomeOnLoad";

        public HelpWindow()
        {
            InitializeComponent();

            try
            {
                var icon = IconHelper.LoadIcon("Logo.png");
                if (icon != null)
                {
                    this.Icon = icon;
                    if (imgLogo != null) imgLogo.Source = icon;
                }
            }
            catch { }

            try
            {
                chkShowOnStartup.IsChecked = ShouldShowOnStartup();
            }
            catch { }
        }

        public static bool ShouldShowOnStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(REG_KEY))
                {
                    if (key != null)
                    {
                        object val = key.GetValue(REG_VAL);
                        if (val != null)
                        {
                            return Convert.ToInt32(val) != 0;
                        }
                    }
                }
            }
            catch { }
            return true;
        }

        private void SaveSettings()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(REG_KEY))
                {
                    if (key != null)
                    {
                        key.SetValue(REG_VAL, (chkShowOnStartup.IsChecked == true) ? 1 : 0, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveSettings();
            base.OnClosing(e);
        }
    }
}
