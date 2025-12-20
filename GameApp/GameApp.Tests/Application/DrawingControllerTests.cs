using GameApp.Application.Controllers;
using GameApp.Service.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GameApp.Tests.Application;
public class DrawingsControllerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly DrawingsController _controller;

    public DrawingsControllerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        var options = Options.Create(new GalleryOptions { DrawingsRoot = _testDirectory });
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

        _controller = new DrawingsController(options, envMock.Object);
    }

    [Fact]
    public async Task PostDrawing_WithNullFile_ReturnsBadRequest()
    {
        var result = await _controller.PostDrawing(null);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file provided.", badRequestResult.Value);
    }

    [Fact]
    public async Task PostDrawing_WithNonPngFile_ReturnsBadRequest()
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.jpg");

        var result = await _controller.PostDrawing(fileMock.Object);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Only PNG files are allowed.", badRequestResult.Value);
    }

    [Fact]
    public async Task PostDrawing_WithValidPngFile_ReturnsCreated()
    {
        var content = new byte[] { 137, 80, 78, 71 }; // PNG header bytes
        var stream = new MemoryStream(content);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.png");
        fileMock.Setup(f => f.Length).Returns(content.Length);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream s, CancellationToken ct) =>
            {
                stream.Position = 0;
                return stream.CopyToAsync(s, ct);
            });

        var result = await _controller.PostDrawing(fileMock.Object);

        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.StartsWith("/drawings/", createdResult.Location);
    }

    [Fact]
    public async Task PostDrawing_WithValidPngFile_SavesFileToDirectory()
    {
        var content = new byte[] { 137, 80, 78, 71 };
        var stream = new MemoryStream(content);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.png");
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream s, CancellationToken ct) =>
            {
                stream.Position = 0;
                return stream.CopyToAsync(s, ct);
            });

        await _controller.PostDrawing(fileMock.Object);

        var files = Directory.GetFiles(_testDirectory, "*.png");
        Assert.Single(files);
    }

    [Fact]
    public async Task PostDrawing_WithPngUppercase_IsAccepted()
    {
        var content = new byte[] { 137, 80, 78, 71 };
        var stream = new MemoryStream(content);
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.PNG");
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream s, CancellationToken ct) =>
            {
                stream.Position = 0;
                return stream.CopyToAsync(s, ct);
            });

        var result = await _controller.PostDrawing(fileMock.Object);

        Assert.IsType<CreatedResult>(result);
    }

    [Fact]
    public void GetLatest_WhenNoFiles_ReturnsNoContent()
    {
        var result = _controller.GetLatest();

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public void GetLatest_WithOneFile_ReturnsOkWithUrl()
    {
        var testFile = Path.Combine(_testDirectory, "test.png");
        File.WriteAllText(testFile, "test content");

        var result = _controller.GetLatest();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        var urlProperty = value.GetType().GetProperty("url");
        var url = urlProperty?.GetValue(value)?.ToString();
        Assert.StartsWith("/drawings/", url);
    }

    [Fact]
    public void GetLatest_WithMultipleFiles_ReturnsNewestFile()
    {
        var oldFile = Path.Combine(_testDirectory, "old.png");
        var newFile = Path.Combine(_testDirectory, "new.png");

        File.WriteAllText(oldFile, "old");
        Thread.Sleep(100);
        File.WriteAllText(newFile, "new");

        var result = _controller.GetLatest();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        var urlProperty = value.GetType().GetProperty("url");
        var url = urlProperty?.GetValue(value)?.ToString();
        Assert.Contains("new.png", url);
    }

    [Fact]
    public void GetLatest_IgnoresNonPngFiles()
    {
        File.WriteAllText(Path.Combine(_testDirectory, "test.jpg"), "jpg");
        File.WriteAllText(Path.Combine(_testDirectory, "test.txt"), "txt");

        var result = _controller.GetLatest();

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public void Constructor_CreatesDirectoryIfNotExists()
    {
        var newDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = Options.Create(new GalleryOptions { DrawingsRoot = newDir });
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

        _ = new DrawingsController(options, envMock.Object);

        Assert.True(Directory.Exists(newDir));
        Directory.Delete(newDir, true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
