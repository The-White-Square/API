using GameApp.Controllers;
using GameApp.Hubs;
using GameApp.LobbySystem;
using GameApp.Models.Requests;
using GameApp.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

public class LobbyControllerTests
{
    private readonly Mock<ILobbyService> _lobbyServiceMock = new();
    private readonly Mock<IHubContext<LobbyHub>> _hubContextMock = new();
    private readonly Mock<IClientProxy> _clientProxyMock = new();

    private LobbyController CreateController()
    {
        _hubContextMock.Setup(h => h.Clients.Group(It.IsAny<string>()))
            .Returns(_clientProxyMock.Object);

        return new LobbyController(_lobbyServiceMock.Object, _hubContextMock.Object);
    }

    [Fact]
    public async Task JoinLobby_JoinsLobby_WhenLobbyExists()
    {
        // Arrange
        var controller = CreateController();
        var request = new LobbyJoinRequest { LobbyId = "LOBBY1", Username = "Bob", IconId = 2 };
        _lobbyServiceMock.Setup(s => s.LobbyExists("lobby1")).Returns(true);

        // Act
        var result = await controller.JoinLobby(request);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Bob", ok.Value);
        _lobbyServiceMock.Verify(s => s.AddPlayer(It.Is<Player>(p => p.DisplayName == "Bob"), "lobby1"), Times.Once);
        _clientProxyMock.Verify(c => c.SendCoreAsync("PlayerJoined",
            It.Is<object[]>(o => (string)o[0] == "Bob"), default), Times.Once);
    }

    [Fact]
    public async Task JoinLobby_ReturnsBadRequest_WhenLobbyDoesNotExist()
    {
        // Arrange
        var controller = CreateController();
        var request = new LobbyJoinRequest { LobbyId = "NOT_EXIST", Username = "Charlie", IconId = 3 };
        _lobbyServiceMock.Setup(s => s.LobbyExists("not_exist")).Returns(false);

        // Act
        var result = await controller.JoinLobby(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Lobby not found", badRequest.Value);
    }

    [Fact]
    public void GetLobbyImage_ReturnsImage_WhenExists()
    {
        // Arrange
        var controller = CreateController();
        string lobbyId = "L1";
        _lobbyServiceMock.Setup(s => s.LobbyExists(lobbyId)).Returns(true);
        _lobbyServiceMock.Setup(s => s.GetOrAssignLobbyImage(lobbyId))
            .Returns(new ImageDto("1", "img.png", 12345));

        // Act
        var result = controller.GetLobbyImage(lobbyId);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ImageDto>(ok.Value);
        Assert.Equal("img.png", dto.Url);
        Assert.Equal("1", dto.Id);
        Assert.Equal(12345, dto.Bytes);
    }

    [Fact]
    public void GetLobbyImage_ReturnsNotFound_WhenLobbyDoesNotExist()
    {
        // Arrange
        var controller = CreateController();
        _lobbyServiceMock.Setup(s => s.LobbyExists("X")).Returns(false);

        // Act
        var result = controller.GetLobbyImage("X");

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("Lobby not found", notFound.Value);
    }

    [Fact]
    public void GetLobbyImage_ReturnsNotFound_WhenNoImage()
    {
        // Arrange
        var controller = CreateController();
        _lobbyServiceMock.Setup(s => s.LobbyExists("L1")).Returns(true);
        _lobbyServiceMock.Setup(s => s.GetOrAssignLobbyImage("L1")).Returns((ImageDto)null!);

        // Act
        var result = controller.GetLobbyImage("L1");

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("No images available.", notFound.Value);
    }

    [Fact]
    public void GetLobbyPlayers_ReturnsPlayers_WhenLobbyExists()
    {
        // Arrange
        var controller = CreateController();
        string lobbyId = "L1";
        var lobby = new Lobby();
        lobby.Players.Add(new Player("Alice", 1));
        lobby.Players.Add(new Player("Bob", 3));
        _lobbyServiceMock.Setup(s => s.LobbyExists(lobbyId)).Returns(true);
        _lobbyServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(lobby);

        // Act
        var result = controller.GetLobbyPlayers(lobbyId);

        // Assert
        Assert.NotNull(result);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var players = Assert.IsAssignableFrom<IEnumerable<dynamic>>(ok.Value);
        Assert.Equal(2, players.Count());
    }

    [Fact]
    public void GetLobbyPlayers_ReturnsNotFound_WhenLobbyDoesNotExist()
    {
        // Arrange
        var controller = CreateController();
        _lobbyServiceMock.Setup(s => s.LobbyExists("X")).Returns(false);

        // Act
        var result = controller.GetLobbyPlayers("X");

        // Assert
        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("Lobby not found", notFound.Value);
    }
}
