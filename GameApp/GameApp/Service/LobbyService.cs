using GameApp.Hubs;
using GameApp.LobbySystem;
using GameApp.Utils;
using Microsoft.AspNetCore.SignalR;

namespace GameApp.Service;

public class LobbyService
{
    private readonly Dictionary<String, Lobby> _lobbies = new();
    
    private readonly IHubContext<LobbyHub> _hubContext;

    public LobbyService(IHubContext<LobbyHub> hubContext)
    {
        _hubContext = hubContext; 
    }

    public Lobby GetLobby(string lobbyId) =>  _lobbies[lobbyId]; //returns lobby or null value

    public bool LobbyExists(string lobbyId)
    {
        return _lobbies.ContainsKey(lobbyId);
    }
    public IEnumerable<Lobby> GetAllLobbies() => _lobbies.Values;

    public void AddPlayer(Player player, string lobbyId)
    {
        if(!_lobbies.ContainsKey(lobbyId))
            _lobbies[lobbyId] = new Lobby(lobbyId); 
    }

    public Lobby CreateLobby()
    {
        string lobbyId = LobbyUtils.GenerateLobbyCode();
        _lobbies[lobbyId] = new Lobby(lobbyId);
        
        return _lobbies[lobbyId];
    }
    
    public void JoinLobby(string lobbyId)
    {
        
    }

}