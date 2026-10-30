using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Windows;

namespace AVMap.Enji
{
    public class RibbonCommandHandler : ICommand
    {
        public bool CanExecute(object parameter) => true;
        public event EventHandler CanExecuteChanged { add { } remove { } }

        public void Execute(object parameter)
        {
            string cmd = null;
            if (parameter is string s)
            {
                cmd = s;
            }
            else if (parameter is RibbonCommandItem cmdItem)
            {
                cmd = cmdItem.CommandParameter as string;
            }

            if (!string.IsNullOrEmpty(cmd))
            {
                Document doc = Gssoft.Gscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    // Gửi ^C^C (\x03\x03) để hủy mọi lệnh đang đợi trên dòng lệnh CAD và thực thi lệnh mới
                    doc.SendStringToExecute("\x03\x03" + cmd.Trim() + "\n", true, false, false);
                }
            }
        }
    }

    public static class RibbonManager_enji
    {
        private const string TabId = "TAB_AVMAP_ENJI_MAIN";
        private static readonly RibbonCommandHandler cmdHandler = new RibbonCommandHandler();

        public static void InitializeRibbon()
        {
            try
            {
                if (ComponentManager.Ribbon == null)
                {
                    ComponentManager.ItemInitialized += ComponentManager_ItemInitialized;
                }
                else
                {
                    CreateRibbon();
                }
            }
            catch { }
        }

        private static void ComponentManager_ItemInitialized(object sender, RibbonItemEventArgs e)
        {
            if (ComponentManager.Ribbon != null)
            {
                ComponentManager.ItemInitialized -= ComponentManager_ItemInitialized;
                CreateRibbon();
            }
        }

        public static void CreateRibbon()
        {
            try
            {
                RibbonControl ribbon = ComponentManager.Ribbon;
                if (ribbon == null) return;

                foreach (var exTab in ribbon.Tabs)
                {
                    if (exTab.Id == TabId)
                    {
                        ribbon.Tabs.Remove(exTab);
                        break;
                    }
                }

                RibbonTab tab = new RibbonTab
                {
                    Title = "AVMap",
                    Id = TabId
                };
                ribbon.Tabs.Add(tab);

                // ==================== PANEL 1: HỆ TỌA ĐỘ ====================
                RibbonPanelSource pnlCrsSource = new RibbonPanelSource { Title = "Hệ Tọa Độ" };
                RibbonPanel pnlCrs = new RibbonPanel { Source = pnlCrsSource };

                var btnCrs = new RibbonButton
                {
                    Text = "Hệ Tọa Độ\nVN-2000",
                    CommandParameter = "AVCRS ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    LargeImage = IconHelper.LoadIcon("SpmSetCrs.png"),
                    Image = IconHelper.LoadIcon("SpmSetCrs16.png"),
                    ToolTip = "Chọn tỉnh thành và kinh tuyến trục VN-2000 (múi 3°/6°) cho bản vẽ"
                };
                pnlCrsSource.Items.Add(btnCrs);
                tab.Panels.Add(pnlCrs);

                // ==================== PANEL 2: BẢN ĐỒ NỀN (BACKGROUND MAP) ====================
                RibbonPanelSource pnlMapSource = new RibbonPanelSource { Title = "Bản Đồ Nền" };
                RibbonPanel pnlMap = new RibbonPanel { Source = pnlMapSource };

                var btnMapOn = new RibbonButton
                {
                    Text = "Bật Nền\n(Show)",
                    CommandParameter = "AVMAPON ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    LargeImage = IconHelper.LoadIcon("SpmBgMapShow.png"),
                    Image = IconHelper.LoadIcon("SpmBgMapShow16.png"),
                    ToolTip = "Bật tải nền Google Maps vệ tinh thời gian thực (lia chuột/zoom tự tải)"
                };

                var btnMapOff = new RibbonButton
                {
                    Text = "Ẩn Nền (Hide)",
                    CommandParameter = "AVMAPOFF ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Standard,
                    Image = IconHelper.LoadIcon("SpmBgMapHide16.png"),
                    ToolTip = "Ẩn / Tắt bản đồ nền, làm sạch bản vẽ (Lệnh: AVMAPOFF hoặc AVHIDE)"
                };

                var btnRefresh = new RibbonButton
                {
                    Text = "Làm Mới (Refresh)",
                    CommandParameter = "AVREFRESH ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Standard,
                    Image = IconHelper.LoadIcon("SpmBgMapRefresh16.png"),
                    ToolTip = "Tải lại dữ liệu ảnh vệ tinh khi đổi góc nhìn"
                };

                var btnMapType = new RibbonButton
                {
                    Text = "Chọn Nguồn Map",
                    CommandParameter = "AVMAPTYPE ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Standard,
                    Image = IconHelper.LoadIcon("SpmThematicMap16.png"),
                    ToolTip = "Đổi loại bản đồ: Google Vệ Tinh, Giao Thông, Địa Hình, Esri, OSM..."
                };

                RibbonRowPanel rowMap = new RibbonRowPanel();
                rowMap.Items.Add(btnMapOff);
                rowMap.Items.Add(new RibbonRowBreak());
                rowMap.Items.Add(btnRefresh);
                rowMap.Items.Add(new RibbonRowBreak());
                rowMap.Items.Add(btnMapType);

                pnlMapSource.Items.Add(btnMapOn);
                pnlMapSource.Items.Add(rowMap);
                tab.Panels.Add(pnlMap);

                // ==================== PANEL 3: BÌNH ĐỒ & ẢNH KHẢO SÁT ====================
                RibbonPanelSource pnlCaptureSource = new RibbonPanelSource { Title = "Bình Đồ & Ảnh Hiện Trường" };
                RibbonPanel pnlCapture = new RibbonPanel { Source = pnlCaptureSource };

                var btnCaptureBox = new RibbonButton
                {
                    Text = "Quét Vùng\nBình Đồ",
                    CommandParameter = "AVCAPTUREBOX ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    LargeImage = IconHelper.LoadIcon("SpmBgMapImage.png"),
                    Image = IconHelper.LoadIcon("SpmBgMapImage16.png"),
                    ToolTip = "Quét chọn 2 góc hình chữ nhật bao quanh tim tuyến để tải ảnh vệ tinh độ nét cao"
                };

                var btnVectorBinhDo = new RibbonButton
                {
                    Text = "Lấy Vector\nBình Đồ",
                    CommandParameter = "AVGETVECTORDATA ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    LargeImage = IconHelper.LoadIcon("SpmSpatialQuery.png"),
                    Image = IconHelper.LoadIcon("SpmSpatialQuery16.png"),
                    ToolTip = "Quét vùng chọn để tự động lấy Nhà cửa, Đường sá (kèm tên đường), Sông suối, Ao hồ vẽ thành Polyline CAD chuẩn VN-2000"
                };

                var btnContour = new RibbonButton
                {
                    Text = "Đường\nĐồng Mức",
                    CommandParameter = "AVCONTOUR ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    LargeImage = IconHelper.LoadIcon("SpmTerrain1.png"),
                    Image = IconHelper.LoadIcon("SpmTerrain1_16.png"),
                    ToolTip = "Quét vùng chọn để tự động tải mô hình độ cao vệ tinh toàn cầu (DEM) và sinh các đường đồng mức có gắn cao độ Elevation Z thực tế"
                };

                var btnInsertOrtho = new RibbonButton
                {
                    Text = "Chèn Ảnh\nBay Chụp",
                    CommandParameter = "AVINSERTORTHO ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    LargeImage = IconHelper.LoadIcon("SpmWorldFile.png"),
                    Image = IconHelper.LoadIcon("SpmWorldFile16.png"),
                    ToolTip = "Chèn ảnh trực giao bay chụp Flycam / UAV / Vệ tinh (TIF, GeoTIFF, PNG, JPG, ECW) có kèm file tọa độ thế giới (TFW, PGW, JGW) vào đúng tọa độ VN-2000"
                };

                var btnSurveyPhoto = new RibbonButton
                {
                    Text = "Chèn Ảnh\nKhảo Sát",
                    CommandParameter = "AVCHENANHKS ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    LargeImage = IconHelper.LoadIcon("SpmImportPhoto.png"),
                    Image = IconHelper.LoadIcon("SpmImportPhoto16.png"),
                    ToolTip = "Chèn hàng loạt ảnh chụp hiện trường bằng điện thoại/iPhone/GPS (kèm mũi tên Leader chỉ đúng vị trí hư hỏng mặt đường)"
                };

                pnlCaptureSource.Items.Add(btnCaptureBox);
                pnlCaptureSource.Items.Add(btnVectorBinhDo);
                pnlCaptureSource.Items.Add(btnContour);
                pnlCaptureSource.Items.Add(btnInsertOrtho);
                pnlCaptureSource.Items.Add(btnSurveyPhoto);
                tab.Panels.Add(pnlCapture);

                // ==================== PANEL 4: THỰC ĐỊA 360° ====================
                RibbonPanelSource pnlStreetSource = new RibbonPanelSource { Title = "Thực Địa 360°" };
                RibbonPanel pnlStreet = new RibbonPanel { Source = pnlStreetSource };

                var btnStreetView = new RibbonButton
                {
                    Text = "Google\nStreet View",
                    CommandParameter = "AVSTREETVIEW ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    LargeImage = IconHelper.LoadIcon("SpmStreetView.png"),
                    Image = IconHelper.LoadIcon("SpmStreetView16.png"),
                    ToolTip = "Click chọn điểm trên bình đồ để xem toàn cảnh 360°, tương tác hướng nhìn đồng bộ CAD, chụp ảnh hiện trường xóa sạch logo Google bằng AI & đóng dấu trắc địa chuyên nghiệp."
                };
                pnlStreetSource.Items.Add(btnStreetView);
                tab.Panels.Add(pnlStreet);

                // ==================== PANEL 5: GOOGLE EARTH (KMZ / KML) ====================
                RibbonPanelSource pnlKmzSource = new RibbonPanelSource { Title = "Google Earth" };
                RibbonPanel pnlKmz = new RibbonPanel { Source = pnlKmzSource };

                var btnExportKmz = new RibbonButton
                {
                    Text = "Xuất KMZ\n(Google Earth)",
                    CommandParameter = "AVEXPORTKMZ ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    LargeImage = IconHelper.LoadIcon("SpmCreateKml.png"),
                    Image = IconHelper.LoadIcon("SpmCreateKml16.png"),
                    ToolTip = "Quét chọn đối tượng CAD xuất ra file KMZ và mở xem trên Google Earth"
                };

                var btnImportKmz = new RibbonButton
                {
                    Text = "Nhập KMZ\n(Vào CAD)",
                    CommandParameter = "AVIMPORTKMZ ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    LargeImage = IconHelper.LoadIcon("SpmImport.png"),
                    Image = IconHelper.LoadIcon("SpmImport16.png"),
                    ToolTip = "Đọc file KMZ / KML từ Google Earth vẽ vào bản vẽ CAD theo VN-2000"
                };

                pnlKmzSource.Items.Add(btnExportKmz);
                pnlKmzSource.Items.Add(btnImportKmz);
                tab.Panels.Add(pnlKmz);

                // ==================== PANEL 6: TRỢ GIÚP & HƯỚNG DẪN ====================
                RibbonPanelSource pnlHelpSource = new RibbonPanelSource { Title = "Trợ Giúp" };
                RibbonPanel pnlHelp = new RibbonPanel { Source = pnlHelpSource };

                var btnHelp = new RibbonButton
                {
                    Text = "Hướng Dẫn\nSử Dụng",
                    CommandParameter = "AVHELP ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    LargeImage = IconHelper.LoadIcon("SpmHelp32.png") ?? IconHelper.LoadIcon("Logo.png"),
                    Image = IconHelper.LoadIcon("SpmHelp.png"),
                    ToolTip = "Mở bảng Hướng dẫn sử dụng, tra cứu các lệnh tắt và mẹo thao tác trên AVMap"
                };

                var btnClean = new RibbonButton
                {
                    Text = "Dọn Rác\nBộ Đệm",
                    CommandParameter = "AVCLEAN ",
                    CommandHandler = cmdHandler,
                    ShowText = true,
                    ShowImage = true,
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    LargeImage = IconHelper.LoadIcon("SpmClose.png"),
                    Image = IconHelper.LoadIcon("SpmClose16.png"),
                    ToolTip = "Quét dọn sạch sẽ toàn bộ file ảnh nền tạm và bộ nhớ đệm (Cache = 0 MB)"
                };

                pnlHelpSource.Items.Add(btnHelp);
                pnlHelpSource.Items.Add(btnClean);
                tab.Panels.Add(pnlHelp);

                tab.IsActive = true;
            }
            catch (Exception ex)
            {
                Gssoft.Gscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor?.WriteMessage($"\n[AVMap Ribbon Lỗi]: {ex.Message}");
            }
        }
    }
}
