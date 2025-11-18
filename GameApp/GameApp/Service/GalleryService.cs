using GameApp.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using GameApp.Service.Extensions;

namespace GameApp.Service;

public class GalleryService : IGalleryService
{
    private readonly string _imagesRoot;
    private readonly ILogger<GalleryService> _logger;
    private static readonly string[] AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public GalleryService(IWebHostEnvironment env, ILogger<GalleryService> logger)
    {
        _logger = logger;
        _imagesRoot = Path.Combine(env.WebRootPath ?? "wwwroot", "images");
        Directory.CreateDirectory(_imagesRoot);
        _logger.LogInformation("GalleryService initialized. Images root: {ImagesRoot}", _imagesRoot);
    }

    public IEnumerable<ImageDto> ListImages()
    {
        var files = Directory.EnumerateFiles(_imagesRoot)
            .Where(f => AllowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f => new ImageDto(
                Id: Path.GetFileName(f),
                Url: $"/images/{Path.GetFileName(f)}",
                Bytes: new FileInfo(f).Length
            ))
            .OrderBy(i => i.Id)
            .ToList();

        _logger.LogDebug("Listed {Count} images.", files.Count);
        return files;
    }

    public ImageDto? GetRandomImage()
    {
        var files = Directory.EnumerateFiles(_imagesRoot)
            .Where(f => AllowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        var pick = files.GetRandom();
        if (pick is null)
        {
            _logger.LogWarning("GetRandomImage: No images available.");
            return null;
        }

        var dto = new ImageDto(
            Id: Path.GetFileName(pick),
            Url: $"/images/{Path.GetFileName(pick)}",
            Bytes: new FileInfo(pick).Length
        );

        _logger.LogInformation("Random image selected: {ImageId}", dto.Id);
        return dto;
    }

    // Save uploaded file, validate extension, return ImageDto
    public async Task<ImageDto> SaveImageAsync(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            _logger.LogWarning("Attempted to save empty file.");
            throw new ArgumentException("No file uploaded.", nameof(file));
        }

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected file with extension {Extension}", ext);
            throw new InvalidOperationException("Only .jpg, .jpeg, .png, .gif, .webp are allowed.");
        }

        var safeName = $"{Guid.NewGuid()}{ext.ToLowerInvariant()}";
        var savePath = Path.Combine(_imagesRoot, safeName);

        await using (var fs = System.IO.File.Create(savePath))
        {
            await file.CopyToAsync(fs);
        }

        _logger.LogInformation("Saved image {ImageId} ({Bytes} bytes)", safeName, file.Length);

        return new ImageDto(
            Id: safeName,
            Url: $"/images/{safeName}",
            Bytes: file.Length
        );
    }

    public string? GetImageFilePath(string imageId)
    {
        if (string.IsNullOrEmpty(imageId))
        {
            _logger.LogDebug("GetImageFilePath called with empty id.");
            return null;
        }

        var path = Path.Combine(_imagesRoot, imageId);
        var exists = File.Exists(path);
        _logger.LogDebug("Image path lookup for {ImageId}. Exists: {Exists}", imageId, exists);
        return exists ? path : null;
    }
}