using System;

namespace AVMap
{
    public class LatLonPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public LatLonPoint(double lat, double lon)
        {
            Latitude = lat;
            Longitude = lon;
        }
    }

    public class CadPoint
    {
        public double X { get; set; }
        public double Y { get; set; }

        public CadPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    public static class VN2000Transform
    {
        // Elipsoid WGS-84 / VN-2000
        private const double a = 6378137.0;
        private const double f = 1.0 / 298.257223563;
        private const double b = 6356752.314245;
        private static readonly double e2 = 2 * f - f * f;
        private static readonly double ePrime2 = e2 / (1.0 - e2);

        // 7 tham số chuyển dịch VN-2000 sang WGS-84 (Bộ Tài nguyên & Môi trường)
        private const double dX = -191.90441429;
        private const double dY = -39.30318279;
        private const double dZ = -111.45032835;
        private const double rx = -0.00928836 * Math.PI / (180.0 * 3600.0);
        private const double ry = 0.03302838 * Math.PI / (180.0 * 3600.0);
        private const double rz = -0.02431627 * Math.PI / (180.0 * 3600.0);
        private const double scale = -1.09e-6; // ppm

        /// <summary>
        /// Chuyển đổi tọa độ VN-2000 (X, Y) sang WGS-84 (Lat, Lon)
        /// Lưu ý trong trắc địa Việt Nam:
        /// X là Bắc (Northing, thường ~ 1-2 triệu m), Y là Đông (Easting, thường ~ 500.000m)
        /// Trong AutoCAD: trục X màn hình là Easting (Y trắc địa), trục Y màn hình là Northing (X trắc địa)
        /// </summary>
        public static LatLonPoint VN2000ToWGS84(double cadX, double cadY, double centralMeridianDeg, double scaleFactor = 0.9999)
        {
            // Trong CAD: cadX = Easting, cadY = Northing
            double easting = cadX;
            double northing = cadY;

            // 1. Phép chiếu ngược Transverse Mercator (VN-2000 X,Y -> B, L trắc địa)
            double x0 = 500000.0; // False Easting
            double y0 = 0.0;      // False Northing

            double x_norm = (easting - x0) / scaleFactor;
            double y_norm = (northing - y0) / scaleFactor;

            double m = y_norm;
            double mu = m / (a * (1.0 - e2 / 4.0 - 3.0 * e2 * e2 / 64.0 - 5.0 * e2 * e2 * e2 / 256.0));
            double e1 = (1.0 - Math.Sqrt(1.0 - e2)) / (1.0 + Math.Sqrt(1.0 - e2));

            double phi1 = mu + (3.0 * e1 / 2.0 - 27.0 * Math.Pow(e1, 3) / 32.0) * Math.Sin(2.0 * mu)
                             + (21.0 * e1 * e1 / 16.0 - 55.0 * Math.Pow(e1, 4) / 32.0) * Math.Sin(4.0 * mu)
                             + (151.0 * Math.Pow(e1, 3) / 96.0) * Math.Sin(6.0 * mu);

            double n1 = a / Math.Sqrt(1.0 - e2 * Math.Sin(phi1) * Math.Sin(phi1));
            double t1 = Math.Tan(phi1) * Math.Tan(phi1);
            double c1 = ePrime2 * Math.Cos(phi1) * Math.Cos(phi1);
            double r1 = a * (1.0 - e2) / Math.Pow(1.0 - e2 * Math.Sin(phi1) * Math.Sin(phi1), 1.5);
            double d = x_norm / n1;

            double lat = phi1 - (n1 * Math.Tan(phi1) / r1) * (d * d / 2.0 - (5.0 + 3.0 * t1 + 10.0 * c1 - 4.0 * c1 * c1 - 9.0 * ePrime2) * Math.Pow(d, 4) / 24.0
                                + (61.0 + 90.0 * t1 + 298.0 * c1 + 45.0 * t1 * t1 - 252.0 * ePrime2 - 3.0 * c1 * c1) * Math.Pow(d, 6) / 720.0);
            double lon = (d - (1.0 + 2.0 * t1 + c1) * Math.Pow(d, 3) / 6.0
                            + (5.0 - 2.0 * c1 + 28.0 * t1 - 3.0 * c1 * c1 + 8.0 * ePrime2 + 24.0 * t1 * t1) * Math.Pow(d, 5) / 120.0) / Math.Cos(phi1);

            double latDegVN = lat * 180.0 / Math.PI;
            double lonDegVN = centralMeridianDeg + (lon * 180.0 / Math.PI);

            // 2. Chuyển đổi 7 tham số từ Elipsoid VN-2000 sang WGS-84
            return HelmertVN2000ToWGS84(latDegVN, lonDegVN, 0.0);
        }

        /// <summary>
        /// Chuyển đổi tọa độ WGS-84 (Lat, Lon) sang VN-2000 CAD (cadX = Easting, cadY = Northing)
        /// </summary>
        public static CadPoint WGS84ToVN2000(double latDeg, double lonDeg, double centralMeridianDeg, double scaleFactor = 0.9999)
        {
            // 1. Chuyển dịch 7 tham số ngược WGS-84 -> VN-2000
            var vnGeo = HelmertWGS84ToVN2000(latDeg, lonDeg, 0.0);
            double latRad = vnGeo.Latitude * Math.PI / 180.0;
            double lonRad = (vnGeo.Longitude - centralMeridianDeg) * Math.PI / 180.0;

            // 2. Phép chiếu thuận Transverse Mercator
            double n = a / Math.Sqrt(1.0 - e2 * Math.Sin(latRad) * Math.Sin(latRad));
            double t = Math.Tan(latRad) * Math.Tan(latRad);
            double c = ePrime2 * Math.Cos(latRad) * Math.Cos(latRad);
            double aVal = Math.Cos(latRad) * lonRad;

            double m = a * ((1.0 - e2 / 4.0 - 3.0 * e2 * e2 / 64.0 - 5.0 * e2 * e2 * e2 / 256.0) * latRad
                           - (3.0 * e2 / 8.0 + 3.0 * e2 * e2 / 32.0 + 45.0 * e2 * e2 * e2 / 1024.0) * Math.Sin(2.0 * latRad)
                           + (15.0 * e2 * e2 / 256.0 + 45.0 * e2 * e2 * e2 / 1024.0) * Math.Sin(4.0 * latRad)
                           - (35.0 * e2 * e2 * e2 / 3072.0) * Math.Sin(6.0 * latRad));

            double x_norm = scaleFactor * (m + n * Math.Tan(latRad) * (aVal * aVal / 2.0 + (5.0 - t + 9.0 * c + 4.0 * c * c) * Math.Pow(aVal, 4) / 24.0
                            + (61.0 - 58.0 * t + t * t + 600.0 * c - 330.0 * ePrime2) * Math.Pow(aVal, 6) / 720.0));
            double y_norm = scaleFactor * n * (aVal + (1.0 - t + c) * Math.Pow(aVal, 3) / 6.0
                            + (5.0 - 18.0 * t + t * t + 72.0 * c - 58.0 * ePrime2) * Math.Pow(aVal, 5) / 120.0);

            double cadX = 500000.0 + y_norm; // Easting
            double cadY = x_norm;            // Northing

            return new CadPoint(cadX, cadY);
        }

        private static LatLonPoint HelmertVN2000ToWGS84(double bDeg, double lDeg, double h)
        {
            double bRad = bDeg * Math.PI / 180.0;
            double lRad = lDeg * Math.PI / 180.0;

            double n = a / Math.Sqrt(1.0 - e2 * Math.Sin(bRad) * Math.Sin(bRad));
            double x = (n + h) * Math.Cos(bRad) * Math.Cos(lRad);
            double y = (n + h) * Math.Cos(bRad) * Math.Sin(lRad);
            double z = (n * (1.0 - e2) + h) * Math.Sin(bRad);

            // Biến đổi 7 tham số Helmert (Bursa-Wolf)
            double xW = (1.0 + scale) * (x + rz * y - ry * z) + dX;
            double yW = (1.0 + scale) * (-rz * x + y + rx * z) + dY;
            double zW = (1.0 + scale) * (ry * x - rx * y + z) + dZ;

            // Chuyển XYZ về B, L, H WGS84
            double p = Math.Sqrt(xW * xW + yW * yW);
            double theta = Math.Atan2(zW * a, p * b);
            double latW = Math.Atan2(zW + ePrime2 * b * Math.Pow(Math.Sin(theta), 3), p - e2 * a * Math.Pow(Math.Cos(theta), 3));
            double lonW = Math.Atan2(yW, xW);

            return new LatLonPoint(latW * 180.0 / Math.PI, lonW * 180.0 / Math.PI);
        }

        private static LatLonPoint HelmertWGS84ToVN2000(double bDeg, double lDeg, double h)
        {
            double bRad = bDeg * Math.PI / 180.0;
            double lRad = lDeg * Math.PI / 180.0;

            double n = a / Math.Sqrt(1.0 - e2 * Math.Sin(bRad) * Math.Sin(bRad));
            double x = (n + h) * Math.Cos(bRad) * Math.Cos(lRad);
            double y = (n + h) * Math.Cos(bRad) * Math.Sin(lRad);
            double z = (n * (1.0 - e2) + h) * Math.Sin(bRad);

            // Biến đổi ngược 7 tham số
            double xV = (x - dX) / (1.0 + scale) - rz * y + ry * z;
            double yV = (y - dY) / (1.0 + scale) + rz * x - rx * z;
            double zV = (z - dZ) / (1.0 + scale) - ry * x + rx * y;

            double p = Math.Sqrt(xV * xV + yV * yV);
            double theta = Math.Atan2(zV * a, p * b);
            double latV = Math.Atan2(zV + ePrime2 * b * Math.Pow(Math.Sin(theta), 3), p - e2 * a * Math.Pow(Math.Cos(theta), 3));
            double lonV = Math.Atan2(yV, xV);

            return new LatLonPoint(latV * 180.0 / Math.PI, lonV * 180.0 / Math.PI);
        }

        // Tọa độ Lat, Lon sang số hiệu Tile Google (X, Y ở Zoom Z)
        public static (int tileX, int tileY) LatLonToTile(double lat, double lon, int zoom)
        {
            int n = 1 << zoom;
            int x = (int)Math.Floor((lon + 180.0) / 360.0 * n);
            double latRad = lat * Math.PI / 180.0;
            int y = (int)Math.Floor((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
            return (x, y);
        }

        // Số hiệu Tile sang góc Tây Bắc (Lat, Lon)
        public static LatLonPoint TileToLatLon(int x, int y, int zoom)
        {
            double n = Math.Pow(2.0, zoom);
            double lon = x / n * 360.0 - 180.0;
            double latRad = Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * y / n)));
            double lat = latRad * 180.0 / Math.PI;
            return new LatLonPoint(lat, lon);
        }
    }
}
