using System;
using System.Globalization;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace AVMap
{
    public partial class ContourWindow : Window
    {
        private Document doc;
        private ProvinceCrs activeCrs;
        private double minX = 0, maxX = 0, minY = 0, maxY = 0;
        private bool hasArea = false;

        public ContourWindow(Document cadDoc)
        {
            InitializeComponent();
            doc = cadDoc;

            activeCrs = CrsManager.GetActiveCrs(doc);
            if (activeCrs == null)
            {
                activeCrs = CrsManager.ShowCrsSelectionDialog(doc);
            }

            if (activeCrs != null)
            {
                lblActiveCrs.Text = $"{activeCrs.Name} ({activeCrs.CentralMeridian}°)";
            }
            else
            {
                lblActiveCrs.Text = "Chưa chọn VN-2000";
            }

            LoadCurrentViewIfPossible();
        }

        private void LoadCurrentViewIfPossible()
        {
            if (doc == null) return;
            try
            {
                Editor ed = doc.Editor;
                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    double cx = view.CenterPoint.X;
                    double cy = view.CenterPoint.Y;
                    double h = view.Height;
                    double w = h * (view.Width > 0 ? (view.Width / view.Height) : 1.77);

                    if (h >= 50 && h <= 50000 && cx > 10000 && cy > 10000)
                    {
                        SetArea(cx - w / 2.0, cx + w / 2.0, cy - h / 2.0, cy + h / 2.0, "Khung nhìn CAD hiện tại");
                    }
                }
            }
            catch { }
        }

        private void SetArea(double x1, double x2, double y1, double y2, string sourceDesc = "")
        {
            minX = Math.Min(x1, x2);
            maxX = Math.Max(x1, x2);
            minY = Math.Min(y1, y2);
            maxY = Math.Max(y1, y2);
            hasArea = true;

            double width = maxX - minX;
            double height = maxY - minY;
            double areaHa = (width * height) / 10000.0;

            string desc = string.IsNullOrEmpty(sourceDesc) ? "" : $" ({sourceDesc})";
            txtAreaDimensions.Text = $"Khu vực: Rộng {width:N0}m x Dài {height:N0}m | Diện tích: {areaHa:N1} ha{desc}";
            txtAreaCoords.Text = $"Tây Nam: X={minX:F1}, Y={minY:F1} | Đông Bắc: X={maxX:F1}, Y={maxY:F1}";
            txtStatus.Text = "Đã xác định phạm vi. Hãy bấm Bắt đầu tạo đồng mức!";
        }

        private void BtnPick2Points_Click(object sender, RoutedEventArgs e)
        {
            if (doc == null) return;
            Editor ed = doc.Editor;

            this.Hide();
            try
            {
                using (doc.LockDocument())
                {
                    PromptPointOptions ppo = new PromptPointOptions("\n[AVMap - Đường Đồng Mức] Chọn điểm góc thứ 1 trên bản vẽ CAD: ");
                    PromptPointResult ppr1 = ed.GetPoint(ppo);
                    if (ppr1.Status == PromptStatus.OK)
                    {
                        PromptCornerOptions pco = new PromptCornerOptions("\n[AVMap - Đường Đồng Mức] Chọn điểm góc thứ 2 đối diện: ", ppr1.Value);
                        PromptPointResult ppr2 = ed.GetCorner(pco);
                        if (ppr2.Status == PromptStatus.OK)
                        {
                            SetArea(ppr1.Value.X, ppr2.Value.X, ppr1.Value.Y, ppr2.Value.Y, "Quét 2 điểm");
                        }
                    }
                }
            }
            finally
            {
                this.ShowDialog();
            }
        }

        private void BtnPickPolyline_Click(object sender, RoutedEventArgs e)
        {
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            this.Hide();
            try
            {
                using (doc.LockDocument())
                {
                    PromptEntityOptions peo = new PromptEntityOptions("\n[AVMap - Đường Đồng Mức] Chọn đường Polyline ranh giới trên bản vẽ: ");
                    peo.SetRejectMessage("\nĐối tượng phải là đường Polyline!");
                    peo.AddAllowedClass(typeof(Polyline), false);
                    PromptEntityResult per = ed.GetEntity(peo);

                    if (per.Status == PromptStatus.OK)
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            Polyline poly = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                            if (poly != null && poly.NumberOfVertices >= 2)
                            {
                                Extents3d ext = poly.GeometricExtents;
                                double pMinX = ext.MinPoint.X;
                                double pMinY = ext.MinPoint.Y;
                                double pMaxX = ext.MaxPoint.X;
                                double pMaxY = ext.MaxPoint.Y;

                                // Mở rộng nhẹ 2%
                                double padX = (pMaxX - pMinX) * 0.02;
                                double padY = (pMaxY - pMinY) * 0.02;
                                SetArea(pMinX - padX, pMaxX + padX, pMinY - padY, pMaxY + padY, $"Polyline {poly.NumberOfVertices} đỉnh");
                            }
                        }
                    }
                }
            }
            finally
            {
                this.ShowDialog();
            }
        }

        private void ChkAddLabels_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlLabelSettings == null) return;
            pnlLabelSettings.Visibility = (chkAddLabels.IsChecked == true) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (doc == null) return;
            Editor ed = doc.Editor;

            if (!hasArea || (maxX - minX) <= 10 || (maxY - minY) <= 10)
            {
                MessageBox.Show("Vui lòng chọn phạm vi vùng cần tạo đồng mức (Quét 2 điểm hoặc chọn Polyline)!", "AVMap", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (activeCrs == null)
            {
                activeCrs = CrsManager.ShowCrsSelectionDialog(doc);
                if (activeCrs == null) return;
            }

            // Đọc thông số từ Form
            double interval = 5.0;
            string intervalText = cboInterval.Text ?? "5";
            if (intervalText.Contains(" ")) intervalText = intervalText.Split(' ')[0];
            if (!double.TryParse(intervalText, NumberStyles.Any, CultureInfo.InvariantCulture, out interval) || interval <= 0)
            {
                interval = 5.0;
            }

            bool addLabels = chkAddLabels.IsChecked == true;
            bool indexOnly = rbLabelIndexOnly.IsChecked == true;

            double labelSpacing = 250.0;
            if (!double.TryParse(txtLabelSpacing.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out labelSpacing) || labelSpacing < 20)
            {
                labelSpacing = 250.0;
            }

            double textHeight = 2.0;
            if (!double.TryParse(txtTextHeight.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out textHeight) || textHeight <= 0)
            {
                textHeight = 2.0;
            }

            bool smoothing = chkSmoothing.IsChecked == true;

            this.Close();

            // Tiến hành sinh đường đồng mức
            ContourService.ExecuteContourWorkflow(doc, minX, maxX, minY, maxY, activeCrs, interval, addLabels, indexOnly, labelSpacing, textHeight, smoothing);
        }
    }
}
