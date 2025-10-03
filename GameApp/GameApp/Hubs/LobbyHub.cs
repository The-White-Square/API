using GameApp.LobbySystem;
using Microsoft.AspNetCore.SignalR;

namespace GameApp.Hubs;

public class LobbyHub : Hub
{
    public async Task JoinLobby(string lobbyId, string playerName, byte iconId)
    {
        // to do: ensure player uniqueness, dissalow null name and limit name lenght
        await Groups.AddToGroupAsync(Context.ConnectionId, lobbyId);
        await Clients.Group(lobbyId).SendAsync("JoinLobby", lobbyId, playerName, iconId);
    }
    
    
}