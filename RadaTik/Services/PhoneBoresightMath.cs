namespace RadaTik.Services;

/// <summary>
/// اتجاه إشعاع الهوائي من موبايل ملصوق عليه: ناظم الشاشة (+Z نحو المستخدم) وفق W3C DeviceOrientation.
/// الإطار الأرضي: X شرق، Y شمال، Z أعلى. السمت 0 = شمال مع عقارب الساعة.
/// </summary>
public static class PhoneBoresightMath
{
    public static PhoneBoresightPose FromDeviceOrientation(double alphaDegrees, double betaDegrees, double gammaDegrees)
    {
        double a = alphaDegrees * Math.PI / 180.0;
        double b = betaDegrees * Math.PI / 180.0;
        double g = gammaDegrees * Math.PI / 180.0;
        double ca = Math.Cos(a);
        double sa = Math.Sin(a);
        double cb = Math.Cos(b);
        double sb = Math.Sin(b);
        double cg = Math.Cos(g);
        double sg = Math.Sin(g);

        double east = (sb * ca) + (cb * sg * sa);
        double north = (sb * sa) - (cb * sg * ca);
        double up = cg * cb;
        double horizontal = Math.Sqrt((east * east) + (north * north));

        if (horizontal < 1e-4)
        {
            return new PhoneBoresightPose(0, up >= 0 ? 90 : -90, false);
        }

        double azimuth = Math.Atan2(east, north) * 180.0 / Math.PI;
        if (azimuth < 0)
        {
            azimuth += 360;
        }

        double elevation = Math.Atan2(up, horizontal) * 180.0 / Math.PI;
        return new PhoneBoresightPose(azimuth, elevation, true);
    }

    public static double ToMagneticAzimuth(double trueOrRawAzimuth, double declinationEastDegrees, bool fromTrueNorth)
    {
        double az = fromTrueNorth
            ? trueOrRawAzimuth - declinationEastDegrees
            : trueOrRawAzimuth;
        return LineOfSightMath.NormalizeDegrees360(az);
    }

    public static bool MutualAzimuthAligned(double transmitterAzimuth, double receiverAzimuth, double toleranceDegrees = 12)
    {
        double delta = Math.Abs(LineOfSightMath.SignedAngleDeltaDegrees(transmitterAzimuth, receiverAzimuth));
        return Math.Abs(delta - 180) <= toleranceDegrees;
    }

    /// <summary>webkitCompassAccuracy: سالب = غير صالح، و25° فأكثر = تشويش معدني/ضعيف.</summary>
    public static bool CompassUnstable(double? accuracyDegrees)
    {
        if (accuracyDegrees is null)
        {
            return false;
        }

        return accuracyDegrees.Value < 0 || accuracyDegrees.Value >= 25;
    }
}

public readonly record struct PhoneBoresightPose(double AzimuthDegrees, double ElevationDegrees, bool AzimuthValid);
