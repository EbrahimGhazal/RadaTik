using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using RadaTik.Domain.Common;
using RadaTik.Helpers;

namespace RadaTik.Services.Clients;

public sealed class ClientNationalIdImageService(IWebHostEnvironment environment) : IClientNationalIdImageService
{
    public const string FolderName = "client-ids";
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public async Task<ServiceResult<string>> SaveAsync(int clientId, IFormFile file, CancellationToken ct = default)
    {
        if (clientId <= 0 || file == null || file.Length <= 0)
        {
            return ServiceResult.Fail<string>("اختر صورة الهوية أولاً.");
        }

        if (ImageUploadRules.IsTooLarge(file))
        {
            return ServiceResult.Fail<string>(ImageUploadRules.MaxNationalIdImageSizeMessage);
        }

        string extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        if (!AllowedExtensions.Contains(extension))
        {
            return ServiceResult.Fail<string>("صيغة الصورة غير مدعومة. استخدم JPG أو PNG أو WEBP.");
        }

        string folder = Path.Combine(
            environment.WebRootPath,
            "uploads",
            FolderName,
            clientId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);

        string fileName = $"{Guid.NewGuid():N}{extension}";
        string fullPath = Path.Combine(folder, fileName);
        await using FileStream stream = new(fullPath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        return ServiceResult.Ok($"/uploads/{FolderName}/{clientId}/{fileName}");
    }

    public void DeleteOwned(string? publicPath, int clientId)
    {
        if (!IsOwnedPath(publicPath, clientId))
        {
            return;
        }

        string fullPath = ToFullPath(publicPath!);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public bool IsOwnedPath(string? publicPath, int clientId)
    {
        if (string.IsNullOrWhiteSpace(publicPath) || clientId <= 0)
        {
            return false;
        }

        string normalized = publicPath.Replace('\\', '/');
        string prefix = $"/uploads/{FolderName}/{clientId}/";
        return normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && !normalized.Contains("..", StringComparison.Ordinal);
    }

    private string ToFullPath(string publicPath)
    {
        string relative = publicPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(environment.WebRootPath, relative);
    }
}
