using GameApp.Service.Models;
using GameApp.Service.Services;
using Microsoft.EntityFrameworkCore;

namespace GameApp.Integration.Data;

public class EfPlayerRepository : IPlayerRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public EfPlayerRepository(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public Player? GetByLobbyAndName(Guid lobbyId, string displayName)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Players.FirstOrDefault(p => p.LobbyId == lobbyId && p.DisplayName == displayName);
    }

    public List<Player> GetByLobbyIds(Guid lobbyId, IEnumerable<string> displayNames)
    {
        using var db = _dbFactory.CreateDbContext();
        var names = displayNames.ToList();
        return db.Players.Where(p => p.LobbyId == lobbyId && names.Contains(p.DisplayName)).ToList();
    }

    // load all players of a lobby
    public List<Player> GetByLobby(Guid lobbyId)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Players.Where(p => p.LobbyId == lobbyId).ToList();
    }

    public void Add(Player player)
    {
        using var db = _dbFactory.CreateDbContext();
        db.Players.Add(player);
        db.SaveChanges();
    }

    public void Update(Player player)
    {
        using var db = _dbFactory.CreateDbContext();
        db.Players.Update(player);
        db.SaveChanges();
    }

    // No-op; kept for interface compatibility.
    public void SaveChanges() { }
}