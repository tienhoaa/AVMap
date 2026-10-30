using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AVMap
{
    public partial class MapTypeWindow : Window
    {
        public string SelectedType { get; private set; } = "y";

        public MapTypeWindow()
        {
            InitializeComponent();
            string curType = GoogleTileClient.ActiveMapType?.Replace(",svv", "") ?? "y";
            SelectCard(curType);
        }

        private void CardHybrid_MouseDown(object sender, MouseButtonEventArgs e) => SelectCard("y");
        private void CardSatellite_MouseDown(object sender, MouseButtonEventArgs e) => SelectCard("s");
        private void CardOsm_MouseDown(object sender, MouseButtonEventArgs e) => SelectCard("osm");
        private void CardEsri_MouseDown(object sender, MouseButtonEventArgs e) => SelectCard("esri");
        private void CardRoadmap_MouseDown(object sender, MouseButtonEventArgs e) => SelectCard("m");
        private void CardTerrain_MouseDown(object sender, MouseButtonEventArgs e) => SelectCard("p");

        private void SelectCard(string type)
        {
            SelectedType = type;
            ResetBorders();

            var activeBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            var activeThick = new Thickness(2);

            switch (type)
            {
                case "y":
                    cardHybrid.BorderBrush = activeBrush;
                    cardHybrid.BorderThickness = activeThick;
                    break;
                case "s":
                    cardSatellite.BorderBrush = activeBrush;
                    cardSatellite.BorderThickness = activeThick;
                    break;
                case "osm":
                    cardOsm.BorderBrush = activeBrush;
                    cardOsm.BorderThickness = activeThick;
                    break;
                case "esri":
                    cardEsri.BorderBrush = activeBrush;
                    cardEsri.BorderThickness = activeThick;
                    break;
                case "m":
                    cardRoadmap.BorderBrush = activeBrush;
                    cardRoadmap.BorderThickness = activeThick;
                    break;
                case "p":
                    cardTerrain.BorderBrush = activeBrush;
                    cardTerrain.BorderThickness = activeThick;
                    break;
            }
        }

        private void ResetBorders()
        {
            var defaultBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            var defaultThick = new Thickness(1);

            cardHybrid.BorderBrush = defaultBrush;
            cardHybrid.BorderThickness = defaultThick;

            cardSatellite.BorderBrush = defaultBrush;
            cardSatellite.BorderThickness = defaultThick;

            cardOsm.BorderBrush = defaultBrush;
            cardOsm.BorderThickness = defaultThick;

            cardEsri.BorderBrush = defaultBrush;
            cardEsri.BorderThickness = defaultThick;

            cardRoadmap.BorderBrush = defaultBrush;
            cardRoadmap.BorderThickness = defaultThick;

            cardTerrain.BorderBrush = defaultBrush;
            cardTerrain.BorderThickness = defaultThick;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            GoogleTileClient.ActiveMapType = SelectedType;
            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}