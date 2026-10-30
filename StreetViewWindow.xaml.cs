using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Web.WebView2.Core;

namespace AVMap
{
    public partial class StreetViewWindow : Window
    {
        private double _lat;
        private double _lon;
        private string _url;
        private double _cadX;
        private double _cadY;
        private string _crsName = "VN-2000";
        private double _heading = 0.0;

        public Action<double, double, double> PovChangedCallback { get; set; }
        public Action ClosedCallback { get; set; }

        private const string PovWatcherJs = @"
(function() {
    if (window._avmap_pov_watcher) return;
    window._avmap_pov_watcher = true;

    // 1. Chèn CSS vĩnh viễn chặn triệt để mọi loại Tooltip (Drag to change...), hộp Map details, và dialogs
    try {
        if (!document.getElementById('avmap-base-suppress-css')) {
            var baseStyle = document.createElement('style');
            baseStyle.id = 'avmap-base-suppress-css';
            baseStyle.innerHTML = `
                /* Chặn bong bóng Tooltip Drag to change, bong bóng trợ giúp */
                div[role=""tooltip""], .widget-tooltip, [class*=""tooltip""], [id*=""tooltip""],
                [data-tooltip], .interactive-tooltip,
                
                /* Chặn hộp popup Map details và layer card */
                [aria-label*=""Map details""], [aria-label*=""Chi tiết bản đồ""],
                #layer-card, .widget-layer-card,
                div[role=""dialog""]:not(#scene) {
                    display: none !important;
                    visibility: hidden !important;
                    opacity: 0 !important;
                    pointer-events: none !important;
                }

                /* Chặn vệt co đen 2 bên: ép Canvas và container phủ đầy 100% chiều rộng màn hình */
                html, body, #scene, .widget-scene, [class*=""scene""],
                #content-container, .gm-style,
                canvas {
                    width: 100% !important;
                    min-width: 100% !important;
                    height: 100% !important;
                    max-width: 100vw !important;
                    margin: 0 !important;
                    padding: 0 !important;
                    left: 0 !important;
                    right: 0 !important;
                    object-fit: cover !important;
                }
            `;
            document.head.appendChild(baseStyle);
        }
    } catch(e) {}

    var lastLat = 0, lastLon = 0, lastHeading = -999;

    function extractPovFromUrl(url) {
        if (!url) return null;
        var coordMatch = url.match(/@(-?\d+\.\d+),(-?\d+\.\d+)/);
        if (!coordMatch) {
            var vm = url.match(/viewpoint=(-?\d+\.\d+),(-?\d+\.\d+)/);
            if (!vm) return null;
            coordMatch = vm;
        }

        var lat = parseFloat(coordMatch[1]);
        var lon = parseFloat(coordMatch[2]);
        var heading = 0.0;

        var hm = url.match(/,(-?\d+(?:\.\d+)?)h/);
        if (hm) {
            heading = parseFloat(hm[1]);
        }

        heading = ((heading % 360) + 360) % 360;
        return { lat: lat, lon: lon, heading: heading };
    }

    function checkAndSendPov() {
        try {
            var pov = extractPovFromUrl(window.location.href);
            if (!pov) return;

            var compass = document.querySelector('.gm-compass-needle, .widget-compass-needle, [class*=""compass""] [style*=""rotate""]');
            if (compass && compass.style && compass.style.transform) {
                var rm = compass.style.transform.match(/rotate\((-?\d+(?:\.\d+)?)deg\)/);
                if (rm) {
                    var deg = parseFloat(rm[1]);
                    pov.heading = ((-deg % 360) + 360) % 360;
                }
            }

            var latDiff = Math.abs(pov.lat - lastLat);
            var lonDiff = Math.abs(pov.lon - lastLon);
            var headDiff = Math.abs(pov.heading - lastHeading);

            if (latDiff > 0.0000005 || lonDiff > 0.0000005 || headDiff > 0.5) {
                lastLat = pov.lat;
                lastLon = pov.lon;
                lastHeading = pov.heading;

                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage(JSON.stringify({
                        type: 'sv_pov',
                        lat: pov.lat,
                        lon: pov.lon,
                        heading: pov.heading
                    }));
                }
            }
        } catch(e) {}
    }

    var origReplace = history.replaceState;
    history.replaceState = function() {
        origReplace.apply(this, arguments);
        checkAndSendPov();
    };

    var origPush = history.pushState;
    history.pushState = function() {
        origPush.apply(this, arguments);
        checkAndSendPov();
    };

    // Chu kỳ 200ms: mượt mà, đồng bộ chính xác và CPU 0%
    setInterval(checkAndSendPov, 200);
})();";

