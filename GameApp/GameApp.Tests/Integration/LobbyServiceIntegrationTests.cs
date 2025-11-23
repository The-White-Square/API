using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using GameApp.Data;
using GameApp.LobbySystem;
using GameApp.Service;
using GameApp.Utils;

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
}
