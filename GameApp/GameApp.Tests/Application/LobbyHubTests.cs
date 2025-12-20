using GameApp.Application.Hubs;
using GameApp.Service.Dtos;
using GameApp.Service.Models;
using GameApp.Service.Services;
using GameApp.Service.Utils;
using Microsoft.AspNetCore.SignalR;

namespace GameApp.Tests.Hubs
{
    public class LobbyHubTests
    {
        private readonly Mock<ILobbyService> _mockLobbyService;
        private readonly Mock<IDrawingRelay> _mockDrawingRelay;
        private readonly Mock<IDrawingStore> _mockDrawingStore;
        private readonly Mock<IHubCallerClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly Mock<IGroupManager> _mockGroups;
        private readonly LobbyHub _hub;

        public LobbyHubTests()
        {
            _mockLobbyService = new Mock<ILobbyService>();
            _mockDrawingRelay = new Mock<IDrawingRelay>();
            _mockDrawingStore = new Mock<IDrawingStore>();
            _mockClients = new Mock<IHubCallerClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockGroups = new Mock<IGroupManager>();

            _hub = new LobbyHub(_mockLobbyService.Object, _mockDrawingRelay.Object, _mockDrawingStore.Object)
            {
                Clients = _mockClients.Object,
                Groups = _mockGroups.Object,
                Context = Mock.Of<HubCallerContext>(ctx => ctx.ConnectionId == "test-connection-id")
            };
        }

        [Fact]
        public async Task SendLobbyMessage_ShouldBroadcastMessageToGroup()
        {
            var lobbyId = "lobby123";
            var message = "Hello everyone!";
            var playerName = "TestPlayer";
            var iconId = 2;

            _mockClients.Setup(c => c.Group(lobbyId)).Returns(_mockClientProxy.Object);

            await _hub.SendLobbyMessage(lobbyId, message, playerName, iconId);

            _mockClientProxy.Verify(c => c.SendCoreAsync("LobbyMessage",
                It.Is<object[]>(args => args[0].ToString() == message && args[1].ToString() == playerName),
                default), Times.Once);
        }

        [Fact]
        public async Task GetPlayers_ShouldReturnPlayerNames()
        {
            var lobbyId = "lobby123";
            var players = new List<Player>
            {
                new Player { DisplayName = "Alice" },
                new Player { DisplayName = "Bob" },
                new Player { DisplayName = "Charlie" }
            };
            var lobby = new Lobby {};
            lobby.Players.AddRange(players);

            _mockLobbyService.Setup(s => s.LobbyExists(lobbyId)).Returns(true);
            _mockLobbyService.Setup(s => s.GetLobby(lobbyId)).Returns(lobby);

            var result = await _hub.GetPlayers(lobbyId);

            Assert.Equal(3, result.Length);
            Assert.Contains("Alice", result);
            Assert.Contains("Bob", result);
            Assert.Contains("Charlie", result);
        }

        [Fact]
        public async Task GetPlayers_ShouldThrowHubException_WhenLobbyDoesNotExist()
        {
            var lobbyId = "nonexistent";
            _mockLobbyService.Setup(s => s.LobbyExists(lobbyId)).Returns(false);

            await Assert.ThrowsAsync<HubException>(() => _hub.GetPlayers(lobbyId));
        }

        [Fact]
        public async Task BeginStroke_ShouldStoreStrokeAndRelayToDescriber()
        {
            var lobbyId = "lobby123";
            var strokeId = "stroke1";
            var color = "#FF0000";
            var width = 2.5;
            var tool = "pen";
            var describerConn = "describer-conn";

            var lobby = new Lobby {};
            lobby.Players.Add(new Player { ConnectionId = describerConn, Role = PlayerRole.Explainer });

            _mockLobbyService.Setup(s => s.LobbyExists(lobbyId)).Returns(true);
            _mockLobbyService.Setup(s => s.GetLobby(lobbyId)).Returns(lobby);

            await _hub.BeginStroke(lobbyId, strokeId, color, width, tool);

            _mockDrawingStore.Verify(s => s.AppendStrokeStarted(lobbyId, strokeId, color, width, tool), Times.Once);
            _mockDrawingRelay.Verify(r => r.RelayStrokeStarted(
                It.Is<StrokeStartedDto>(dto => dto.StrokeId == strokeId && dto.Color == color),
                describerConn), Times.Once);
        }

        [Fact]
        public async Task AddStrokePoints_ShouldStorePointsAndRelay()
        {
            var lobbyId = "lobby123";
            var strokeId = "stroke1";
            var points = new List<PointDto>
            {
                new(10, 20),
                new(15, 25)
            };

            _mockLobbyService.Setup(s => s.LobbyExists(lobbyId)).Returns(false);
            _mockClients.Setup(c => c.GroupExcept(lobbyId, It.IsAny<IReadOnlyList<string>>()))
                .Returns(_mockClientProxy.Object);

            await _hub.AddStrokePoints(lobbyId, strokeId, points);

            _mockDrawingStore.Verify(s => s.AppendStrokePoints(lobbyId, strokeId, points), Times.Once);
        }

