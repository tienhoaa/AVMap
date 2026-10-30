using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Colors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AVMap
{
    public static class VectorDataService
    {
        public const string LayerRoad = "BD_DUONG_GIAO_THONG";
        public const string LayerRoadMain = "BD_DUONG_QUOC_LO";
        public const string LayerRoadName = "BD_TEN_DUONG";
        public const string LayerWater = "BD_SONG_SUOI";
        public const string LayerLake = "BD_AO_HO";
        public const string LayerWaterName = "BD_TEN_SONG_SUOI";
        public const string LayerBuilding = "BD_CONG_TRINH_CONG_CONG";
        public const string LayerHouse = "BD_NHA_DAN";
        public const string LayerFloorText = "BD_SO_TANG";
        public const string LayerBuildingName = "BD_TEN_NHA";
        public const string LayerBarrier = "BD_HANG_RAO_RANH_DAT";

        private static readonly HttpClient client = new HttpClient();
        private static Dictionary<string, string> _vnBuildingTiles;

        static VectorDataService()
        {
            client.Timeout = TimeSpan.FromSeconds(50);
            client.DefaultRequestHeaders.Add("User-Agent", "AVMap_AutoCAD_Civil/1.0 (contact: support@opencartis.com)");
        }

        public static void ExtractVectorDataCommand(Document doc)
        {
            if (doc == null) return;
            VectorWindow win = new VectorWindow(doc);
            CadApp.ShowModalWindow(win);
        }

        public static void ExecuteVectorExtractionWorkflow(
            Document doc, double minX, double maxX, double minY, double maxY, ProvinceCrs crs,
            bool getRoads, bool getBuildings, bool getAiHouses, bool getWater, bool getBarriers, bool getPoi,
            bool is3D = false, double user3DHeight = 3.5, bool addFloorText = true, int floorMode = 0, string floorPrefix = "G", double floorTextHeight = 1.5)
        {
            if (doc == null || crs == null) return;
            Editor ed = doc.Editor;

            double widthMeters = maxX - minX;
            double heightMeters = maxY - minY;

            string modeDesc = is3D ? $"3D (Khối hộp {(floorMode == 0 ? $"{user3DHeight:F1}m mặc định" : $"{user3DHeight:F1}m/tầng")})" : "2D (Mặt bằng phẳng chuẩn in)";
            string floorDesc = "Tắt";
            if (addFloorText)
            {
                if (floorMode == 0) floorDesc = "Mặc định chữ \"G\" (Có tầng sẵn điền G1, G2...)";
                else if (floorMode == 1) floorDesc = "AI tự điền theo diện tích mái (G1, G2, G3...)";
                else floorDesc = $"Toàn bộ là {(floorPrefix == "T" ? $"{floorMode - 1}T" : $"G{floorMode - 1}")}";
            }

            ed.WriteMessage($"\n=======================================================");
            ed.WriteMessage($"\n[AVMap] Đang kết nối máy chủ dữ liệu GIS & AI để bóc tách bình đồ...");
            ed.WriteMessage($"\n        Phạm vi quét: Rộng {widthMeters:N0}m x Dài {heightMeters:N0}m ({(widthMeters * heightMeters / 10000.0):N1} ha)...");
            ed.WriteMessage($"\n        Chế độ mô hình: {modeDesc} | Ký hiệu số tầng: {floorDesc}");

            var ptSW = VN2000Transform.VN2000ToWGS84(minX, minY, crs.CentralMeridian, crs.ScaleFactor);
            var ptNE = VN2000Transform.VN2000ToWGS84(maxX, maxY, crs.CentralMeridian, crs.ScaleFactor);

            double s = Math.Min(ptSW.Latitude, ptNE.Latitude);
            double w = Math.Min(ptSW.Longitude, ptNE.Longitude);
            double n = Math.Max(ptSW.Latitude, ptNE.Latitude);
            double e = Math.Max(ptSW.Longitude, ptNE.Longitude);

            // 1. Tải dữ liệu Overpass GIS
            string jsonResult = null;
            if (getRoads || getBuildings || getWater || getBarriers || getPoi)
            {
                jsonResult = Task.Run(() => QueryOverpassDataAsync(s, w, n, e, getRoads, getBuildings, getWater, getBarriers, getPoi)).GetAwaiter().GetResult();
            }

            // 2. Tải dữ liệu Nhà Dân từ Microsoft Global ML Building Footprints (AI vệ tinh)
            List<List<Point2d>> aiHouses = null;
            if (getAiHouses)
            {
                ed.WriteMessage($"\n[AVMap] Đang tải bản đồ số Nhà Dân AI (Microsoft Building Footprints)...");
                aiHouses = Task.Run(() => FetchMicrosoftBuildingsAsync(s, w, n, e, crs)).GetAwaiter().GetResult();
            }

            // 3. Phân tích cú pháp và vẽ các đối tượng vào AutoCAD
            try
            {
                DrawVectorEntities(doc, jsonResult, aiHouses, crs, minX, maxX, minY, maxY, is3D, user3DHeight, addFloorText, floorMode, floorPrefix, floorTextHeight);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[AVMap Lỗi Vẽ Vector]: {ex.Message}");
            }
        }

        private static async Task<string> QueryOverpassDataAsync(
            double s, double w, double n, double e,
            bool getRoads, bool getBuildings, bool getWater, bool getBarriers, bool getPoi)
        {
            string sS = s.ToString("F6", CultureInfo.InvariantCulture);
            string sW = w.ToString("F6", CultureInfo.InvariantCulture);
            string sN = n.ToString("F6", CultureInfo.InvariantCulture);
            string sE = e.ToString("F6", CultureInfo.InvariantCulture);

            var sbParts = new StringBuilder();
            if (getRoads)
            {
                sbParts.AppendLine($"  way[\"highway\"]({sS},{sW},{sN},{sE});");
            }
            if (getBuildings)
            {
                sbParts.AppendLine($"  way[\"building\"]({sS},{sW},{sN},{sE});");
                sbParts.AppendLine($"  way[\"building:part\"]({sS},{sW},{sN},{sE});");
                sbParts.AppendLine($"  relation[\"building\"]({sS},{sW},{sN},{sE});");
                sbParts.AppendLine($"  relation[\"type\"=\"multipolygon\"][\"building\"]({sS},{sW},{sN},{sE});");
            }
            if (getWater)
            {
                sbParts.AppendLine($"  way[\"waterway\"]({sS},{sW},{sN},{sE});");
                sbParts.AppendLine($"  way[\"natural\"=\"water\"]({sS},{sW},{sN},{sE});");
                sbParts.AppendLine($"  relation[\"natural\"=\"water\"]({sS},{sW},{sN},{sE});");
                sbParts.AppendLine($"  way[\"water\"]({sS},{sW},{sN},{sE});");
                sbParts.AppendLine($"  way[\"landuse\"=\"basin\"]({sS},{sW},{sN},{sE});");
                sbParts.AppendLine($"  way[\"landuse\"=\"reservoir\"]({sS},{sW},{sN},{sE});");
            }
            if (getBarriers)
            {
                sbParts.AppendLine($"  way[\"barrier\"]({sS},{sW},{sN},{sE});");
            }
            if (getPoi)
            {
                sbParts.AppendLine($"  way[\"amenity\"]({sS},{sW},{sN},{sE});");
                sbParts.AppendLine($"  way[\"shop\"]({sS},{sW},{sN},{sE});");
                sbParts.AppendLine($"  node[\"amenity\"]({sS},{sW},{sN},{sE});");
                sbParts.AppendLine($"  node[\"shop\"]({sS},{sW},{sN},{sE});");
                sbParts.AppendLine($"  node[\"tourism\"]({sS},{sW},{sN},{sE});");
            }

            if (sbParts.Length == 0) return null;

            string query = $@"[out:json][timeout:45];
(
{sbParts}
);
out body geom qt;";

            string[] endpoints = new[]
            {
                "https://overpass.openstreetmap.fr/api/interpreter",
                "https://overpass-api.de/api/interpreter",
                "https://overpass.kumi.systems/api/interpreter",
                "https://maps.mail.ru/osm/tools/overpass/api/interpreter"
            };

            foreach (var url in endpoints)
            {
                try
                {
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("data", query)
                    });

                    using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(35)))
                    {
                        var response = await client.PostAsync(url, content, cts.Token);
                        if (response.IsSuccessStatusCode)
                        {
                            string json = await response.Content.ReadAsStringAsync();
                            if (!string.IsNullOrEmpty(json) && json.Contains("\"elements\""))
                            {
                                return json;
                            }
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        private static async Task<List<List<Point2d>>> FetchMicrosoftBuildingsAsync(double south, double west, double north, double east, ProvinceCrs crs)
        {
            var result = new List<List<Point2d>>();
            try
            {
                int zoom = 9;
                var (t1X, t1Y) = VN2000Transform.LatLonToTile(north, west, zoom);
                var (t2X, t2Y) = VN2000Transform.LatLonToTile(south, east, zoom);

                int minTX = Math.Min(t1X, t2X);
                int maxTX = Math.Max(t1X, t2X);
                int minTY = Math.Min(t1Y, t2Y);
                int maxTY = Math.Max(t1Y, t2Y);

                var tileDict = GetVnBuildingTiles();
                if (tileDict == null || tileDict.Count == 0) return result;

                string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AVMap", "BuildingCache");
                if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

                for (int ty = minTY; ty <= maxTY; ty++)
                {
                    for (int tx = minTX; tx <= maxTX; tx++)
                    {
                        string qk = TileToQuadKey(tx, ty, zoom);
                        if (!tileDict.TryGetValue(qk, out string url) || string.IsNullOrEmpty(url)) continue;

                        string localGz = Path.Combine(cacheDir, $"{qk}.csv.gz");
                        if (!File.Exists(localGz) || new FileInfo(localGz).Length == 0)
                        {
                            try
                            {
                                byte[] data = await client.GetByteArrayAsync(url);
                                File.WriteAllBytes(localGz, data);
                            }
                            catch { continue; }
                        }

                        if (!File.Exists(localGz)) continue;

                        using (var fs = File.OpenRead(localGz))
                        using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                        using (var sr = new StreamReader(gz, Encoding.UTF8))
                        {
                            string line;
                            while ((line = await sr.ReadLineAsync()) != null)
                            {
                                if (string.IsNullOrWhiteSpace(line)) continue;
                                try
                                {
                                    JObject feat = JObject.Parse(line);
                                    JArray coords = feat["geometry"]?["coordinates"]?[0] as JArray;
                                    if (coords == null || coords.Count < 3) continue;

                                    bool anyInside = false;
                                    List<Point2d> cadPts = new List<Point2d>(coords.Count);
                                    foreach (JArray pt in coords)
                                    {
                                        double lon = (double)pt[0];
                                        double lat = (double)pt[1];
                                        if (!anyInside && lon >= west && lon <= east && lat >= south && lat <= north)
                                        {
                                            anyInside = true;
                                        }
                                        var cadPt = VN2000Transform.WGS84ToVN2000(lat, lon, crs.CentralMeridian, crs.ScaleFactor);
                                        cadPts.Add(new Point2d(cadPt.X, cadPt.Y));
                                    }

                                    if (anyInside && cadPts.Count >= 3)
                                    {
                                        result.Add(cadPts);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        private static string TileToQuadKey(int tx, int ty, int zoom)
        {
            StringBuilder qk = new StringBuilder();
            for (int i = zoom; i > 0; i--)
            {
                char digit = '0';
                int mask = 1 << (i - 1);
                if ((tx & mask) != 0) digit++;
                if ((ty & mask) != 0) { digit++; digit++; }
                qk.Append(digit);
            }
            return qk.ToString();
        }

        private static Dictionary<string, string> GetVnBuildingTiles()
        {
            if (_vnBuildingTiles != null) return _vnBuildingTiles;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                string resName = null;
                foreach (string name in asm.GetManifestResourceNames())
                {
                    if (name.EndsWith("vn_building_tiles.json", StringComparison.OrdinalIgnoreCase))
                    {
                        resName = name;
                        break;
                    }
                }

                if (resName != null)
                {
                    using (var stream = asm.GetManifestResourceStream(resName))
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        _vnBuildingTiles = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (_vnBuildingTiles != null && _vnBuildingTiles.Count > 0) return _vnBuildingTiles;
                    }
                }

                string dllDir = Path.GetDirectoryName(asm.Location);
                string diskPath = Path.Combine(dllDir, "vn_building_tiles.json");
                if (File.Exists(diskPath))
                {
                    string json = File.ReadAllText(diskPath, Encoding.UTF8);
                    _vnBuildingTiles = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (_vnBuildingTiles != null && _vnBuildingTiles.Count > 0) return _vnBuildingTiles;
                }
            }
            catch { }

            _vnBuildingTiles = new Dictionary<string, string>();
            return _vnBuildingTiles;
        }

        private static void DrawVectorEntities(
            Document doc, string json, List<List<Point2d>> aiHouses, ProvinceCrs crs,
            double minCadX, double maxCadX, double minCadY, double maxCadY,
            bool is3D, double user3DHeight, bool addFloorText, int floorMode, string floorPrefix, double floorTextHeight)
        {
            Editor ed = doc.Editor;
            Database db = doc.Database;

            int roadCount = 0;
            int buildingCount = 0;
            int aiHouseCount = 0;
            int lakeCount = 0;
            int waterCount = 0;
            int barrierCount = 0;
            int floorLabelCount = 0;

            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                EnsureLayers(db, tr);

                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // 1. VẼ DỮ LIỆU NHÀ DÂN AI (MICROSOFT BUILDING FOOTPRINTS)
                if (aiHouses != null && aiHouses.Count > 0)
                {
                    foreach (var cadPoints in aiHouses)
                    {
                        if (cadPoints.Count < 3) continue;

                        int floors = 1;
                        if (floorMode == 1) // AI tự động theo diện tích mái
                        {
                            double area = ComputePolygonArea(cadPoints);
                            if (area <= 65) floors = 3;       // Nhà ống, nhà phố hẹp
                            else if (area <= 130) floors = 2;  // Nhà phố / biệt thự 2 tầng
                            else if (area <= 280) floors = 1;  // Nhà cấp 4 vườn nông thôn rộng
                            else floors = 3;                   // Nhà xưởng / trụ sở
                        }
                        else if (floorMode == 2) floors = 1;
                        else if (floorMode == 3) floors = 2;
                        else if (floorMode == 4) floors = 3;
                        else floors = 1; // floorMode == 0: Mặc định chữ G

                        // Chiều cao đùn 3D
                        double wallHeight = 0.0;
                        if (is3D)
                        {
                            // Nếu mặc định G -> đùn 1 khối chuẩn theo user3DHeight
                            // Nếu AI tự điền hoặc chọn số tầng -> đùn theo số tầng * user3DHeight
                            wallHeight = (floorMode == 0) ? user3DHeight : (floors * user3DHeight);
                        }

                        Polyline pl = new Polyline(cadPoints.Count);
                        for (int i = 0; i < cadPoints.Count; i++)
                        {
                            pl.AddVertexAt(i, cadPoints[i], 0, 0, 0);
                        }
                        pl.Closed = true;
                        pl.Layer = LayerHouse;
                        pl.Thickness = wallHeight;

                        btr.AppendEntity(pl);
                        tr.AddNewlyCreatedDBObject(pl, true);
                        aiHouseCount++;

                        if (addFloorText)
                        {
                            string floorTag = "G";
                            if (floorMode == 0)
                            {
                                floorTag = "G"; // Mặc định cứ G hết để kỹ sư tự điền
                            }
                            else
                            {
                                floorTag = (floorPrefix == "T") ? $"{floors}T" : $"G{floors}";
                            }

                            Point2d center = ComputeCentroid(cadPoints);
                            CreateFloorLabel(btr, tr, db, center, floorTag, LayerFloorText, floorTextHeight, is3D ? wallHeight : 0.0);
                            floorLabelCount++;
                        }
                    }
                }

                // 2. VẼ DỮ LIỆU TỪ OVERPASS GIS (ĐƯỜNG, NƯỚC, CÔNG TRÌNH CÔNG CỘNG, BARRIER)
                if (!string.IsNullOrEmpty(json))
                {
                    JObject root = JObject.Parse(json);
                    JArray elements = root["elements"] as JArray;

                    if (elements != null)
                    {
                        foreach (JObject item in elements)
                        {
                            JObject tags = item["tags"] as JObject;
                            if (tags == null) continue;

                            List<List<Point2d>> allGeometries = new List<List<Point2d>>();
                            if (item["geometry"] is JArray gArr && gArr.Count >= 2)
                            {
                                List<Point2d> pts = new List<Point2d>();
                                foreach (JObject pt in gArr)
                                {
                                    double lat = (double)pt["lat"];
                                    double lon = (double)pt["lon"];
                                    var cadPt = VN2000Transform.WGS84ToVN2000(lat, lon, crs.CentralMeridian, crs.ScaleFactor);
                                    pts.Add(new Point2d(cadPt.X, cadPt.Y));
                                }
                                if (pts.Count >= 2) allGeometries.Add(pts);
                            }
                            else if (item["members"] is JArray mArr)
                            {
                                foreach (JObject member in mArr)
                                {
                                    string role = member["role"]?.ToString();
                                    if (role == "inner") continue;
                                    if (member["geometry"] is JArray mgArr && mgArr.Count >= 2)
                                    {
                                        List<Point2d> pts = new List<Point2d>();
                                        foreach (JObject pt in mgArr)
                                        {
                                            double lat = (double)pt["lat"];
                                            double lon = (double)pt["lon"];
                                            var cadPt = VN2000Transform.WGS84ToVN2000(lat, lon, crs.CentralMeridian, crs.ScaleFactor);
                                            pts.Add(new Point2d(cadPt.X, cadPt.Y));
                                        }
                                        if (pts.Count >= 2) allGeometries.Add(pts);
                                    }
                                }
                            }

                            if (allGeometries.Count == 0) continue;

                            foreach (var cadPoints in allGeometries)
                            {
                                if (cadPoints.Count < 2) continue;

                                Point2d center = ComputeCentroid(cadPoints);

                                // 2.1 CÔNG TRÌNH KIẾN TRÚC & CƠ QUAN CÔNG CỘNG (BUILDING / POI)
                                if (tags["building"] != null || tags["building:part"] != null || tags["amenity"] != null || tags["shop"] != null)
                                {
                                    bool hasKnownFloors = false;
                                    int bFloors = 1;

                                    if (tags["building:levels"] != null && double.TryParse(tags["building:levels"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double lvlVal) && lvlVal > 0)
                                    {
                                        bFloors = (int)Math.Max(1, Math.Round(lvlVal));
                                        hasKnownFloors = true;
                                    }
                                    else if (tags["height"] != null && double.TryParse(tags["height"].ToString().Replace("m", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double hVal) && hVal > 0)
                                    {
                                        bFloors = (int)Math.Max(1, Math.Round(hVal / user3DHeight));
                                        hasKnownFloors = true;
                                    }
                                    else
                                    {
                                        if (floorMode == 1) // AI tự điền
                                        {
                                            double bArea = ComputePolygonArea(cadPoints);
                                            bFloors = bArea > 250 ? 3 : 2;
                                        }
                                        else if (floorMode == 2) bFloors = 1;
                                        else if (floorMode == 3) bFloors = 2;
                                        else if (floorMode == 4) bFloors = 3;
                                        else bFloors = 1;
                                    }

                                    double bHeight = 0.0;
                                    if (is3D)
                                    {
                                        if (tags["height"] != null && double.TryParse(tags["height"].ToString().Replace("m", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double exactH) && exactH > 0)
                                        {
                                            bHeight = exactH;
                                        }
                                        else
                                        {
                                            bHeight = (!hasKnownFloors && floorMode == 0) ? user3DHeight : (bFloors * user3DHeight);
                                        }
                                    }

                                    Polyline pl = new Polyline(cadPoints.Count);
                                    for (int i = 0; i < cadPoints.Count; i++)
                                    {
                                        pl.AddVertexAt(i, cadPoints[i], 0, 0, 0);
                                    }
                                    pl.Closed = true;
                                    pl.Layer = LayerBuilding;
                                    pl.Thickness = bHeight;

                                    btr.AppendEntity(pl);
                                    tr.AddNewlyCreatedDBObject(pl, true);
                                    buildingCount++;

                                    string bName = tags["name"]?.ToString()?.Trim();

                                    if (addFloorText)
                                    {
                                        string floorTag = "G";
                                        if (hasKnownFloors)
                                        {
                                            // Đã có số tầng sẵn trong dữ liệu GIS -> luôn điền chuẩn số tầng!
                                            floorTag = (floorPrefix == "T") ? $"{bFloors}T" : $"G{bFloors}";
                                        }
                                        else
                                        {
                                            // Chưa có số tầng trong dữ liệu
                                            if (floorMode == 0)
                                            {
                                                floorTag = "G"; // Mặc định cứ G hết
                                            }
                                            else
                                            {
                                                floorTag = (floorPrefix == "T") ? $"{bFloors}T" : $"G{bFloors}";
                                            }
                                        }

                                        CreateFloorLabel(btr, tr, db, center, floorTag, LayerFloorText, floorTextHeight, is3D ? bHeight : 0.0);
                                        floorLabelCount++;
                                    }

                                    if (!string.IsNullOrEmpty(bName))
                                    {
                                        try
                                        {
                                            Point3d ptPos = new Point3d(center.X, center.Y - (floorTextHeight * 1.5), is3D ? bHeight : 0.0);
                                            DBText txt = new DBText();
                                            txt.SetDatabaseDefaults();
                                            txt.TextString = bName;
                                            txt.Height = 2.0;
                                            txt.Layer = LayerBuildingName;
                                            txt.HorizontalMode = TextHorizontalMode.TextCenter;
                                            txt.VerticalMode = TextVerticalMode.TextVerticalMid;
                                            txt.Position = ptPos;
                                            txt.AlignmentPoint = ptPos;
                                            txt.AdjustAlignment(db);

                                            btr.AppendEntity(txt);
                                            tr.AddNewlyCreatedDBObject(txt, true);
                                        }
                                        catch { }
                                    }
                                }
                                // 2.2 MẠNG LƯỚI ĐƯỜNG GIAO THÔNG (HIGHWAY)
                                else if (tags["highway"] != null)
                                {
                                    string hwType = tags["highway"].ToString();
                                    bool isMainRoad = (hwType == "motorway" || hwType == "trunk" || hwType == "primary" || hwType == "secondary");

                                    Polyline pl = new Polyline(cadPoints.Count);
                                    for (int i = 0; i < cadPoints.Count; i++)
                                    {
                                        pl.AddVertexAt(i, cadPoints[i], 0, 0, 0);
                                    }
                                    pl.Layer = isMainRoad ? LayerRoadMain : LayerRoad;
                                    pl.ConstantWidth = isMainRoad ? 0.35 : 0.15;

                                    btr.AppendEntity(pl);
                                    tr.AddNewlyCreatedDBObject(pl, true);
                                    roadCount++;

                                    string roadName = tags["name"]?.ToString()?.Trim();
                                    if (!string.IsNullOrEmpty(roadName) && cadPoints.Count >= 2)
                                    {
                                        CreateAlignedLabel(btr, tr, db, cadPoints, roadName, LayerRoadName, 2.5);
                                    }
                                }
                                // 2.3 AO HỒ MẶT NƯỚC (WATER / LAKE)
                                else if (tags["natural"]?.ToString() == "water" || tags["water"] != null || tags["landuse"]?.ToString() == "basin" || tags["landuse"]?.ToString() == "reservoir")
                                {
                                    Polyline pl = new Polyline(cadPoints.Count);
                                    for (int i = 0; i < cadPoints.Count; i++)
                                    {
                                        pl.AddVertexAt(i, cadPoints[i], 0, 0, 0);
                                    }
                                    pl.Closed = true;
                                    pl.Layer = LayerLake;

                                    btr.AppendEntity(pl);
                                    tr.AddNewlyCreatedDBObject(pl, true);
                                    lakeCount++;

                                    string lakeName = tags["name"]?.ToString()?.Trim();
                                    if (!string.IsNullOrEmpty(lakeName))
                                    {
                                        try
                                        {
                                            Point3d ptPos = new Point3d(center.X, center.Y, 0);
                                            DBText txt = new DBText();
                                            txt.SetDatabaseDefaults();
                                            txt.TextString = lakeName;
                                            txt.Height = 2.5;
                                            txt.Layer = LayerWaterName;
                                            txt.HorizontalMode = TextHorizontalMode.TextCenter;
                                            txt.VerticalMode = TextVerticalMode.TextVerticalMid;
                                            txt.Position = ptPos;
                                            txt.AlignmentPoint = ptPos;
                                            txt.AdjustAlignment(db);

                                            btr.AppendEntity(txt);
                                            tr.AddNewlyCreatedDBObject(txt, true);
                                        }
                                        catch { }
                                    }
                                }
                                // 2.4 SÔNG SUỐI KÊNH RẠCH (WATERWAY)
                                else if (tags["waterway"] != null)
                                {
                                    Polyline pl = new Polyline(cadPoints.Count);
                                    for (int i = 0; i < cadPoints.Count; i++)
                                    {
                                        pl.AddVertexAt(i, cadPoints[i], 0, 0, 0);
                                    }
                                    pl.Layer = LayerWater;
                                    pl.ConstantWidth = 0.25;

                                    btr.AppendEntity(pl);
                                    tr.AddNewlyCreatedDBObject(pl, true);
                                    waterCount++;

                                    string waterName = tags["name"]?.ToString()?.Trim();
                                    if (!string.IsNullOrEmpty(waterName) && cadPoints.Count >= 2)
                                    {
                                        CreateAlignedLabel(btr, tr, db, cadPoints, waterName, LayerWaterName, 2.5);
                                    }
                                }
                                // 2.5 HÀNG RÀO & RANH ĐẤT (BARRIER)
                                else if (tags["barrier"] != null)
                                {
                                    Polyline pl = new Polyline(cadPoints.Count);
                                    for (int i = 0; i < cadPoints.Count; i++)
                                    {
                                        pl.AddVertexAt(i, cadPoints[i], 0, 0, 0);
                                    }
                                    pl.Layer = LayerBarrier;

                                    btr.AppendEntity(pl);
                                    tr.AddNewlyCreatedDBObject(pl, true);
                                    barrierCount++;
                                }
                            }
                        }
                    }
                }

                tr.Commit();
            }

            ed.UpdateScreen();
            ed.WriteMessage($"\n=======================================================");
            ed.WriteMessage($"\n  BÓC TÁCH BÌNH ĐỒ VECTOR GIS & NHÀ DÂN THÀNH CÔNG!");
            ed.WriteMessage($"\n  ✔ Mạng lưới đường giao thông:      {roadCount} tuyến đường");
            ed.WriteMessage($"\n  ✔ Nhà dân AI (Microsoft Footprints):{aiHouseCount} căn nhà ({(is3D ? "Khối 3D" : "Mặt bằng 2D")})");
            string tagSample = (floorMode == 0) ? "G (và G1, G2.. khi có sẵn)" : ((floorPrefix == "T") ? "1T, 2T..." : "G1, G2...");
            ed.WriteMessage($"\n  ✔ Nhãn ký hiệu số tầng ({tagSample}):  {floorLabelCount} nhãn tại tâm nhà");
            ed.WriteMessage($"\n  ✔ Công trình công cộng & Cơ quan:   {buildingCount} khối nhà");
            ed.WriteMessage($"\n  ✔ Sông ngòi & Kênh mương thủy hệ:  {waterCount} đoạn");
            ed.WriteMessage($"\n  ✔ Hồ nước, ao đầm:                 {lakeCount} mặt nước");
            ed.WriteMessage($"\n  ✔ Ranh đất & Hàng rào:             {barrierCount} đoạn");
            ed.WriteMessage($"\n=======================================================\n");
        }

        private static void CreateFloorLabel(BlockTableRecord btr, Transaction tr, Database db, Point2d center, string text, string layer, double height, double zPos = 0)
        {
            try
            {
                Point3d ptPos = new Point3d(center.X, center.Y, zPos);
                DBText txt = new DBText();
                txt.SetDatabaseDefaults();
                txt.TextString = text;
                txt.Height = height;
                txt.Layer = layer;
                txt.HorizontalMode = TextHorizontalMode.TextCenter;
                txt.VerticalMode = TextVerticalMode.TextVerticalMid;
                txt.Position = ptPos;
                txt.AlignmentPoint = ptPos;
                txt.AdjustAlignment(db);

                btr.AppendEntity(txt);
                tr.AddNewlyCreatedDBObject(txt, true);
            }
            catch { }
        }

        private static double ComputePolygonArea(List<Point2d> pts)
        {
            if (pts == null || pts.Count < 3) return 0;
            double area = 0;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                area += (pts[j].X + pts[i].X) * (pts[j].Y - pts[i].Y);
            }
            return Math.Abs(area / 2.0);
        }

        private static void CreateAlignedLabel(BlockTableRecord btr, Transaction tr, Database db, List<Point2d> points, string text, string layer, double height)
        {
            if (points == null || points.Count < 2 || string.IsNullOrEmpty(text)) return;

            int midIdx = points.Count / 2;
            Point2d p1 = points[midIdx - 1];
            Point2d p2 = points[midIdx];

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1.0) return;

            double angle = Math.Atan2(dy, dx);
            if (angle > Math.PI / 2.0) angle -= Math.PI;
            else if (angle < -Math.PI / 2.0) angle += Math.PI;

            Point3d pos = new Point3d((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0, 0);

            try
            {
                DBText dbt = new DBText();
                dbt.SetDatabaseDefaults();
                dbt.TextString = text;
                dbt.Height = height;
                dbt.Layer = layer;
                dbt.Rotation = angle;
                dbt.HorizontalMode = TextHorizontalMode.TextCenter;
                dbt.VerticalMode = TextVerticalMode.TextVerticalMid;
                dbt.Position = pos;
                dbt.AlignmentPoint = pos;
                dbt.AdjustAlignment(db);

                btr.AppendEntity(dbt);
                tr.AddNewlyCreatedDBObject(dbt, true);
            }
            catch { }
        }

        private static Point2d ComputeCentroid(List<Point2d> pts)
        {
            double sumX = 0, sumY = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                sumX += pts[i].X;
                sumY += pts[i].Y;
            }
            return new Point2d(sumX / pts.Count, sumY / pts.Count);
        }

        private static void EnsureLayers(Database db, Transaction tr)
        {
            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

            CreateOrUpdateLayer(lt, tr, LayerRoadMain, Color.FromColorIndex(ColorMethod.ByAci, 1));     // Red
            CreateOrUpdateLayer(lt, tr, LayerRoad, Color.FromColorIndex(ColorMethod.ByAci, 2));         // Yellow
            CreateOrUpdateLayer(lt, tr, LayerRoadName, Color.FromColorIndex(ColorMethod.ByAci, 4));     // Cyan
            CreateOrUpdateLayer(lt, tr, LayerWater, Color.FromColorIndex(ColorMethod.ByAci, 140));      // Blue
            CreateOrUpdateLayer(lt, tr, LayerLake, Color.FromColorIndex(ColorMethod.ByAci, 140));       // Blue
            CreateOrUpdateLayer(lt, tr, LayerWaterName, Color.FromColorIndex(ColorMethod.ByAci, 150));  // Light Blue
            CreateOrUpdateLayer(lt, tr, LayerBuilding, Color.FromColorIndex(ColorMethod.ByAci, 30));    // Orange
            CreateOrUpdateLayer(lt, tr, LayerHouse, Color.FromColorIndex(ColorMethod.ByAci, 32));       // Warm Terracotta / Nâu cam gạch
            CreateOrUpdateLayer(lt, tr, LayerFloorText, Color.FromColorIndex(ColorMethod.ByAci, 3));   // Green / Số tầng G1, G2
            CreateOrUpdateLayer(lt, tr, LayerBuildingName, Color.FromColorIndex(ColorMethod.ByAci, 3)); // Green
            CreateOrUpdateLayer(lt, tr, LayerBarrier, Color.FromColorIndex(ColorMethod.ByAci, 8));      // Gray
        }

        private static void CreateOrUpdateLayer(LayerTable lt, Transaction tr, string layerName, Color color)
        {
            if (!lt.Has(layerName))
            {
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = layerName,
                    Color = color,
                    IsOff = false,
                    IsFrozen = false,
                    IsLocked = false
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }
    }
}
