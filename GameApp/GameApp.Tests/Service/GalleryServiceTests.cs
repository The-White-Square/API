using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using GameApp.Service;

namespace GameApp.Tests.Service;

public class GalleryServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly GalleryService _service;

    public GalleryServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "gallery_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRoot);

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns(_tempRoot);

        _service = new GalleryService(envMock.Object, NullLogger<GalleryService>.Instance);
    }

    public void Dispose()
    {
        // Cleanup test image folder
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    private string CreateImage(string name, int size = 10)
    {
        var path = Path.Combine(_tempRoot, "images", name);
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(new string('x', size)));
        return path;
    }

    // Helper to mock IFormFile
    private IFormFile CreateMockFormFile(string fileName, byte[] data)
    {
        var stream = new MemoryStream(data);
        var file = new FormFile(stream, 0, data.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
        return file;
    }

    // TESTS

    [Fact]
    public void ListImages_Returns_Only_Allowed_Extensions()
    {
        // Arrange
        CreateImage("a.jpg");
        CreateImage("b.png");
        CreateImage("c.gif");
        File.WriteAllText(Path.Combine(_tempRoot, "images", "invalid.txt"), "bad file");

        // Act
        var result = _service.ListImages().ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, x => x.Id.EndsWith("a.jpg"));
        Assert.DoesNotContain(result, x => x.Id.EndsWith("invalid.txt"));
    }

    [Fact]
    public void GetRandomImage_Returns_Null_When_No_Images()
    {
        // Act
        var result = _service.GetRandomImage();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetRandomImage_Returns_One_Of_Existing_Files()
    {
        // Arrange
        CreateImage("x1.jpg");
        CreateImage("x2.jpg");

        // Act
        var image = _service.GetRandomImage();

        // Assert
        Assert.NotNull(image);
        Assert.True(image!.Id == "x1.jpg" || image.Id == "x2.jpg");
    }

    [Fact]
    public async Task SaveImageAsync_Saves_File_And_Returns_Dto()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("imagecontent");
        var file = CreateMockFormFile("test.jpg", data);

        // Act
        var result = await _service.SaveImageAsync(file);

        // Assert
        Assert.NotNull(result);
        Assert.EndsWith(".jpg", result.Id);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "images", result.Id)));
    }

    [Fact]
    public async Task SaveImageAsync_Rejects_Invalid_Extension()
    {
        // Arrange
        var file = CreateMockFormFile("bad.exe", Encoding.UTF8.GetBytes("data"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SaveImageAsync(file));
    }

    [Fact]
    public void GetImageFilePath_Returns_Path_When_File_Exists()
    {
        // Arrange
        CreateImage("hello.png");

        // Act
        var result = _service.GetImageFilePath("hello.png");

        // Assert
        Assert.NotNull(result);
        Assert.True(File.Exists(result));
    }

    [Fact]
    public void GetImageFilePath_Returns_Null_When_Not_Found()
    {
        // Arrange & Act
        var result = _service.GetImageFilePath("missing.jpg");

        // Assert
        Assert.Null(result);
    }
}