        [Fact]
        public async Task ClearCanvas_ShouldClearStoreAndNotifyClients()
        {
            var lobbyId = "lobby123";
            _mockLobbyService.Setup(s => s.LobbyExists(lobbyId)).Returns(false);
            _mockClients.Setup(c => c.GroupExcept(lobbyId, It.IsAny<IReadOnlyList<string>>()))
                .Returns(_mockClientProxy.Object);

            await _hub.ClearCanvas(lobbyId);

            _mockDrawingStore.Verify(s => s.AppendCanvasCleared(lobbyId), Times.Once);
            _mockClientProxy.Verify(c => c.SendCoreAsync("CanvasCleared",
                It.IsAny<object[]>(), default), Times.Once);
        }

        [Fact]
        public async Task UndoLast_ShouldResetCanvasWhenSuccessful()
        {
            var lobbyId = "lobby123";
            var events = new List<DrawingEventBase>
            {
                new StrokeStartedEvent("s1", "#000", 1, "pen")
            };

            _mockDrawingStore.Setup(s => s.UndoLast(lobbyId)).Returns(true);
            _mockDrawingStore.Setup(s => s.GetActiveEvents(lobbyId)).Returns(events);
            _mockClients.Setup(c => c.Group(lobbyId)).Returns(_mockClientProxy.Object);

            var result = await _hub.UndoLast(lobbyId);

            Assert.True(result);
            _mockDrawingStore.Verify(s => s.UndoLast(lobbyId), Times.Once);
            _mockClientProxy.Verify(c => c.SendCoreAsync("CanvasReset",
                It.IsAny<object[]>(), default), Times.Once);
        }

        [Fact]
        public async Task GoToFinal_ShouldBroadcastToAllClientsInLobby()
        {
            var lobbyId = "lobby123";
            _mockLobbyService.Setup(s => s.LobbyExists(lobbyId)).Returns(true);
            _mockClients.Setup(c => c.Group(lobbyId)).Returns(_mockClientProxy.Object);

            await _hub.GoToFinal(lobbyId);

            _mockClientProxy.Verify(c => c.SendCoreAsync("GoToFinal",
                It.IsAny<object[]>(), default), Times.Once);
        }

        [Fact]
        public async Task AssignRoles_ShouldReturnFalse_WhenAssignmentFails()
        {
            var lobbyId = "lobby123";
            _mockLobbyService.Setup(s => s.AssignRoles(lobbyId)).Returns((RolesAssignment)null);

            var result = await _hub.AssignRoles(lobbyId);

            Assert.False(result);
        }

        [Fact]
        public async Task EndStroke_ShouldStoreEndEventAndRelayToDescriber()
        {
            var lobbyId = "lobby123";
            var strokeId = "stroke1";
            var describerConn = "describer-conn";

            var lobby = new Lobby {};
            lobby.Players.Add(new Player { ConnectionId = describerConn, Role = PlayerRole.Explainer });

            _mockLobbyService.Setup(s => s.LobbyExists(lobbyId)).Returns(true);
            _mockLobbyService.Setup(s => s.GetLobby(lobbyId)).Returns(lobby);

            await _hub.EndStroke(lobbyId, strokeId);

            _mockDrawingStore.Verify(s => s.AppendStrokeEnded(lobbyId, strokeId), Times.Once);
            _mockDrawingRelay.Verify(r => r.RelayStrokeEnded(
                It.Is<StrokeEndedDto>(dto => dto.StrokeId == strokeId),
                describerConn), Times.Once);
        }

        [Fact]
        public async Task AddStrokePoints_ShouldNotProcess_WhenPointsListIsEmpty()
        {
            var lobbyId = "lobby123";
            var strokeId = "stroke1";
            var emptyPoints = new List<PointDto>();

            await _hub.AddStrokePoints(lobbyId, strokeId, emptyPoints);

            _mockDrawingStore.Verify(s => s.AppendStrokePoints(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<PointDto>>()), Times.Never);
        }

        [Fact]
        public async Task AddStrokePoints_ShouldNotProcess_WhenPointsListIsNull()
        {
            var lobbyId = "lobby123";
            var strokeId = "stroke1";

            await _hub.AddStrokePoints(lobbyId, strokeId, null);

            _mockDrawingStore.Verify(s => s.AppendStrokePoints(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<PointDto>>()), Times.Never);
        }

