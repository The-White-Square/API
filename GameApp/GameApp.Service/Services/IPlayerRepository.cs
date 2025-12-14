using GameApp.Service.Models;

namespace GameApp.Service.Services;

public interface IPlayerRepository
{
    Player? GetByLobbyAndName(Guid lobbyId, string displayName);
    List<Player> GetByLobbyIds(Guid lobbyId, IEnumerable<string> displayNames);
    List<Player> GetByLobby(Guid lobbyId);
    void Add(Player player);
    void Update(Player player);
    void SaveChanges();
}