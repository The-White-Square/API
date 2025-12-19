using System.Text;
using GameApp.Service.Dtos;
using GameApp.Service.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameApp.Tests.Service;

public class GalleryServiceTests
{
    private readonly Mock<IGalleryRepository> _repoMock = new();
    private readonly GalleryService _service;

    public GalleryServiceTests()
    {
        _service = new GalleryService(_repoMock.Object, NullLogger<GalleryService>.Instance);
    }

    [Fact]
    public void ListImages_Returns_Ordered_By_Id()
    {
        // Arrange
        var images = new[]
        {
            new ImageDto("b.png", null, 0),
            new ImageDto("a.jpg", null, 0),
            new ImageDto("c.gif", null, 0),
        };
        _repoMock.Setup(r => r.ListImages()).Returns(images);

        // Act
        var result = _service.ListImages().ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Collection(
            result,
            x => Assert.Equal("a.jpg", x.Id),
            x => Assert.Equal("b.png", x.Id),
            x => Assert.Equal("c.gif", x.Id));
    }

    [Fact]
    public void GetRandomImage_Returns_Null_When_Repository_Returns_Null()
    {
        // Arrange
        _repoMock.Setup(r => r.GetRandomImage()).Returns((ImageDto?)null);

        // Act
        var result = _service.GetRandomImage();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetRandomImage_Returns_Image_From_Repository()
    {
        // Arrange
        var expected = new ImageDto("x1.jpg", null, 0);
        _repoMock.Setup(r => r.GetRandomImage()).Returns(expected);

        // Act
        var image = _service.GetRandomImage();

        // Assert
        Assert.NotNull(image);
        Assert.Equal("x1.jpg", image!.Id);
    }

    [Fact]
    public async Task SaveImageAsync_Passes_Stream_Name_Length_To_Repository_And_Returns_Dto()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("imagecontent");
        using var stream = new MemoryStream(data);
        var fileName = "test.jpg";
        var expected = new ImageDto("saved-id.jpg", null, 0);

        _repoMock
            .Setup(r => r.SaveImageAsync(It.IsAny<Stream>(), fileName, data.Length, It.IsAny<CancellationToken>()))
            .Callback<Stream, string, long, CancellationToken>((s, n, l, ct) =>
            {
                // Verify the exact stream content passed down
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                Assert.Equal(data, ms.ToArray());
                Assert.Equal(fileName, n);
                Assert.Equal(data.Length, l);
            })
            .ReturnsAsync(expected);

        // Act
        var result = await _service.SaveImageAsync(stream, fileName, data.Length);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected.Id, result.Id);
        _repoMock.Verify(r => r.SaveImageAsync(It.IsAny<Stream>(), fileName, data.Length, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetImageFilePath_Delegates_To_Repository()
    {
        // Arrange
        var imageId = "hello.png";
        var expectedPath = "/var/wwwroot/images/hello.png";
        _repoMock.Setup(r => r.GetImageFilePath(imageId)).Returns(expectedPath);

        // Act
        var result = _service.GetImageFilePath(imageId);

        // Assert
        Assert.Equal(expectedPath, result);
        _repoMock.Verify(r => r.GetImageFilePath(imageId), Times.Once);
    }
}
