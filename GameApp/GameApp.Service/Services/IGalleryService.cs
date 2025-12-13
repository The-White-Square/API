using Microsoft.AspNetCore.Http;

namespace GameApp.Service.Services;

public interface IGalleryService
{
    IEnumerable<ImageDto> ListImages();
    ImageDto? GetRandomImage();
    Task<ImageDto> SaveImageAsync(IFormFile file);
    string? GetImageFilePath(string imageId);
}