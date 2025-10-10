using GameApp.Hubs;
using GameApp.LobbySystem;
using GameApp.Utils;
using Microsoft.AspNetCore.SignalR;

namespace GameApp.Service;

public class LobbyService
{
    private readonly Dictionary<string, Lobby> _lobbies = new();
    

    public Lobby GetLobby(string lobbyId) =>  _lobbies[lobbyId]; //returns lobby or null value

    public bool LobbyExists(string lobbyId)
    {
        return _lobbies.ContainsKey(lobbyId);
    }
    public IEnumerable<Lobby> GetAllLobbies() => _lobbies.Values;

    public void AddPlayer(Player player, string lobbyId)
    {
        if (!_lobbies.ContainsKey(lobbyId))
            CreateLobby();
        _lobbies[lobbyId].Players.Add(player);

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