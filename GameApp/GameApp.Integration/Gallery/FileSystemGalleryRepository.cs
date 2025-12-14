using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GameApp.Service.Services;
using GameApp.Service.Dtos;
using GameApp.Service.Options;

namespace GameApp.Integration.Gallery;

public class FileSystemGalleryRepository : IGalleryRepository
{
    private readonly string _imagesRoot;
    private readonly ILogger<FileSystemGalleryRepository> _logger;
    private static readonly string[] AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public FileSystemGalleryRepository(IOptions<GalleryOptions> options, ILogger<FileSystemGalleryRepository> logger)
    {
        _logger = logger;
        _imagesRoot = options.Value.ImagesRoot;
        Directory.CreateDirectory(_imagesRoot);
        _logger.LogInformation("Gallery repository initialized. Images root: {ImagesRoot}", _imagesRoot);
    }

    public IEnumerable<ImageDto> ListImages()
    {
        return Directory.EnumerateFiles(_imagesRoot)
            .Where(f => AllowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f => new ImageDto(Path.GetFileName(f), $"/images/{Path.GetFileName(f)}", new FileInfo(f).Length));
    }

    public ImageDto? GetRandomImage()
    {
        var files = Directory.EnumerateFiles(_imagesRoot)
            .Where(f => AllowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (files.Count == 0)
        {
            _logger.LogWarning("GetRandomImage: No images available.");
            return null;
        }

        var pick = files[Random.Shared.Next(files.Count)];
        return new ImageDto(Path.GetFileName(pick), $"/images/{Path.GetFileName(pick)}", new FileInfo(pick).Length);
    }

    public async Task<ImageDto> SaveImageAsync(Stream fileStream, string originalFileName, long length, CancellationToken ct = default)
    {
        if (fileStream is null || length <= 0)
        {
            _logger.LogWarning("Attempted to save empty file.");
            throw new ArgumentException("No file data provided.", nameof(fileStream));
        }

        var ext = Path.GetExtension(originalFileName);
        if (!AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected file with extension {Extension}", ext);
            throw new InvalidOperationException("Only .jpg, .jpeg, .png, .gif, .webp are allowed.");
        }

        var safeName = $"{Guid.NewGuid()}{ext.ToLowerInvariant()}";
        var savePath = Path.Combine(_imagesRoot, safeName);

        await using (var fs = File.Create(savePath))
        {
            await fileStream.CopyToAsync(fs, ct);
        }

        _logger.LogInformation("Saved image {ImageId} ({Bytes} bytes)", safeName, length);
        return new ImageDto(safeName, $"/images/{safeName}", length);
    }

    public string? GetImageFilePath(string imageId)
    {
        if (string.IsNullOrEmpty(imageId))
        {
            _logger.LogDebug("GetImageFilePath called with empty id.");
            return null;
        }

        var path = Path.Combine(_imagesRoot, imageId);
        return File.Exists(path) ? path : null;
    }
}