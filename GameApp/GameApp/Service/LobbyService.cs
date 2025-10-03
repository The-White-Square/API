using GameApp.LobbySystem;

namespace GameApp.Service;

public class LobbyService
{
    private readonly Dictionary<String, Lobby> _lobbies = new();

    public Lobby getLobby(string lobbyId) =>  _lobbies[lobbyId];

    public void addPlayer(Player player, string lobbyId)
    {
        if(!_lobbies.ContainsKey(lobbyId))
            _lobbies[lobbyId] = new Lobby(lobbyId);
    }

    public void createLobby(string lobbyId)
    {
        _lobbies[lobbyId] = new Lobby(lobbyId);
    }

}