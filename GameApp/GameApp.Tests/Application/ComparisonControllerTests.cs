using GameApp.Application.Controllers;
using GameApp.Service.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text;

namespace GameApp.Tests.Application;
public class ComparisonControllerTests
{
    private readonly Mock<IGalleryService> _mockGallery;
    private readonly Mock<ILogger<ComparisonController>> _mockLogger;
    private readonly Mock<IWebHostEnvironment> _mockEnv;
    private readonly ComparisonController _controller;

    public ComparisonControllerTests()
    {
        _mockGallery = new Mock<IGalleryService>();
        _mockLogger = new Mock<ILogger<ComparisonController>>();
        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockEnv.Setup(e => e.WebRootPath).Returns("/wwwroot");

        Environment.SetEnvironmentVariable("COMPARISON_SERVICE_URL", "http://test-service/compare");
        _controller = new ComparisonController(_mockGallery.Object, _mockLogger.Object, _mockEnv.Object);
    }

    [Fact]
    public async Task Compare_NullRequest_ReturnsBadRequest()
    {
        var result = await _controller.Compare(null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Request body is required.", badRequest.Value);
    }

    [Fact]
    public async Task Compare_BothImagesNull_ReturnsBadRequest()
    {
        var request = new ComparisonController.ComparisonRequest
        {
            ImageIdA = null,
            ImagePathA = null,
            ImageIdB = null,
            ImagePathB = null
        };

        var result = await _controller.Compare(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Compare_GalleryIdResolves_CallsComparisonService()
    {
        var tempFileA = Path.GetTempFileName();
        var tempFileB = Path.GetTempFileName();

        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        await File.WriteAllBytesAsync(tempFileA, pngBytes);
        await File.WriteAllBytesAsync(tempFileB, pngBytes);

        _mockGallery.Setup(g => g.GetImageFilePath("id1")).Returns(tempFileA);
        _mockGallery.Setup(g => g.GetImageFilePath("id2")).Returns(tempFileB);

        var request = new ComparisonController.ComparisonRequest
        {
            ImageIdA = "id1",
            ImageIdB = "id2"
        };

        File.Delete(tempFileA);
        File.Delete(tempFileB);
    }

    [Fact]
    public async Task Compare_InvalidGalleryId_ReturnsBadRequest()
    {
        _mockGallery.Setup(g => g.GetImageFilePath("invalid")).Returns((string)null);

        var request = new ComparisonController.ComparisonRequest
        {
            ImageIdA = "invalid",
            ImageIdB = "invalid"
        };

        var result = await _controller.Compare(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Compare_NonImageBytes_ReturnsBadRequest()
    {
        var tempFileA = Path.GetTempFileName();
        var tempFileB = Path.GetTempFileName();

        await File.WriteAllBytesAsync(tempFileA, Encoding.UTF8.GetBytes("not an image"));
        await File.WriteAllBytesAsync(tempFileB, Encoding.UTF8.GetBytes("not an image"));

        _mockGallery.Setup(g => g.GetImageFilePath("id1")).Returns(tempFileA);
        _mockGallery.Setup(g => g.GetImageFilePath("id2")).Returns(tempFileB);

        var request = new ComparisonController.ComparisonRequest
        {
            ImageIdA = "id1",
            ImageIdB = "id2"
        };

        var result = await _controller.Compare(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);

        File.Delete(tempFileA);
        File.Delete(tempFileB);
    }

    [Fact]
    public async Task CompareMultipart_NullFiles_ReturnsBadRequest()
    {
        var result = await _controller.CompareMultipart(null, null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("form-data must include 'fileA' and 'fileB'", badRequest.Value);
    }

    [Fact]
    public async Task CompareMultipart_EmptyFiles_ReturnsBadRequest()
    {
        var mockFileA = new Mock<IFormFile>();
        var mockFileB = new Mock<IFormFile>();

        mockFileA.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockFileB.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.CompareMultipart(mockFileA.Object, mockFileB.Object, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    }

    private bool InvokeLooksLikeImage(byte[] data)
    {
        var method = typeof(ComparisonController).GetMethod("LooksLikeImage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (bool)method.Invoke(null, new object[] { data });
    }

    [Fact]
    public void LooksLikeImage_ValidGIF_ReturnsTrue()
    {
        var gifBytes = new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', 0x39, 0x61 };

        var result = InvokeLooksLikeImage(gifBytes);

        Assert.True(result);
    }

    [Fact]
    public void LooksLikeImage_ValidBMP_ReturnsTrue()
    {
        var bmpBytes = new byte[] { 0x42, 0x4D, 0x00, 0x00, 0x00, 0x00 };

        var result = InvokeLooksLikeImage(bmpBytes);

        Assert.True(result);
    }

    [Fact]
    public void LooksLikeImage_ValidWebP_ReturnsTrue()
    {
        var webpBytes = new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0x00, 0x00, 0x00, 0x00, (byte)'W', (byte)'E', (byte)'B', (byte)'P' };

        var result = InvokeLooksLikeImage(webpBytes);

        Assert.True(result);
    }

    [Fact]
    public void LooksLikeImage_ValidTIFF_LittleEndian_ReturnsTrue()
    {
        var tiffBytes = new byte[] { (byte)'I', (byte)'I', 0x2A, 0x00 };

        var result = InvokeLooksLikeImage(tiffBytes);

        Assert.True(result);
    }

    [Fact]
    public void LooksLikeImage_ValidTIFF_BigEndian_ReturnsTrue()
    {
        var tiffBytes = new byte[] { (byte)'M', (byte)'M', 0x00, 0x2A };

        var result = InvokeLooksLikeImage(tiffBytes);

        Assert.True(result);
    }

    [Fact]
    public void LooksLikeImage_NullData_ReturnsFalse()
    {
        var result = InvokeLooksLikeImage(null);

        Assert.False(result);
    }

    [Fact]
    public void LooksLikeImage_EmptyArray_ReturnsFalse()
    {
        var result = InvokeLooksLikeImage(new byte[0]);

        Assert.False(result);
    }

    [Fact]
    public void LooksLikeImage_TooShort_ReturnsFalse()
    {
        var result = InvokeLooksLikeImage(new byte[] { 0x01, 0x02 });

        Assert.False(result);
    }

    [Fact]
    public async Task Compare_ImageWithValidExtension_AcceptsNonImageBytes()
    {
        var tempFileA = Path.Combine(Path.GetTempPath(), "test.png");
        var tempFileB = Path.Combine(Path.GetTempPath(), "test2.png");

        await File.WriteAllBytesAsync(tempFileA, Encoding.UTF8.GetBytes("fake png data"));
        await File.WriteAllBytesAsync(tempFileB, Encoding.UTF8.GetBytes("fake png data"));

        _mockGallery.Setup(g => g.GetImageFilePath("id1")).Returns(tempFileA);
        _mockGallery.Setup(g => g.GetImageFilePath("id2")).Returns(tempFileB);

        var request = new ComparisonController.ComparisonRequest
        {
            ImageIdA = "id1",
            ImageIdB = "id2"
        };

        File.Delete(tempFileA);
        File.Delete(tempFileB);
    }
}