using Microsoft.AspNetCore.Hosting;
using FloodLink.Domain.Exceptions;
using Microsoft.AspNetCore.Http;

namespace FloodLink.Infrastructure.Services;

public interface IPhotoStorageService
{
    /// <summary>Saves an image under <c>wwwroot/uploads/&lt;folder&gt;</c> and returns its URL path, or null for no file.</summary>
    Task<string?> SavePhotoAsync(IFormFile? photoFile, string folder = PhotoStorageService.ReportsFolder);
}

/// <summary>
/// Saves report photos under <c>wwwroot/uploads/reports</c> (served at <c>/uploads/reports/…</c>) with a
/// random name. Only JPEG, PNG and WebP images up to <see cref="MaxBytes"/> are accepted, so nothing a
/// browser would run (HTML, SVG, scripts) can be uploaded and then served.
/// </summary>
public class PhotoStorageService : IPhotoStorageService
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const string ReportsFolder = "reports";
    public const string DeliveriesFolder = "deliveries";

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private readonly IWebHostEnvironment _environment;

    public PhotoStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string?> SavePhotoAsync(IFormFile? photoFile, string folder = ReportsFolder)
    {
        if (photoFile == null || photoFile.Length == 0)
        {
            return null;
        }

        if (photoFile.Length > MaxBytes)
        {
            throw new BadRequestException($"Photo must be at most {MaxBytes / (1024 * 1024)} MB.");
        }

        // The extension decides how the file is served, so it must be an image type; files without
        // one are taken as JPEG (camera uploads). The declared content type must agree.
        string extension = Path.GetExtension(photoFile.FileName);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".jpg";
        }
        if (!AllowedTypes.TryGetValue(extension, out var expectedType) ||
            (!string.IsNullOrEmpty(photoFile.ContentType) && photoFile.ContentType != "application/octet-stream" &&
             !string.Equals(photoFile.ContentType, expectedType, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BadRequestException("Photo must be a JPEG, PNG or WebP image.");
        }

        string webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        if (folder is not (ReportsFolder or DeliveriesFolder))
        {
            throw new ArgumentOutOfRangeException(nameof(folder), folder, "Unknown photo folder.");
        }
        string uploadsFolder = Path.Combine(webRootPath, "uploads", folder);

        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        string uniqueFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await photoFile.CopyToAsync(stream);
        }

        return $"/uploads/{folder}/{uniqueFileName}";
    }
}
