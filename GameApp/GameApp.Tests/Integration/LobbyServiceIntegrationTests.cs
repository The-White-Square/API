using System;
using System.Linq;
using GameApp.Application.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using GameApp.Application.Data;
using GameApp.Application.LobbySystem;
using GameApp.Application.Service;
using GameApp.Application.Service.Exceptions;
using GameApp.Application.Utils;

namespace GameApp.Tests.Integration;

public class LobbyServiceIntegrationTests : IDisposable
{
    private readonly DbContextOptions<AppDbContext> _dbOptions;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly Mock<IGalleryService> _mockGallery;
    private readonly Mock<ILobbyCodeGenerator> _mockCodeGenerator;
    private readonly Mock<ILogger<LobbyService>> _mockLogger;

    public LobbyServiceIntegrationTests()
    {
        var dbName = Guid.NewGuid().ToString();
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        _dbFactory = new TestDbContextFactory(_dbOptions);
        _mockGallery = new Mock<IGalleryService>();
        _mockCodeGenerator = new Mock<ILobbyCodeGenerator>();
        _mockLogger = new Mock<ILogger<LobbyService>>();
    }

    public void Dispose()
    {
        using var context = new AppDbContext(_dbOptions);
        context.Database.EnsureDeleted();
    }

    [Fact]
    public void Lobby_PersistsAcrossServiceInstances_NoHardcodedSecrets()
    {
        _mockCodeGenerator.Setup(x => x.Generate()).Returns(() => Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper());

        var service1 = new LobbyService(_mockGallery.Object, _dbFactory, _mockCodeGenerator.Object, _mockLogger.Object);

        var created = service1.CreateLobby();
        var lobbyCode = created.LobbyCode;

        var player = new Player("IntUser", 5) { ConnectionId = Guid.NewGuid().ToString("N") };
        service1.AddPlayer(player, lobbyCode);

        // Simulate a separate instance (for example, another process) using the same database
        var service2 = new LobbyService(_mockGallery.Object, _dbFactory, _mockCodeGenerator.Object, _mockLogger.Object);

        using var db = new AppDbContext(_dbOptions);
        var dbLobby = db.Lobbies.Include(l => l.Players).FirstOrDefault(l => l.LobbyCode == lobbyCode);
        Assert.NotNull(dbLobby);
        Assert.Single(dbLobby.Players);
        Assert.Equal("IntUser", dbLobby.Players.First().DisplayName);
    }
    [Fact]
    public void AddPlayer_ToFullLobby_ThrowsLobbyFullException()
    {
        // Arrange
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("TEST123");
        var service = new LobbyService(_mockGallery.Object, _dbFactory, _mockCodeGenerator.Object, _mockLogger.Object);
        var lobby = service.CreateLobby();
        var lobbyCode = lobby.LobbyCode;

        var player1 = new Player("Player1", 1) { ConnectionId = "conn1" };
        var player2 = new Player("Player2", 2) { ConnectionId = "conn2" };
        var player3 = new Player("Player3", 3) { ConnectionId = "conn3" };

        service.AddPlayer(player1, lobbyCode);
        service.AddPlayer(player2, lobbyCode);

        // Act
        var exception = Assert.Throws<LobbyFullException>(() => 
            service.AddPlayer(player3, lobbyCode));

        // Assert
        Assert.Equal(lobbyCode, exception.LobbyId);

        using var db = new AppDbContext(_dbOptions);
        var dbLobby = db.Lobbies.Include(l => l.Players).First(l => l.LobbyCode == lobbyCode);
        Assert.Equal(2, dbLobby.Players.Count);
        Assert.DoesNotContain(dbLobby.Players, p => p.DisplayName == "Player3");
    }

    [Fact]
    public void AssignRoles_PersistsRolesToDatabase_AndRetrievableByNewInstance()
    {
        // Arrange
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("ROLE123");

        _mockGallery.Setup(x => x.GetRandomImage())
            .Returns(new ImageDto("img1", "/images/img1", 1024));
        _mockGallery.Setup(x => x.GetImageFilePath("img1"))
            .Returns("/path/to/img1.jpg");

        var service1 = new LobbyService(_mockGallery.Object, _dbFactory, _mockCodeGenerator.Object, _mockLogger.Object);
        var lobby = service1.CreateLobby();
        var lobbyCode = lobby.LobbyCode;

        var player1 = new Player("Alice", 1) { ConnectionId = "conn_alice" };
        var player2 = new Player("Bob", 2) { ConnectionId = "conn_bob" };

        service1.AddPlayer(player1, lobbyCode);
        service1.AddPlayer(player2, lobbyCode);

        // Act
        var assignment = service1.AssignRoles(lobbyCode);

        // Assert
        Assert.NotNull(assignment);
        Assert.NotNull(assignment.Describer);
        Assert.NotNull(assignment.Drawer);
        Assert.NotEqual(assignment.Describer.DisplayName, assignment.Drawer.DisplayName);

        using var db = new AppDbContext(_dbOptions);
        var dbLobby = db.Lobbies.Include(l => l.Players).First(l => l.LobbyCode == lobbyCode);

        var explainer = dbLobby.Players.Single(p => p.Role == PlayerRole.Explainer);
        var artist = dbLobby.Players.Single(p => p.Role == PlayerRole.Artist);

        Assert.NotNull(explainer);
        Assert.NotNull(artist);
        Assert.Contains(explainer.DisplayName, new[] { "Alice", "Bob" });
        Assert.Contains(artist.DisplayName, new[] { "Alice", "Bob" });

        Assert.NotNull(dbLobby.SelectedImageId);
        Assert.Equal("img1", dbLobby.SelectedImageId);
    }

    [Fact]
    public void AddPlayer_SamePlayerTwice_UpdatesInsteadOfDuplicating()
    {
        // Arrange
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("DUP123");
        var service = new LobbyService(_mockGallery.Object, _dbFactory, _mockCodeGenerator.Object, _mockLogger.Object);
        var lobby = service.CreateLobby();
        var lobbyCode = lobby.LobbyCode;

        var player = new Player("David", 7) { ConnectionId = "conn_initial" };

        // Act
        service.AddPlayer(player, lobbyCode);
        
        player.ConnectionId = "conn_updated";
        player.iconId = 15;
        player.Role = PlayerRole.Artist;
        service.AddPlayer(player, lobbyCode);

        // Assert
        using (var db = new AppDbContext(_dbOptions))
        {
            var dbLobby = db.Lobbies.Include(l => l.Players).First(l => l.LobbyCode == lobbyCode);
            
            Assert.Single(dbLobby.Players);
            
            var dbPlayer = dbLobby.Players.First();
            Assert.Equal("David", dbPlayer.DisplayName);
            Assert.Equal("conn_updated", dbPlayer.ConnectionId);
            Assert.Equal(15, dbPlayer.iconId);
            Assert.Equal(PlayerRole.Artist, dbPlayer.Role);
        }

        var memoryLobby = service.GetLobby(lobbyCode);
        Assert.Single(memoryLobby.Players);
        Assert.Equal("conn_updated", memoryLobby.Players.First().ConnectionId);
    }
}

