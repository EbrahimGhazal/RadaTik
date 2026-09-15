using QRCoder;

namespace RadaTik.Services.Calibration;

public static class CalibrationQrCode
{
    public static string PngDataUri(string? text, int pixelsPerModule = 5)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(text.Trim(), QRCodeGenerator.ECCLevel.M);
        PngByteQRCode png = new(data);
        byte[] bytes = png.GetGraphic(Math.Clamp(pixelsPerModule, 2, 12), false);
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }
}
