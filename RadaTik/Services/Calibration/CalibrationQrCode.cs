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
        using QRCodeData data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using PngByteQRCode png = new(data);
        byte[] bytes = png.GetGraphic(pixelsPerModule);
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }
}
