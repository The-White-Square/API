using GameApp.Service.Models;
using GameApp.Service.Services;
using Microsoft.EntityFrameworkCore;

namespace GameApp.Integration.Data;

public class EfLobbyRepository : ILobbyRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public EfLobbyRepository(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public Lobby? GetByCode(string code)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Lobbies.AsNoTracking().FirstOrDefault(l => l.LobbyCode == code);
    }

    public Lobby? GetById(Guid id)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.Lobbies.FirstOrDefault(l => l.Id == id);
    }

    public void Add(Lobby lobby)
    {
        using var db = _dbFactory.CreateDbContext();
        db.Lobbies.Add(lobby);
        db.SaveChanges();
    }

    public void Update(Lobby lobby)
    {
        using var db = _dbFactory.CreateDbContext();
        db.Lobbies.Update(lobby);
        db.SaveChanges();
    }

    // With per-operation contexts, SaveChanges is a no-op to keep interface compatibility.
    public void SaveChanges() { }
}