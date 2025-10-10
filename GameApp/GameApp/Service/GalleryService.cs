using GameApp.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace GameApp.Service;

public class GalleryService
{
    private readonly string _imagesRoot;
    private static readonly string[] AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public GalleryService(IWebHostEnvironment env)
    {
        _imagesRoot = Path.Combine(env.WebRootPath ?? "wwwroot", "images");
        Directory.CreateDirectory(_imagesRoot);
    }

    public IEnumerable<ImageDto> ListImages()
    {
        return Directory.EnumerateFiles(_imagesRoot)
            .Where(f => AllowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f => new ImageDto(
                Id: Path.GetFileName(f),
                Url: $"/images/{Path.GetFileName(f)}",
                Bytes: new FileInfo(f).Length
            ))
            .OrderBy(i => i.Id);
    }

    public ImageDto? GetRandomImage()
    {
        var files = Directory.EnumerateFiles(_imagesRoot)
            .Where(f => AllowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (files.Count == 0) return null;

        var pick = files[Random.Shared.Next(files.Count)];
        return new ImageDto(
            Id: Path.GetFileName(pick),
            Url: $"/images/{Path.GetFileName(pick)}",
            Bytes: new FileInfo(pick).Length
        );
    }

    // Save uploaded file, validate extension, return ImageDto
    public async Task<ImageDto> SaveImageAsync(IFormFile file)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("No file uploaded.", nameof(file));

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only .jpg, .jpeg, .png, .gif, .webp are allowed.");

        var safeName = $"{Guid.NewGuid()}{ext.ToLowerInvariant()}";
        var savePath = Path.Combine(_imagesRoot, safeName);

        await using (var fs = System.IO.File.Create(savePath))
        {
            await file.CopyToAsync(fs);
        }

        return new ImageDto(
            Id: safeName,
            Url: $"/images/{safeName}",
            Bytes: file.Length
        );
    }

    public string? GetImageFilePath(string imageId)
    {
        if (string.IsNullOrEmpty(imageId)) return null;
        var path = Path.Combine(_imagesRoot, imageId);
        return File.Exists(path) ? path : null;
    }
}