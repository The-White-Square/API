using GameApp.Controllers;
using GameApp.LobbySystem;

namespace GameApp.Service;

public interface ILobbyService
{
    Lobby GetLobby(string lobbyId);
    bool LobbyExists(string lobbyId);
    IEnumerable<Lobby> GetAllLobbies();

    void AddPlayer(Player player, string lobbyId);
    Lobby CreateLobby();
    void JoinLobby(string lobbyId);

    void AddOrUpdatePlayerConnection(string lobbyId, string displayName, int iconId, string connectionId);

    ImageDto? GetOrAssignLobbyImage(string lobbyId);
    string? GetLobbySelectedImagePath(string lobbyId);

    RolesAssignment? AssignRoles(string lobbyId);
}