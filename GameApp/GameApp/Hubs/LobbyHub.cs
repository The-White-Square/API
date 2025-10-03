using GameApp.LobbySystem;
using Microsoft.AspNetCore.SignalR;

namespace GameApp.Hubs;

public class LobbyHub : Hub
{
    public async Task AddPlayerToLobby(string lobbyId, string playerName, byte iconId)
    {
        // to do: ensure player uniqueness, dissalow null name and limit name lenght
        await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
        await Clients.Group(lobbyId).SendAsync("AddPlayerToLobby", lobbyId, playerName, iconId);
        
    }

    public async Task SendLobbyMessage(string lobbyId, string message, string playerName)
    {
        await Clients.Group(lobbyId).SendAsync("SendMessage", message, playerName);
    }
    
    
}