using GameApp.Application.Controllers;
using GameApp.Service.Services;
using GameApp.Service.Dtos;
using GameApp.Integration.Data;
using GameApp.Service.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GameApp.Service.Exceptions;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using GameApp.Application.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using GameApp.Service.Models;
using Moq;

namespace GameApp.Tests.Service;

public class LobbyServiceTests
{
    private readonly DbContextOptions<AppDbContext> _dbOptions;
    private readonly Mock<IGalleryService> _mockGallery;
    private readonly Mock<ILobbyCodeGenerator> _mockCodeGenerator;
    private readonly Mock<ILobbyRepository> _mockLobbyRepo;
    private readonly Mock<IPlayerRepository> _mockPlayerRepo;
    private readonly Mock<ILogger<LobbyService>> _mockLogger;

    public LobbyServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _mockGallery = new Mock<IGalleryService>();
        _mockCodeGenerator = new Mock<ILobbyCodeGenerator>();
        _mockLobbyRepo = new Mock<ILobbyRepository>();
        _mockPlayerRepo = new Mock<IPlayerRepository>();
        _mockLogger = new Mock<ILogger<LobbyService>>();
    }

    [Fact]
    public void Dispose()
    {
        using var context = new AppDbContext(_dbOptions);
        context.Database.EnsureDeleted();
    }

    private LobbyService CreateService()
    {
        return new LobbyService(
            _mockGallery.Object,
            _mockLobbyRepo.Object,
            _mockPlayerRepo.Object,
            _mockCodeGenerator.Object,
            NullLogger<LobbyService>.Instance);
    }

    private void SetupPersistedLobby(string code, Lobby? lobby = null)
    {
        var lb = lobby ?? new Lobby(code);
        _mockLobbyRepo.Setup(r => r.GetByCode(code)).Returns(lb);
        _mockLobbyRepo.Setup(r => r.GetById(lb.Id)).Returns(lb);
    }

    [Fact]
    public void CreateLobby_ShouldCreateNewLobby_AndPersist()
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
        _mockLobbyRepo.Verify(r => r.Add(It.Is<Lobby>(l => l.LobbyCode == "ABC123")), Times.Once);
        _mockLobbyRepo.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public void LobbyExists_ReturnsTrueForExistingLobby_InMemory()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("TEST01");
        service.CreateLobby();

        Assert.True(service.LobbyExists("TEST01"));
    }

    [Fact]
    public void LobbyExists_LoadsFromPersistence_WhenNotInMemory()
    {
        var service = CreateService();
        var persisted = new Lobby("PERSIST");
        SetupPersistedLobby("PERSIST", persisted);
        _mockPlayerRepo.Setup(r => r.GetByLobby(persisted.Id)).Returns(new List<Player>());

        Assert.True(service.LobbyExists("PERSIST"));
        _mockLobbyRepo.Verify(r => r.GetByCode("PERSIST"), Times.Once);
    }

    [Fact]
    public void LobbyExists_ReturnsFalseForNonExistingLobby()
    {
        var service = CreateService();
        Assert.False(service.LobbyExists("NONEXIST"));
        _mockLobbyRepo.Verify(r => r.GetByCode("NONEXIST"), Times.Once);
    }

    [Fact]
    public void GetLobby_ReturnsCorrectLobby_FromMemoryOrPersistence()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("GETTEST");
        var createdLobby = service.CreateLobby();

        var retrievedLobby = service.GetLobby("GETTEST");

        Assert.NotNull(retrievedLobby);
        Assert.Equal("GETTEST", retrievedLobby.LobbyCode);
        Assert.Same(createdLobby, retrievedLobby);
    }

    [Fact]
    public void AddPlayer_ShouldAddNewPlayer_AndPersist()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY1");
        var lobby = service.CreateLobby();

        var player = new Player("John", 1);

        service.AddPlayer(player, "LOBBY1");

        var memLobby = service.GetLobby("LOBBY1");
        Assert.Single(memLobby.Players);
        Assert.Equal("John", memLobby.Players.First().DisplayName);

        _mockPlayerRepo.Verify(r => r.GetByLobbyAndName(lobby.Id, "John"), Times.Once);
        _mockPlayerRepo.Verify(r => r.Add(It.Is<Player>(p => p.DisplayName == "John" && p.LobbyId == lobby.Id)), Times.Once);
        _mockPlayerRepo.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public void AddOrUpdatePlayerConnection_ShouldAddNewPlayer_AndPersist()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY3");
        var lobby = service.CreateLobby();

        _mockPlayerRepo.Setup(r => r.GetByLobbyAndName(lobby.Id, "Alice")).Returns((Player?)null);

        service.AddOrUpdatePlayerConnection("LOBBY3", "Alice", 3, "conn123");

        var memLobby = service.GetLobby("LOBBY3");
        Assert.Single(memLobby.Players);
        var alice = memLobby.Players.First();
        Assert.Equal("Alice", alice.DisplayName);
        Assert.Equal("conn123", alice.ConnectionId);
        Assert.Equal(3, alice.iconId);

        _mockPlayerRepo.Verify(r => r.Add(It.Is<Player>(p => p.DisplayName == "Alice" && p.ConnectionId == "conn123" && p.iconId == 3)), Times.Once);
        _mockPlayerRepo.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public void AddOrUpdatePlayerConnection_ShouldUpdateExistingPlayer_AndPersist()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY4");
        var lobby = service.CreateLobby();

        // Existing in memory first
        service.AddOrUpdatePlayerConnection("LOBBY4", "Bob", 1, "connOld");

        // Simulate existing in DB
        var dbPlayer = new Player("Bob", 1) { LobbyId = lobby.Id, ConnectionId = "connOld" };
        _mockPlayerRepo.Setup(r => r.GetByLobbyAndName(lobby.Id, "Bob")).Returns(dbPlayer);

        service.AddOrUpdatePlayerConnection("LOBBY4", "Bob", 2, "connNew");

        var memLobby = service.GetLobby("LOBBY4");
        var bobPlayer = memLobby.Players.FirstOrDefault(p => p.DisplayName.Equals("Bob", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(bobPlayer);
        Assert.Equal("connNew", bobPlayer!.ConnectionId);
        Assert.Equal(2, bobPlayer.iconId);

        _mockPlayerRepo.Verify(r => r.Update(It.Is<Player>(p => p.DisplayName == "Bob" && p.ConnectionId == "connNew" && p.iconId == 2)), Times.Once);
        _mockPlayerRepo.Verify(r => r.SaveChanges(), Times.AtLeastOnce);
    }

    [Fact]
    public void GetOrAssignLobbyImage_ReturnsNullForNonExistentLobby_LoadFails()
    {
        var service = CreateService();
        // Persistence returns null; EnsureLobbyExists will create a new lobby internally if accessed via GetLobby,
        // but here we directly call GetOrAssignLobbyImage which also ensures it exists by creating one in memory.
        // To simulate non-existent with no image availability, just set gallery to return null.
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("X");
        service.CreateLobby(); // ensure service works

        _mockGallery.Setup(x => x.GetRandomImage()).Returns((ImageDto?)null);

        var result = service.GetOrAssignLobbyImage("UNKNOWN");
        // It will create an in-memory lobby and then fail to assign image
        Assert.Null(result);
    }

    [Fact]
    public void GetOrAssignLobbyImage_ReusesExistingImage()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY5");
        var lobby = service.CreateLobby();
        lobby.SelectedImageId = "img123";
        lobby.SelectedImageUrl = "/images/img123";

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "fake image content");

        _mockGallery.Setup(x => x.GetImageFilePath("img123")).Returns(tempFile);

        try
        {
            var result = service.GetOrAssignLobbyImage("LOBBY5");

            Assert.NotNull(result);
            Assert.Equal("img123", result!.Id);
            _mockGallery.Verify(x => x.GetRandomImage(), Times.Never);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetOrAssignLobbyImage_AssignsNewImageWhenNoneExists_AndPersistsOnLobby()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY6");
        var lobby = service.CreateLobby();

        var imageDto = new ImageDto("newImg", "/images/newImg", 1024);
        _mockGallery.Setup(x => x.GetRandomImage()).Returns(imageDto);

        SetupPersistedLobby("LOBBY6", lobby);

        // Clear prior invocations from Arrange (CreateLobby triggers Add + SaveChanges)
        _mockLobbyRepo.Invocations.Clear();

        var result = service.GetOrAssignLobbyImage("LOBBY6");

        Assert.NotNull(result);
        Assert.Equal("newImg", result!.Id);

        var memLobby = service.GetLobby("LOBBY6");
        Assert.Equal("newImg", memLobby.SelectedImageId);
        Assert.Equal("/images/newImg", memLobby.SelectedImageUrl);

        _mockLobbyRepo.Verify(r => r.GetById(lobby.Id), Times.Once);
        _mockLobbyRepo.Verify(r => r.Update(It.Is<Lobby>(l => l.Id == lobby.Id && l.SelectedImageId == "newImg")), Times.Once);
        _mockLobbyRepo.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public void GetOrAssignLobbyImage_ReturnsNullWhenNoImagesAvailable()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY7");
        service.CreateLobby();

        _mockGallery.Setup(x => x.GetRandomImage()).Returns((ImageDto?)null);

        var result = service.GetOrAssignLobbyImage("LOBBY7");

        Assert.Null(result);
    }

    [Fact]
    public void GetLobbySelectedImagePath_ReturnsPathForExistingImage()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY8");
        var lobby = service.CreateLobby();
        lobby.SelectedImageId = "img456";

        _mockGallery.Setup(x => x.GetImageFilePath("img456")).Returns("/path/to/img456.jpg");

        var path = service.GetLobbySelectedImagePath("LOBBY8");

        Assert.Equal("/path/to/img456.jpg", path);
    }

    [Fact]
    public void GetLobbySelectedImagePath_ReturnsNullWhenNoImageAssigned()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("L");
        service.CreateLobby();

        var path = service.GetLobbySelectedImagePath("L");

        Assert.Null(path);
    }

    [Fact]
    public void AssignRoles_ReturnsNullWhenNotEnoughPlayers()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY9");
        service.CreateLobby();

        var player = new Player("Solo", 1);
        service.AddPlayer(player, "LOBBY9");

        var result = service.AssignRoles("LOBBY9");

        Assert.Null(result);
    }

    [Fact]
    public void AssignRoles_AssignsRolesCorrectly_AndPersistsRoleUpdates()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("LOBBY10");
        var lobby = service.CreateLobby();

        var player1 = new Player("Player1", 1) { ConnectionId = "conn1" };
        var player2 = new Player("Player2", 2) { ConnectionId = "conn2" };

        service.AddPlayer(player1, "LOBBY10");
        service.AddPlayer(player2, "LOBBY10");

        var imageDto = new ImageDto("roleImg", "/images/roleImg", 2048);
        _mockGallery.Setup(x => x.GetRandomImage()).Returns(imageDto);

        // Simulate fetching players from repo for role persistence
        _mockPlayerRepo.Setup(r => r.GetByLobbyIds(lobby.Id, It.IsAny<IEnumerable<string>>()))
            .Returns(new List<Player>
            {
                new Player("Player1", 1){ LobbyId = lobby.Id },
                new Player("Player2", 2){ LobbyId = lobby.Id }
            });

        var result = service.AssignRoles("LOBBY10");

        Assert.NotNull(result);
        Assert.NotNull(result!.Describer);
        Assert.NotNull(result.Drawer);
        Assert.NotEqual(result.Describer.DisplayName, result.Drawer.DisplayName);
        Assert.Equal(PlayerRole.Explainer, result.Describer.Role);
        Assert.Equal(PlayerRole.Artist, result.Drawer.Role);
        Assert.NotNull(result.Image);

        _mockPlayerRepo.Verify(r => r.Update(It.Is<Player>(p => p.DisplayName == "Player1" && p.Role != PlayerRole.None)), Times.AtLeastOnce);
        _mockPlayerRepo.Verify(r => r.Update(It.Is<Player>(p => p.DisplayName == "Player2" && p.Role != PlayerRole.None)), Times.AtLeastOnce);
        _mockPlayerRepo.Verify(r => r.SaveChanges(), Times.AtLeastOnce);
    }

    [Fact]
    public void GetAllLobbies_ReturnsAllCreatedLobbies()
    {
        var service = CreateService();
        _mockCodeGenerator.SetupSequence(x => x.Generate())
            .Returns("LOBBY_A")
            .Returns("LOBBY_B")
            .Returns("LOBBY_C");

        service.CreateLobby();
        service.CreateLobby();
        service.CreateLobby();

        var lobbies = service.GetAllLobbies().ToList();

        Assert.Equal(3, lobbies.Count);
        Assert.Contains(lobbies, l => l.LobbyCode == "LOBBY_A");
        Assert.Contains(lobbies, l => l.LobbyCode == "LOBBY_B");
        Assert.Contains(lobbies, l => l.LobbyCode == "LOBBY_C");
    }

    [Fact]
    public void JoinLobby_NoErrors_WhenLobbyExistsOrLoaded()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("JOINTEST");
        service.CreateLobby();

        service.JoinLobby("JOINTEST");

        Assert.True(service.LobbyExists("JOINTEST"));
    }

    [Fact]
    public void AddPlayer_ThrowsLobbyFullException_WhenLobbyIsFull()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("FULL001");
        service.CreateLobby();

        service.AddPlayer(new Player("First", 1), "FULL001");
        service.AddPlayer(new Player("Second", 2), "FULL001");

        Assert.Throws<LobbyFullException>(() => service.AddPlayer(new Player("Third", 3), "FULL001"));
    }

    [Fact]
    public void AddOrUpdatePlayerConnection_CreatesLobbyIfMissing()
    {
        var service = CreateService();

        Assert.False(service.LobbyExists("ImplicitLobby"));
        // Setup persistence to return null so EnsureLobbyExists creates new one
        _mockLobbyRepo.Setup(r => r.GetByCode("ImplicitLobby")).Returns((Lobby?)null);
        _mockLobbyRepo.Setup(r => r.Add(It.Is<Lobby>(l => l.LobbyCode == "ImplicitLobby")));
        _mockLobbyRepo.Setup(r => r.SaveChanges());

        service.AddOrUpdatePlayerConnection("ImplicitLobby", "DemoName", 5, "Connection432");

        Assert.True(service.LobbyExists("ImplicitLobby"));
        var lobby = service.GetLobby("ImplicitLobby");
        Assert.Single(lobby.Players);
        var player = lobby.Players.First();
        Assert.Equal("DemoName", player.DisplayName);
        Assert.Equal("Connection432", player.ConnectionId);
        Assert.Equal(5, player.iconId);

        _mockPlayerRepo.Verify(r => r.Add(It.Is<Player>(p => p.DisplayName == "DemoName" && p.iconId == 5 && p.ConnectionId == "Connection432")), Times.Once);
        _mockPlayerRepo.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public void AddPlayer_DoesNotDuplicateExistingPlayer_UpdatesExisting_AndPersists()
    {
        var service = CreateService();
        _mockCodeGenerator.Setup(x => x.Generate()).Returns("Lobby123");
        var lobby = service.CreateLobby();

        var initial = new Player("RepeatingName", 1)
        {
            Role = PlayerRole.None,
            ConnectionId = "ConnectionA"
        };
        _mockPlayerRepo.Setup(r => r.GetByLobbyAndName(lobby.Id, "RepeatingName")).Returns((Player?)null);
        service.AddPlayer(initial, "Lobby123");

        var updated = new Player("RepeatingName", 9)
        {
            Role = PlayerRole.Artist,
            ConnectionId = "ConnectionB"
        };
        var existingDb = new Player("RepeatingName", 1) { LobbyId = lobby.Id, Role = PlayerRole.None, ConnectionId = "ConnectionA" };
        _mockPlayerRepo.Setup(r => r.GetByLobbyAndName(lobby.Id, "RepeatingName")).Returns(existingDb);

        service.AddPlayer(updated, "Lobby123");

        var memLobby = service.GetLobby("Lobby123");
        Assert.Single(memLobby.Players);
        var player = memLobby.Players.First();
        Assert.Equal("RepeatingName", player.DisplayName);
        Assert.Equal(9, player.iconId);
        Assert.Equal(PlayerRole.Artist, player.Role);
        Assert.Equal("ConnectionB", player.ConnectionId);

        _mockPlayerRepo.Verify(r => r.Update(It.Is<Player>(p => p.DisplayName == "RepeatingName" && p.iconId == 9 && p.Role == PlayerRole.Artist && p.ConnectionId == "ConnectionB")), Times.Once);
        _mockPlayerRepo.Verify(r => r.SaveChanges(), Times.AtLeastOnce);
    }
}
