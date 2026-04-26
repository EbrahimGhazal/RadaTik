using Microsoft.AspNetCore.Http;

namespace RadTik.Helpers;

public static class ImageUploadRules
{
    public const long MaxImageBytes = 3 * 1024 * 1024; // 3 MB
    public const string MaxImageSizeMessage = "حجم الصورة يجب ألا يتجاوز 3 ميغابايت.";
    public const string MaxReceiptImageSizeMessage = "حجم صورة الإيصال يجب ألا يتجاوز 3 ميغابايت.";
    public const string MaxQrImageSizeMessage = "حجم صورة QR يجب ألا يتجاوز 3 ميغابايت.";
    public const string MaxNetworkLogoSizeMessage = "حجم شعار الشبكة يجب ألا يتجاوز 3 ميغابايت.";

    public static bool IsTooLarge(IFormFile? file) =>
        file != null && file.Length > MaxImageBytes;
}
