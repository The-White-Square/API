using GameApp.LobbySystem;
using Microsoft.AspNetCore.SignalR;

namespace GameApp.Hubs;

public class LobbyHub : Hub
{
    public async Task AddPlayerToLobby(string lobbyId, string playerName, int iconId)
    {
        // to do: ensure player uniqueness, dissalow null name and limit name lenght
        await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
        await Clients.Group(lobbyId).SendAsync("PlayerJoined", lobbyId, playerName, iconId);
        
    }

    public async Task SendLobbyMessage(string lobbyId, string message, string playerName)
    {
        await Clients.Group(lobbyId).SendAsync("LobbyMessage", message, playerName);
    }
    
    
}