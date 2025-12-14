using GameApp.Service.Dtos;
namespace GameApp.Service.Services;

public interface IGalleryRepository
{
    IEnumerable<ImageDto> ListImages();
    ImageDto? GetRandomImage();
    Task<ImageDto> SaveImageAsync(Stream fileStream, string originalFileName, long length, CancellationToken ct = default);
    string? GetImageFilePath(string imageId);
}