        [Fact]
        public async Task RedoLast_ShouldResetCanvasWhenSuccessful()
        {
            var lobbyId = "lobby123";
            var events = new List<DrawingEventBase>
            {
                new StrokeStartedEvent("s1", "#FF0000", 2, "pen"),
                new StrokeEndedEvent("s1")
            };

            _mockDrawingStore.Setup(s => s.RedoLast(lobbyId)).Returns(true);
            _mockDrawingStore.Setup(s => s.GetActiveEvents(lobbyId)).Returns(events);
            _mockClients.Setup(c => c.Group(lobbyId)).Returns(_mockClientProxy.Object);

            var result = await _hub.RedoLast(lobbyId);

            Assert.True(result);
            _mockDrawingStore.Verify(s => s.RedoLast(lobbyId), Times.Once);
            _mockClientProxy.Verify(c => c.SendCoreAsync("CanvasReset",
                It.IsAny<object[]>(), default), Times.Once);
        }

        [Fact]
        public async Task RedoLast_ShouldReturnFalse_WhenRedoFails()
        {
            var lobbyId = "lobby123";
            _mockDrawingStore.Setup(s => s.RedoLast(lobbyId)).Returns(false);

            var result = await _hub.RedoLast(lobbyId);

            Assert.False(result);
            _mockDrawingStore.Verify(s => s.GetActiveEvents(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UndoLast_ShouldReturnFalse_WhenUndoFails()
        {
            var lobbyId = "lobby123";
            _mockDrawingStore.Setup(s => s.UndoLast(lobbyId)).Returns(false);

            var result = await _hub.UndoLast(lobbyId);

            Assert.False(result);
            _mockDrawingStore.Verify(s => s.GetActiveEvents(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetDrawingEvents_ShouldReturnProjectedEvents()
        {
            var lobbyId = "lobby123";
            var events = new List<DrawingEventBase>
            {
                new StrokeStartedEvent("s1", "#000000", 1.5, "pen"),
                new StrokePointsEvent ("s1", new List<PointDto> { new (10, 20) } ),
                new StrokeEndedEvent ("s1"),
                new CanvasClearedEvent()
            };

            _mockDrawingStore.Setup(s => s.GetActiveEvents(lobbyId)).Returns(events);

            var result = await _hub.GetDrawingEvents(lobbyId);

            Assert.Equal(4, result.Length);
            _mockDrawingStore.Verify(s => s.GetActiveEvents(lobbyId), Times.Once);
        }

        [Fact]
        public async Task AnnounceDrawing_ShouldBroadcastDrawingUrl()
        {
            var lobbyId = "lobby123";
            var drawingUrl = "/drawings/test-drawing.png";

            _mockLobbyService.Setup(s => s.LobbyExists(lobbyId)).Returns(true);
            _mockClients.Setup(c => c.Group(lobbyId)).Returns(_mockClientProxy.Object);

            await _hub.AnnounceDrawing(lobbyId, drawingUrl);

            _mockClientProxy.Verify(c => c.SendCoreAsync("DrawingSaved",
                It.Is<object[]>(args => args[0].ToString() == drawingUrl),
                default), Times.Once);
        }

        [Fact]
        public async Task AnnounceDrawing_ShouldThrowHubException_WhenLobbyDoesNotExist()
        {
            var lobbyId = "nonexistent";
            var drawingUrl = "/drawings/test.png";

            _mockLobbyService.Setup(s => s.LobbyExists(lobbyId)).Returns(false);

            await Assert.ThrowsAsync<HubException>(() => _hub.AnnounceDrawing(lobbyId, drawingUrl));
        }

        [Fact]
        public async Task GoToFinal_ShouldThrowHubException_WhenLobbyDoesNotExist()
        {
            var lobbyId = "nonexistent";
            _mockLobbyService.Setup(s => s.LobbyExists(lobbyId)).Returns(false);

            await Assert.ThrowsAsync<HubException>(() => _hub.GoToFinal(lobbyId));
        }

        [Fact]
        public async Task BeginStroke_ShouldBroadcastToGroup_WhenNoDescriberFound()
        {
            var lobbyId = "lobby123";
            var strokeId = "stroke1";
            var color = "#00FF00";
            var width = 3.0;
            var tool = "marker";

            _mockLobbyService.Setup(s => s.LobbyExists(lobbyId)).Returns(false);
            _mockClients.Setup(c => c.GroupExcept(lobbyId, It.IsAny<IReadOnlyList<string>>()))
                .Returns(_mockClientProxy.Object);

            await _hub.BeginStroke(lobbyId, strokeId, color, width, tool);

            _mockDrawingStore.Verify(s => s.AppendStrokeStarted(lobbyId, strokeId, color, width, tool), Times.Once);
            _mockClientProxy.Verify(c => c.SendCoreAsync("StrokeStarted",
                It.Is<object[]>(args => args[0].ToString() == strokeId && args[1].ToString() == color),
                default), Times.Once);
        }
    }
}
