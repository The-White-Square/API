using GameApp.Service.Models;
using GameApp.Service.Services;
using Microsoft.EntityFrameworkCore;

namespace GameApp.Integration.Data;

public class EfLobbyRepository : ILobbyRepository
{
    private readonly AppDbContext _db;

    public EfLobbyRepository(AppDbContext db)
    {
        _db = db;
    }

    public Lobby? GetByCode(string code)
        => _db.Lobbies.AsNoTracking().FirstOrDefault(l => l.LobbyCode == code);

    public Lobby? GetById(Guid id)
        => _db.Lobbies.FirstOrDefault(l => l.Id == id);

    public void Add(Lobby lobby)
        => _db.Lobbies.Add(lobby);

    public void Update(Lobby lobby)
        => _db.Lobbies.Update(lobby);

    public void SaveChanges()
        => _db.SaveChanges();
}