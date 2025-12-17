using Microsoft.AspNetCore.SignalR;
using GameApp.Service.Dtos;
using GameApp.Service.Models;
using GameApp.Service.Services;
using GameApp.Service.Utils;

namespace GameApp.Application.Hubs
{
    public class LobbyHub : Hub
    {
        private readonly ILobbyService _lobbyService;
        private readonly IDrawingRelay _drawingRelay;
        private readonly IDrawingStore _drawingStore;

        public LobbyHub(ILobbyService lobbyService, IDrawingRelay drawingRelay, IDrawingStore drawingStore)
        {
            _lobbyService = lobbyService;
            _drawingRelay = drawingRelay;
            _drawingStore = drawingStore;
        }

        public async Task AddPlayerToLobby(string lobbyId, string playerName, int iconId)
        {
            // add connection to SignalR group
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);

            // register or update player with their connection id in server-side lobby store
            _lobbyService.AddOrUpdatePlayerConnection(lobbyId, playerName, iconId, Context.ConnectionId);

            // Send current players state to the caller so new joiner sees existing players
            try
            {
                var lobby = _lobbyService.GetLobby(lobbyId);
                var names = lobby.Players.Select(p => p.DisplayName).ToArray();
                await Clients.Caller.SendAsync("PlayersState", names);
            }
            catch
            {
                // ignore: lobby might not exist or be empty
            }

            // notify group that a player joined (including the caller)
            await Clients.Group(lobbyId).SendAsync("PlayerJoined", lobbyId, playerName, iconId);
        }

        public async Task SendLobbyMessage(string lobbyId, string message, string playerName, int iconId)
        {
            await Clients.Group(lobbyId).SendAsync("LobbyMessage", message, playerName, iconId);
        }

        // allow clients to invoke GetPlayers via SignalR
        public Task<string[]> GetPlayers(string lobbyId)
        {
            if (!_lobbyService.LobbyExists(lobbyId))
                throw new HubException("Lobby not found");

            var lobby = _lobbyService.GetLobby(lobbyId);
            var names = lobby.Players.Select(p => p.DisplayName).ToArray();
            return Task.FromResult(names);
        }

        // server-side role assignment. sends AssignedRole to each player and sends the image only to the describer.
        public async Task<bool> AssignRoles(string lobbyId)
        {
            var result = _lobbyService.AssignRoles(lobbyId);
            if (result is null) return false;

            var describerConn = result.DescriberConnectionId;
            var drawerConn = result.DrawerConnectionId;
            var image = result.Image;

            // Notify individual clients of their roles
            if (!string.IsNullOrEmpty(describerConn))
                await Clients.Client(describerConn).SendAsync("AssignedRole", result.Describer.Role.ToString());
            if (!string.IsNullOrEmpty(drawerConn))
                await Clients.Client(drawerConn).SendAsync("AssignedRole", result.Drawer.Role.ToString());

            // Send the image only to the describer (if available)
            if (image is not null && !string.IsNullOrEmpty(describerConn))
            {
                await Clients.Client(describerConn).SendAsync("ReceiveImage", image.Url);
            }

            // notify the whole group that roles were assigned
            await Clients.Group(lobbyId).SendAsync("RolesAssigned", result.Describer.DisplayName, result.Drawer.DisplayName);

            return true;
        }

        // drawer initiates a stroke
        public async Task BeginStroke(string lobbyId, string strokeId, string color, double width, string tool)
        {
            _drawingStore.AppendStrokeStarted(lobbyId, strokeId, color, width, tool);

            var target = GetDescriberConnection(lobbyId, Context.ConnectionId);
            var dto = new StrokeStartedDto(lobbyId, strokeId, color, width, tool);

            if (target is not null)
                await _drawingRelay.RelayStrokeStarted(dto, target);
            else
                await Clients.GroupExcept(lobbyId, new[] { Context.ConnectionId }).SendAsync("StrokeStarted", strokeId, color, width, tool);
        }

