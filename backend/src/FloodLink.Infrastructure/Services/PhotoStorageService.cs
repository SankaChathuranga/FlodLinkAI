using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace FloodLink.Infrastructure.Services;

public interface IPhotoStorageService
{
    Task<string?> SavePhotoAsync(IFormFile? photoFile);
}

public class PhotoStorageService : IPhotoStorageService
{
    private readonly IWebHostEnvironment _environment;

    public PhotoStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string?> SavePhotoAsync(IFormFile? photoFile)
    {
        if (photoFile == null || photoFile.Length == 0)
        {
            return null;
        }

        string webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        string uploadsFolder = Path.Combine(webRootPath, "uploads", "reports");

        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        string extension = Path.GetExtension(photoFile.FileName);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".jpg";
        }

        string uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await photoFile.CopyToAsync(stream);
        }

        return $"/uploads/reports/{uniqueFileName}";
    }
}