        private const string CleanSurveyCssJs = @"
(function() {
    try {
        var canvases = Array.from(document.querySelectorAll('canvas'));
        if (canvases.length === 0) return;

        // 1. Tìm canvas WebGL 3D Street View lớn nhất
        var mainCanvas = canvases[0];
        var maxArea = 0;
        for (var i = 0; i < canvases.length; i++) {
            var c = canvases[i];
            var area = (c.clientWidth || c.width || 0) * (c.clientHeight || c.height || 0);
            if (area > maxArea) {
                maxArea = area;
                mainCanvas = c;
            }
        }

        // 2. Thu thập chuỗi tổ tiên (Ancestors) từ mainCanvas lên đến document.body
        var ancestors = new Set();
        var p = mainCanvas;
        while (p && p !== document.body) {
            ancestors.add(p);
            p = p.parentElement;
        }
        ancestors.add(document.body);

        // 3. Ẩn tất cả các nhánh container anh-em không thuộc chuỗi tổ tiên canvas
        // (Ẩn sạch 100% minimap, la bàn, hộp tìm kiếm, thanh tiêu đề, chân trang mà không đụng style bên trong)
        ancestors.forEach(function(anc) {
            if (!anc.children) return;
            for (var j = 0; j < anc.children.length; j++) {
                var child = anc.children[j];
                if (child !== mainCanvas && !ancestors.has(child)) {
                    if (child.tagName !== 'SCRIPT' && child.tagName !== 'STYLE' && child.tagName !== 'LINK') {
                        if (!child.hasAttribute('data-avmap-clean')) {
                            child.setAttribute('data-avmap-clean', '1');
                            child.setAttribute('data-avmap-orig-disp', child.style.display || '');
                            child.setAttribute('data-avmap-orig-vis', child.style.visibility || '');
                            child.style.setProperty('display', 'none', 'important');
                            child.style.setProperty('visibility', 'hidden', 'important');
                        }
                    }
                }
            }
        });

        // 4. Ẩn thêm các thẻ SVG mũi tên trên mặt đường
        var svgs = document.querySelectorAll('svg');
        for (var s = 0; s < svgs.length; s++) {
            var svg = svgs[s];
            if (!svg.hasAttribute('data-avmap-clean')) {
                svg.setAttribute('data-avmap-clean', '1');
                svg.setAttribute('data-avmap-orig-disp', svg.style.display || '');
                svg.setAttribute('data-avmap-orig-vis', svg.style.visibility || '');
                svg.style.setProperty('display', 'none', 'important');
                svg.style.setProperty('visibility', 'hidden', 'important');
            }
        }
    } catch(e) {}
})();";

        private const string RemoveCleanSurveyCssJs = @"
(function() {
    try {
        var hidden = document.querySelectorAll('[data-avmap-clean=""1""]');
        for (var i = 0; i < hidden.length; i++) {
            var el = hidden[i];
            var origDisp = el.getAttribute('data-avmap-orig-disp');
            if (origDisp) {
                el.style.display = origDisp;
            } else {
                el.style.removeProperty('display');
            }
            var origVis = el.getAttribute('data-avmap-orig-vis');
            if (origVis) {
                el.style.visibility = origVis;
            } else {
                el.style.removeProperty('visibility');
            }
            el.removeAttribute('data-avmap-clean');
            el.removeAttribute('data-avmap-orig-disp');
            el.removeAttribute('data-avmap-orig-vis');
        }
    } catch(e) {}
})();";

