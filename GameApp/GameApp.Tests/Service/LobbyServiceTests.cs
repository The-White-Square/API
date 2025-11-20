using GameApp.Controllers;
using GameApp.LobbySystem;
using GameApp.Service;
using GameApp.Data;
using GameApp.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameApp.Tests.Service;

public class LobbyServiceTests : IDisposable
{
    private readonly DbContextOptions<AppDbContext> _dbOptions;
    private readonly Mock<IGalleryService> _mockGallery;
    private readonly Mock<ILobbyCodeGenerator> _mockCodeGenerator;
    private readonly Mock<ILogger<LobbyService>> _mockLogger;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public LobbyServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var factory = new TestDbContextFactory(_dbOptions);
        _dbFactory = factory;

        _mockGallery = new Mock<IGalleryService>();
        _mockCodeGenerator = new Mock<ILobbyCodeGenerator>();
        _mockLogger = new Mock<ILogger<LobbyService>>();
    }

    public void Dispose()
    {
        using var context = new AppDbContext(_dbOptions);
        context.Database.EnsureDeleted();
    }

    private LobbyService CreateService()
    {
        return new LobbyService(_mockGallery.Object, _dbFactory, _mockCodeGenerator.Object, _mockLogger.Object);
    }

    [Fact]
    public void CreateLobby_ShouldCreateNewLobby()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("ABC123");

        // Act
        var lobby = service.CreateLobby();

        // Assert
        Assert.NotNull(lobby);
        Assert.Equal("ABC123", lobby.LobbyCode);
        Assert.True(service.LobbyExists("ABC123"));

        using var db = new AppDbContext(_dbOptions);
        var dbLobby = db.Lobbies.FirstOrDefault(l => l.LobbyCode == "ABC123");
        Assert.NotNull(dbLobby);
    }

    [Fact]
    public void LobbyExists_ReturnsTrueForExistingLobby()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("TEST01");
        service.CreateLobby();

        // Act & Assert
        Assert.True(service.LobbyExists("TEST01"));
    }

    [Fact]
    public void LobbyExists_ReturnsFalseForNonExistingLobby()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Assert.False(service.LobbyExists("NONEXIST"));
    }

    [Fact]
    public void GetLobby_ReturnsCorrectLobby()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("GETTEST");
        var createdLobby = service.CreateLobby();

        // Act
        var retrievedLobby = service.GetLobby("GETTEST");

        // Assert
        Assert.NotNull(retrievedLobby);
        Assert.Equal("GETTEST", retrievedLobby.LobbyCode);
        Assert.Same(createdLobby, retrievedLobby);
    }

    [Fact]
    public void AddPlayer_ShouldAddNewPlayer()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY1");
        service.CreateLobby();

        var player = new Player("John", 1);

        // Act
        service.AddPlayer(player, "LOBBY1");

        // Assert
        var lobby = service.GetLobby("LOBBY1");
        Assert.Single(lobby.Players);
        Assert.Equal("John", lobby.Players.First().DisplayName);

        using var db = new AppDbContext(_dbOptions);
        var dbPlayer = db.Players.FirstOrDefault(p => p.DisplayName == "John" && p.LobbyId == lobby.Id);
        Assert.NotNull(dbPlayer);
    }

    [Fact]
    public void AddPlayer_ShouldUpdateExistingPlayer()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY2");
        service.CreateLobby();

        var player1 = new Player("Jane", 1) { ConnectionId = "conn1" };
        var player2 = new Player("Jane", 2) { ConnectionId = "conn2", Role = PlayerRole.Artist };

        // Act
        service.AddPlayer(player1, "LOBBY2");
        service.AddPlayer(player2, "LOBBY2");

        // Assert
        var lobby = service.GetLobby("LOBBY2");
        Assert.Single(lobby.Players); // Should be 1 player after update, not 2

        var player = lobby.Players.First();
        Assert.Equal("Jane", player.DisplayName);
        Assert.Equal(2, player.iconId);
        Assert.Equal("conn2", player.ConnectionId);
        Assert.Equal(PlayerRole.Artist, player.Role);

        using var db = new AppDbContext(_dbOptions);
        var dbPlayer = db.Players.FirstOrDefault(p => p.DisplayName == "Jane");
        Assert.NotNull(dbPlayer);
        Assert.Equal(2, dbPlayer.iconId);
        Assert.Equal("conn2", dbPlayer.ConnectionId);
        Assert.Equal(PlayerRole.Artist, dbPlayer.Role);
    }

    [Fact]
    public void AddOrUpdatePlayerConnection_ShouldAddNewPlayer()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY3");
        service.CreateLobby();

        // Act
        service.AddOrUpdatePlayerConnection("LOBBY3", "Alice", 3, "conn123");

        // Assert
        var lobby = service.GetLobby("LOBBY3");
        Assert.Single(lobby.Players);
        Assert.Equal("Alice", lobby.Players.First().DisplayName);
        Assert.Equal("conn123", lobby.Players.First().ConnectionId);

        using var db = new AppDbContext(_dbOptions);
        var dbPlayer = db.Players.FirstOrDefault(p => p.DisplayName == "Alice");
        Assert.NotNull(dbPlayer);
        Assert.Equal("conn123", dbPlayer.ConnectionId);
    }

    [Fact]
    public void AddOrUpdatePlayerConnection_ShouldUpdateExistingPlayer()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY4");
        service.CreateLobby();

        service.AddOrUpdatePlayerConnection("LOBBY4", "Bob", 1, "connOld");

        // Act
        service.AddOrUpdatePlayerConnection("LOBBY4", "Bob", 2, "connNew");

        // Assert
        var lobby = service.GetLobby("LOBBY4");
        var bobPlayer = lobby.Players.FirstOrDefault(p => p.DisplayName.Equals("Bob", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(bobPlayer);
        Assert.Equal("connNew", bobPlayer.ConnectionId);
        Assert.Equal(2, bobPlayer.iconId);

        using var db = new AppDbContext(_dbOptions);
        var dbPlayer = db.Players.FirstOrDefault(p => p.DisplayName == "Bob");
        Assert.NotNull(dbPlayer);
        Assert.Equal("connNew", dbPlayer.ConnectionId);
        Assert.Equal(2, dbPlayer.iconId);
    }

    [Fact]
    public void GetOrAssignLobbyImage_ReturnsNullForNonExistentLobby()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetOrAssignLobbyImage("NONEXIST");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetOrAssignLobbyImage_ReusesExistingImage()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY5");
        var lobby = service.CreateLobby();
        lobby.SelectedImageId = "img123";
        lobby.SelectedImageUrl = "/images/img123";

        // Create a temporary file to simulate the image
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "fake image content");
        
        _mockGallery.Setup(x => x.GetImageFilePath("img123")).Returns(tempFile);

        try
        {
            // Act
            var result = service.GetOrAssignLobbyImage("LOBBY5");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("img123", result.Id);
            _mockGallery.Verify(x => x.GetRandomImage(), Times.Never);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetOrAssignLobbyImage_AssignsNewImageWhenNoneExists()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY6");
        service.CreateLobby();

        var imageDto = new ImageDto("newImg", "/images/newImg", 1024);
        _mockGallery.Setup(x => x.GetRandomImage()).Returns(imageDto);

        // Act
        var result = service.GetOrAssignLobbyImage("LOBBY6");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("newImg", result.Id);

        var lobby = service.GetLobby("LOBBY6");
        Assert.Equal("newImg", lobby.SelectedImageId);
        Assert.Equal("/images/newImg", lobby.SelectedImageUrl);
    }

    [Fact]
    public void GetOrAssignLobbyImage_ReturnsNullWhenNoImagesAvailable()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY7");
        service.CreateLobby();

        _mockGallery.Setup(x => x.GetRandomImage()).Returns((ImageDto?)null);

        // Act
        var result = service.GetOrAssignLobbyImage("LOBBY7");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetLobbySelectedImagePath_ReturnsPathForExistingImage()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY8");
        var lobby = service.CreateLobby();
        lobby.SelectedImageId = "img456";

        _mockGallery.Setup(x => x.GetImageFilePath("img456")).Returns("/path/to/img456.jpg");

        // Act
        var path = service.GetLobbySelectedImagePath("LOBBY8");

        // Assert
        Assert.Equal("/path/to/img456.jpg", path);
    }

    [Fact]
    public void GetLobbySelectedImagePath_ReturnsNullForNonExistentLobby()
    {
        // Arrange
        var service = CreateService();

        // Act
        var path = service.GetLobbySelectedImagePath("NONEXIST");

        // Assert
        Assert.Null(path);
    }

    [Fact]
    public void AssignRoles_ReturnsNullForNonExistentLobby()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.AssignRoles("NONEXIST");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void AssignRoles_ReturnsNullWhenNotEnoughPlayers()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY9");
        service.CreateLobby();

        var player = new Player("Solo", 1);
        service.AddPlayer(player, "LOBBY9");

        // Act
        var result = service.AssignRoles("LOBBY9");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void AssignRoles_AssignsRolesCorrectly()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY10");
        service.CreateLobby();

        var player1 = new Player("Player1", 1) { ConnectionId = "conn1" };
        var player2 = new Player("Player2", 2) { ConnectionId = "conn2" };

        service.AddPlayer(player1, "LOBBY10");
        service.AddPlayer(player2, "LOBBY10");

        var imageDto = new ImageDto("roleImg", "/images/roleImg", 2048);
        _mockGallery.Setup(x => x.GetRandomImage()).Returns(imageDto);

        // Act
        var result = service.AssignRoles("LOBBY10");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Describer);
        Assert.NotNull(result.Drawer);
        Assert.NotEqual(result.Describer.DisplayName, result.Drawer.DisplayName);
        Assert.Equal(PlayerRole.Explainer, result.Describer.Role);
        Assert.Equal(PlayerRole.Artist, result.Drawer.Role);
        Assert.NotNull(result.Image);

        using var db = new AppDbContext(_dbOptions);
        var dbPlayers = db.Players.Where(p => p.LobbyId == service.GetLobby("LOBBY10").Id).ToList();
        Assert.Contains(dbPlayers, p => p.Role == PlayerRole.Explainer);
        Assert.Contains(dbPlayers, p => p.Role == PlayerRole.Artist);
    }

    [Fact]
    public void GetAllLobbies_ReturnsAllCreatedLobbies()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.SetupSequence(x => x.Generate())
            .Returns("LOBBY_A")
            .Returns("LOBBY_B")
            .Returns("LOBBY_C");

        service.CreateLobby();
        service.CreateLobby();
        service.CreateLobby();

        // Act
        var lobbies = service.GetAllLobbies().ToList();

        // Assert
        Assert.Equal(3, lobbies.Count);
        Assert.Contains(lobbies, l => l.LobbyCode == "LOBBY_A");
        Assert.Contains(lobbies, l => l.LobbyCode == "LOBBY_B");
        Assert.Contains(lobbies, l => l.LobbyCode == "LOBBY_C");
    }

    [Fact]
    public void JoinLobby_LogsCorrectly()
    {
        // Arrange
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("JOINTEST");
        service.CreateLobby();

        // Act
        service.JoinLobby("JOINTEST");

        // Assert
        // Verify that JoinLobby executes without error
        Assert.True(service.LobbyExists("JOINTEST"));
    }
}

// Helper class to create DbContext for testing
public class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext()
    {
        return new AppDbContext(_options);
    }
}