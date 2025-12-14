using GameApp.Service.Models;
using GameApp.Service.Services;
using Microsoft.EntityFrameworkCore;

namespace GameApp.Integration.Data;

public class EfPlayerRepository : IPlayerRepository
{
    private readonly AppDbContext _db;

    public EfPlayerRepository(AppDbContext db)
    {
        _db = db;
    }

    public Player? GetByLobbyAndName(Guid lobbyId, string displayName)
        => _db.Players.FirstOrDefault(p => p.LobbyId == lobbyId && p.DisplayName == displayName);

    public List<Player> GetByLobbyIds(Guid lobbyId, IEnumerable<string> displayNames)
    {
        var names = displayNames.ToList();
        return _db.Players.Where(p => p.LobbyId == lobbyId && names.Contains(p.DisplayName)).ToList();
    }

    public void Add(Player player)
        => _db.Players.Add(player);

    public void Update(Player player)
        => _db.Players.Update(player);

    public void SaveChanges()
        => _db.SaveChanges();
}