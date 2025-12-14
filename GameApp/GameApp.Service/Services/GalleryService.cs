using Microsoft.Extensions.Logging;
using GameApp.Service.Dtos;

namespace GameApp.Service.Services;

public class GalleryService : IGalleryService
{
    private readonly IGalleryRepository _repo;
    private readonly ILogger<GalleryService> _logger;

    public GalleryService(IGalleryRepository repo, ILogger<GalleryService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public IEnumerable<ImageDto> ListImages()
    {
        var images = _repo.ListImages().OrderBy(i => i.Id).ToList();
        _logger.LogDebug("Listed {Count} images.", images.Count);
        return images;
    }

    public ImageDto? GetRandomImage()
    {
        var dto = _repo.GetRandomImage();
        if (dto is null)
        {
            _logger.LogWarning("GetRandomImage: No images available.");
            return null;
        }
        _logger.LogInformation("Random image selected: {ImageId}", dto.Id);
        return dto;
    }

    public Task<ImageDto> SaveImageAsync(Stream fileStream, string fileName, long length, CancellationToken ct = default)
        => _repo.SaveImageAsync(fileStream, fileName, length, ct);

    public string? GetImageFilePath(string imageId) => _repo.GetImageFilePath(imageId);
}