using Microsoft.AspNetCore.SignalR;
using GameApp.Service.Dtos;           // your drawing DTOs
using GameApp.Service.Models;       // PlayerRole
using GameApp.Service.Services;  // ILobbyService
using GameApp.Service.Utils;     // IDrawingRelay

namespace GameApp.Application.Hubs
{
    public class LobbyHub : Hub
    {
        private readonly ILobbyService _lobbyService;
        private readonly IDrawingRelay _drawingRelay;

        public LobbyHub(ILobbyService lobbyService, IDrawingRelay drawingRelay)
        {
            _lobbyService = lobbyService;
            _drawingRelay = drawingRelay;
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
            var target = GetDescriberConnection(lobbyId, Context.ConnectionId);
            if (target is null) return; // silently ignore if no describer yet

            var dto = new StrokeStartedDto(lobbyId, strokeId, color, width, tool);
            await _drawingRelay.RelayStrokeStarted(dto, target);
        }

        // drawer sends batched points
        public async Task AddStrokePoints(string lobbyId, string strokeId, List<PointDto> points)
        {
            if (points is null || points.Count == 0) return;
            var target = GetDescriberConnection(lobbyId, Context.ConnectionId);
            if (target is null) return;

            var dto = new StrokePointsDto(lobbyId, strokeId, points);
            await _drawingRelay.RelayStrokePoints(dto, target);
        }

        public async Task EndStroke(string lobbyId, string strokeId)
        {
            var target = GetDescriberConnection(lobbyId, Context.ConnectionId);
            if (target is null) return;

            var dto = new StrokeEndedDto(lobbyId, strokeId);
            await _drawingRelay.RelayStrokeEnded(dto, target);
        }

        public async Task ClearCanvas(string lobbyId)
        {
            var target = GetDescriberConnection(lobbyId, Context.ConnectionId);
            if (target is null) return;

            var dto = new CanvasClearedDto(lobbyId);
            await _drawingRelay.RelayCanvasCleared(dto, target);
        }

        // helper obtains describer connection
        private string? GetDescriberConnection(string lobbyId, string callerConnection)
        {
            if (!_lobbyService.LobbyExists(lobbyId)) return null;
            var lobby = _lobbyService.GetLobby(lobbyId);
            var describer = lobby.Players.FirstOrDefault(p => p.Role == PlayerRole.Explainer);
            if (describer is null) return null;
            if (describer.ConnectionId == callerConnection) return null; // caller is describer; ignore
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
