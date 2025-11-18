using GameApp.LobbySystem;
using GameApp.Service;
using Microsoft.AspNetCore.SignalR;

namespace GameApp.Hubs;

public class LobbyHub : Hub
{
    private readonly ILobbyService _lobbyService; // changed type

    public LobbyHub(ILobbyService lobbyService) // changed parameter
    {
        _lobbyService = lobbyService;
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

    public async Task SendLobbyMessage(string lobbyId, string message, string playerName)
    {
        await Clients.Group(lobbyId).SendAsync("LobbyMessage", message, playerName);
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
}
