namespace RadaTik.Services;

/// <summary>
/// انحراف المغناطيسية (WMM2025 حتى الدرجة 2). الموجب = شرق، أي شمال البوصلة يمين الشمال الجغرافي.
/// سمت البوصلة = السمت الجغرافي − الانحراف.
/// </summary>
public static class MagneticDeclination
{
    public const double EpochYear = 2025.0;

    public static double EastDegrees(double latitude, double longitude, DateTime? utc = null)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return 0;
        }

        DateTime at = utc ?? DateTime.UtcNow;
        double year = at.Year + (at.DayOfYear - 1 + at.TimeOfDay.TotalDays) / (DateTime.IsLeapYear(at.Year) ? 366.0 : 365.0);
        double dt = year - EpochYear;

        // NOAA WMM2025 main field + secular variation (n ≤ 2), nT and nT/year.
        double g10 = -29351.8 + (12.0 * dt);
        double g11 = -1410.8 + (9.7 * dt);
        double h11 = 4545.4 + (-21.5 * dt);
        double g20 = -2556.6 + (-11.6 * dt);
        double g21 = 2951.1 + (-5.2 * dt);
        double h21 = -3133.6 + (-27.7 * dt);
        double g22 = 1649.3 + (-8.0 * dt);
        double h22 = -815.1 + (-12.1 * dt);

        double lat = latitude * Math.PI / 180.0;
        double lon = longitude * Math.PI / 180.0;
        double theta = (Math.PI / 2.0) - lat;
        double sinTheta = Math.Sin(theta);
        double cosTheta = Math.Cos(theta);
        if (Math.Abs(sinTheta) < 1e-8)
        {
            return 0;
        }

        double sinLon = Math.Sin(lon);
        double cosLon = Math.Cos(lon);
        double sin2Lon = Math.Sin(2 * lon);
        double cos2Lon = Math.Cos(2 * lon);

        double dp10 = -sinTheta;
        double p11 = sinTheta;
        double dp11 = cosTheta;
        double p20 = (3 * cosTheta * cosTheta - 1) / 2.0;
        double dp20 = -3 * cosTheta * sinTheta;
        double p21 = Math.Sqrt(3) * sinTheta * cosTheta;
        double dp21 = Math.Sqrt(3) * (cosTheta * cosTheta - sinTheta * sinTheta);
        double p22 = (Math.Sqrt(3) / 2.0) * sinTheta * sinTheta;
        double dp22 = Math.Sqrt(3) * sinTheta * cosTheta;

        double x =
            (g10 * dp10) +
            ((g11 * cosLon) + (h11 * sinLon)) * dp11 +
            (g20 * dp20) +
            ((g21 * cosLon) + (h21 * sinLon)) * dp21 +
            ((g22 * cos2Lon) + (h22 * sin2Lon)) * dp22;

        double y =
            (((g11 * sinLon) - (h11 * cosLon)) * p11 +
             ((g21 * sinLon) - (h21 * cosLon)) * p21 +
             (2 * ((g22 * sin2Lon) - (h22 * cos2Lon)) * p22)) / sinTheta;

        if (Math.Abs(x) < 1e-9 && Math.Abs(y) < 1e-9)
        {
            return 0;
        }

        return Math.Atan2(y, x) * 180.0 / Math.PI;
    }
}