        public StreetViewWindow(double lat, double lon, string crsName, double cadX, double cadY)
        {
            InitializeComponent();
            _lat = lat;
            _lon = lon;
            _cadX = cadX;
            _cadY = cadY;
            _crsName = crsName;

            lblCoordinates.Text = $"CAD: X={cadX:F2}, Y={cadY:F2}  |  WGS84: Lat={lat:F6}, Lon={lon:F6}";
            lblCrsInfo.Text = $"Hệ tọa độ: {crsName}";

            string sLat = lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            string sLon = lon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            _url = $"https://www.google.com/maps/@?api=1&map_action=pano&viewpoint={sLat},{sLon}";

            Loaded += StreetViewWindow_Loaded;
            SizeChanged += StreetViewWindow_SizeChanged;
        }

        public void NavigateTo(double lat, double lon, string crsName, double cadX, double cadY)
        {
            _lat = lat;
            _lon = lon;
            _cadX = cadX;
            _cadY = cadY;
            _crsName = crsName;

            lblCoordinates.Text = $"CAD: X={cadX:F2}, Y={cadY:F2}  |  WGS84: Lat={lat:F6}, Lon={lon:F6}";
            lblCrsInfo.Text = $"Hệ tọa độ: {crsName}";

            string sLat = lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            string sLon = lon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            _url = $"https://www.google.com/maps/@?api=1&map_action=pano&viewpoint={sLat},{sLon}";

            if (webView?.CoreWebView2 != null)
            {
                webView.CoreWebView2.Navigate(_url);
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            Focus();
        }

        private async void StreetViewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVMap", "WebView2Cache");
                if (!Directory.Exists(userDataFolder))
                {
                    Directory.CreateDirectory(userDataFolder);
                }

                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webView.EnsureCoreWebView2Async(env);

                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(PovWatcherJs);

                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.CoreWebView2.Navigate(_url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi hiển thị cửa sổ nhúng: {ex.Message}\nĐang mở trực tiếp trên trình duyệt của bạn.", "AVMap", MessageBoxButton.OK, MessageBoxImage.Information);
                try
                {
                    Process.Start(new ProcessStartInfo(_url) { UseShellExecute = true });
                }
                catch { }
            }
        }

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                if (webView.CoreWebView2 != null)
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(PovWatcherJs);
                }
            }
            catch { }
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string jsonStr = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(jsonStr))
                {
                    jsonStr = e.WebMessageAsJson;
                }
                if (string.IsNullOrEmpty(jsonStr)) return;

                var jo = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
                if ((string)jo["type"] == "sv_pov")
                {
                    double lat = (double)jo["lat"];
                    double lon = (double)jo["lon"];
                    double heading = (double)jo["heading"];

                    _lat = lat;
                    _lon = lon;
                    _heading = heading;

                    string dirText = GetDirectionText(heading);
                    if (lblHeadingInfo != null)
                    {
                        lblHeadingInfo.Text = $"Góc nhìn: {heading:F0}° ({dirText})";
                    }
                    if (lblCoordinates != null)
                    {
                        lblCoordinates.Text = $"WGS84: {lat:F6}, {lon:F6}";
                    }

                    PovChangedCallback?.Invoke(lat, lon, heading);
                }
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            try
            {
                ClosedCallback?.Invoke();
            }
            catch { }
        }

        public static string GetDirectionText(double heading)
        {
            if (heading >= 337.5 || heading < 22.5) return "Bắc (N)";
            if (heading >= 22.5 && heading < 67.5) return "Đông Bắc (NE)";
            if (heading >= 67.5 && heading < 112.5) return "Đông (E)";
            if (heading >= 112.5 && heading < 157.5) return "Đông Nam (SE)";
            if (heading >= 157.5 && heading < 202.5) return "Nam (S)";
            if (heading >= 202.5 && heading < 247.5) return "Tây Nam (SW)";
            if (heading >= 247.5 && heading < 292.5) return "Tây (W)";
            return "Tây Bắc (NW)";
        }

        private async System.Threading.Tasks.Task ApplyCleanSurveyModeAsync()
        {
            try
            {
                if (webView.CoreWebView2 != null)
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(CleanSurveyCssJs);
                }
            }
            catch { }
        }

        private async System.Threading.Tasks.Task RemoveCleanSurveyModeAsync()
        {
            try
            {
                if (webView.CoreWebView2 != null)
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(RemoveCleanSurveyCssJs);
                }
            }
            catch { }
        }

        private void BtnAiSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new StreetViewAiSettingsWindow();
                win.Owner = this;
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi mở cài đặt: {ex.Message}", "AVMap", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnCapture_Click(object sender, RoutedEventArgs e)
        {
            await CaptureAndProcessImageAsync();
        }

        private async System.Threading.Tasks.Task CaptureAndProcessImageAsync()
        {
            try
            {
                if (webView.CoreWebView2 == null)
                {
                    MessageBox.Show("Street View chưa tải xong, vui lòng chờ trong giây lát.", "AVMap", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Lưu ảnh hiện trạng thực tế",
                    Filter = "Ảnh JPEG (*.jpg)|*.jpg|Ảnh PNG (*.png)|*.png",
                    FileName = $"AnhHienTrang_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"
                };

                if (sfd.ShowDialog() != true) return;

                if (lblAiStatus != null)
                {
                    lblAiStatus.Text = "⏳ Đang chụp ảnh góc nhìn độ nét cao...";
                }

                // 1. Tự động ẩn sạch 100% tất cả UI, thẻ thông tin, pegman, la bàn, map details, tooltip
                await ApplyCleanSurveyModeAsync();
                await System.Threading.Tasks.Task.Delay(150);

                string tempSnap = Path.Combine(Path.GetTempPath(), $"snap_{DateTime.Now.Ticks}.png");

                // 2. Chụp ảnh góc nhìn toàn màn hình Full HD 1920x1080 chuẩn 16:9 auto tràn viền
                bool capturedHd = false;
                try
                {
                    // Luôn giả lập độ phân giải toàn màn hình Full HD 1920x1080 chuẩn 16:9 sắc nét (kể cả khi đang để cửa sổ nhỏ)
                    await webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                        "Emulation.setDeviceMetricsOverride",
                        "{\"width\":1920,\"height\":1080,\"deviceScaleFactor\":1,\"mobile\":false}");

                    // Ép WebGL canvas giãn 100% tràn viền không chừa vệt đen
                    await webView.CoreWebView2.ExecuteScriptAsync(@"
                        try {
                            window.dispatchEvent(new Event('resize'));
                            var canvases = document.querySelectorAll('canvas');
                            for (var i = 0; i < canvases.length; i++) {
                                canvases[i].style.setProperty('width', '100%', 'important');
                                canvases[i].style.setProperty('min-width', '100%', 'important');
                                canvases[i].style.setProperty('left', '0px', 'important');
                                canvases[i].style.setProperty('margin', '0px', 'important');
                                canvases[i].style.setProperty('object-fit', 'cover', 'important');
                            }
                        } catch(e) {}
                    ");

                    await System.Threading.Tasks.Task.Delay(200);

                    string snapResult = await webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                        "Page.captureScreenshot",
                        "{\"format\":\"png\"}");

                    if (!string.IsNullOrEmpty(snapResult))
                    {
                        var jo = Newtonsoft.Json.Linq.JObject.Parse(snapResult);
                        string base64Data = (string)jo["data"];
                        if (!string.IsNullOrEmpty(base64Data))
                        {
                            byte[] rawBytes = Convert.FromBase64String(base64Data);
                            File.WriteAllBytes(tempSnap, rawBytes);
                            capturedHd = true;
                        }
                    }
                }
                catch { }
                finally
                {
                    try
                    {
                        await webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                            "Emulation.clearDeviceMetricsOverride", "{}");

                        // Kích hoạt ngay lập tức sự kiện resize để WebGL trải rộng 100% toàn màn hình, xóa tan vệt đen 2 bên
                        await webView.CoreWebView2.ExecuteScriptAsync(@"
                            try {
                                window.dispatchEvent(new Event('resize'));
                                var canvases = document.querySelectorAll('canvas');
                                for (var i = 0; i < canvases.length; i++) {
                                    canvases[i].style.width = '100%';
                                    canvases[i].style.height = '100%';
                                }
                            } catch(e) {}
                        ");
                    }
                    catch { }
                }

                // Dự phòng: nếu CDP không khả dụng thì chụp từ khung nhìn hiện tại
                if (!capturedHd || !File.Exists(tempSnap))
                {
                    using (var stream = new FileStream(tempSnap, FileMode.Create, FileAccess.Write))
                    {
                        await webView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
                    }
                }

                // 3. Khôi phục lại giao diện để người dùng tiếp tục thao tác bình thường
                await RemoveCleanSurveyModeAsync();

                if (!File.Exists(tempSnap))
                {
                    MessageBox.Show("Không thể chụp ảnh từ Street View.", "AVMap", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 4. Xử lý làm sạch mép đáy, xóa mờ chữ chìm © Google trên đường và xuất ảnh
                if (lblAiStatus != null)
                {
                    lblAiStatus.Text = "🪄 Đang xử lý làm sạch ảnh hiện trường nguyên bản...";
                }

                await StreetViewAiService.ProcessStreetViewImageAsync(
                    tempSnap,
                    sfd.FileName,
                    _lat,
                    _lon,
                    _heading,
                    _crsName,
                    _cadX,
                    _cadY,
                    msg => Dispatcher.Invoke(() => { if (lblAiStatus != null) lblAiStatus.Text = msg; }));

                try { File.Delete(tempSnap); } catch { }

                if (lblAiStatus != null)
                {
                    lblAiStatus.Text = "✅ Đã lưu ảnh hiện trạng & nhúng tọa độ GPS EXIF thành công!";
                }

                // Sao chép ảnh vào Clipboard
                try
                {
                    using (var bmpStream = new MemoryStream(File.ReadAllBytes(sfd.FileName)))
                    {
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.StreamSource = bmpStream;
                        bmp.EndInit();
                        Clipboard.SetImage(bmp);
                    }
                }
                catch { }

                string dirText = GetDirectionText(_heading);
                string successMsg = $"Đã chụp và lưu ảnh hiện trạng thành công!\n{sfd.FileName}\n\n" +
                    $"📍 Tọa độ GPS (WGS84): {_lat:F6}, {_lon:F6}\n" +
                    $"📐 Tọa độ VN-2000: X={_cadX:F2}, Y={_cadY:F2} ({_crsName})\n" +
                    $"👁 Góc nhìn la bàn: {_heading:F0}° ({dirText})\n\n" +
                    $"• Đã xóa sạch logo Google & chữ bản quyền.\n" +
                    $"• Đã xóa sạch vệt chữ chìm © Google trên mặt đường.\n" +
                    $"• ĐÃ NHÚNG TỌA ĐỘ GPS EXIF VÀO ẢNH: Sẵn sàng dùng lệnh AVPHOTO (Chèn ảnh khảo sát) để chèn thẳng vào CAD hoặc mở trên Google Earth, QGIS.\n" +
                    $"• Ảnh đã được copy vào Clipboard sẵn sàng dán.\n\n" +
                    $"Bạn có muốn mở xem ảnh ngay không?";

                var res = MessageBox.Show(successMsg, "AVMap - Ảnh Hiện Trạng", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (res == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
                webView.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi chụp/xử lý ảnh: {ex.Message}", "AVMap", MessageBoxButton.OK, MessageBoxImage.Error);
                if (lblAiStatus != null) lblAiStatus.Text = $"❌ Lỗi: {ex.Message}";
            }
        }

        private async void BtnToggleFullScreen_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }

            try
            {
                await System.Threading.Tasks.Task.Delay(100);
                if (webView?.CoreWebView2 != null)
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(@"
                        try {
                            window.dispatchEvent(new Event('resize'));
                            var canvases = document.querySelectorAll('canvas');
                            for (var i = 0; i < canvases.length; i++) {
                                canvases[i].style.width = '100%';
                                canvases[i].style.height = '100%';
                            }
                        } catch(e) {}
                    ");
                }
            }
            catch { }
        }

        private async void StreetViewWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (webView?.CoreWebView2 != null)
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(@"
                        try {
                            window.dispatchEvent(new Event('resize'));
                            var canvases = document.querySelectorAll('canvas');
                            for (var i = 0; i < canvases.length; i++) {
                                canvases[i].style.width = '100%';
                                canvases[i].style.height = '100%';
                            }
                        } catch(e) {}
                    ");
                }
            }
            catch { }
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            webView.CoreWebView2?.Reload();
        }

        private void BtnOpenExternal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(_url) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