        public async Task AddStrokePoints(string lobbyId, string strokeId, List<PointDto> points)
        {
            if (points is null || points.Count == 0) return;

            _drawingStore.AppendStrokePoints(lobbyId, strokeId, points);

            var target = GetDescriberConnection(lobbyId, Context.ConnectionId);
            var dto = new StrokePointsDto(lobbyId, strokeId, points);

            if (target is not null)
                await _drawingRelay.RelayStrokePoints(dto, target);
            else
                await Clients.GroupExcept(lobbyId, new[] { Context.ConnectionId }).SendAsync("StrokePoints", strokeId, points);
        }

        public async Task EndStroke(string lobbyId, string strokeId)
        {
            _drawingStore.AppendStrokeEnded(lobbyId, strokeId);

            var target = GetDescriberConnection(lobbyId, Context.ConnectionId);
            var dto = new StrokeEndedDto(lobbyId, strokeId);

            if (target is not null)
                await _drawingRelay.RelayStrokeEnded(dto, target);
            else
                await Clients.GroupExcept(lobbyId, new[] { Context.ConnectionId }).SendAsync("StrokeEnded", strokeId);
        }

        public async Task ClearCanvas(string lobbyId)
        {
            _drawingStore.AppendCanvasCleared(lobbyId);

            var target = GetDescriberConnection(lobbyId, Context.ConnectionId);
            var dto = new CanvasClearedDto(lobbyId);

            if (target is not null)
                await _drawingRelay.RelayCanvasCleared(dto, target);
            else
                await Clients.GroupExcept(lobbyId, new[] { Context.ConnectionId }).SendAsync("CanvasCleared");
        }

        private static object Project(DrawingEventBase e) =>
            e switch
            {
                StrokeStartedEvent s => new { type = "StrokeStarted", strokeId = s.StrokeId, color = s.Color, width = s.Width, tool = s.Tool },
                StrokePointsEvent p => new { type = "StrokePoints", strokeId = p.StrokeId, points = p.Points.Select(pt => new { x = pt.X, y = pt.Y }).ToArray() },
                StrokeEndedEvent se => new { type = "StrokeEnded", strokeId = se.StrokeId },
                CanvasClearedEvent => new { type = "CanvasCleared" },
                _ => new { type = "Unknown" }
            };

        public Task<object[]> GetDrawingEvents(string lobbyId)
        {
            var wire = _drawingStore.GetActiveEvents(lobbyId).Select(Project).ToArray();
            return Task.FromResult(wire);
        }

        public async Task<bool> UndoLast(string lobbyId)
        {
            var ok = _drawingStore.UndoLast(lobbyId);
            if (!ok) return false;
            var wire = _drawingStore.GetActiveEvents(lobbyId).Select(Project).ToArray();
            await Clients.Group(lobbyId).SendAsync("CanvasReset", wire);
            return true;
        }

        public async Task<bool> RedoLast(string lobbyId)
        {
            var ok = _drawingStore.RedoLast(lobbyId);
            if (!ok) return false;
            var wire = _drawingStore.GetActiveEvents(lobbyId).Select(Project).ToArray();
            await Clients.Group(lobbyId).SendAsync("CanvasReset", wire);
            return true;
        }

        private string? GetDescriberConnection(string lobbyId, string callerConnection)
        {
            if (!_lobbyService.LobbyExists(lobbyId)) return null;
            var lobby = _lobbyService.GetLobby(lobbyId);
            var describer = lobby.Players.FirstOrDefault(p => p.Role == PlayerRole.Explainer);
            if (describer is null) return null;
            if (describer.ConnectionId == callerConnection) return null;
            return describer.ConnectionId;
        }

        // Broadcast GoToFinal so all clients in the lobby navigate to final page
        public async Task GoToFinal(string lobbyId)
        {
            if (!_lobbyService.LobbyExists(lobbyId))
                throw new HubException("Lobby not found");
            await Clients.Group(lobbyId).SendAsync("GoToFinal");
        }

        // New: announce a freshly saved drawing to the lobby so all clients can update immediately
        public async Task AnnounceDrawing(string lobbyId, string drawingUrl)
        {
            if (!_lobbyService.LobbyExists(lobbyId))
                throw new HubException("Lobby not found");

            // drawingUrl is expected to be the relative path like "/drawings/{file}.png"
            await Clients.Group(lobbyId).SendAsync("DrawingSaved", drawingUrl);
        }
    }
}
