using GameApp.Service.Models;

namespace GameApp.Service.Services;

public interface ILobbyRepository
{
    Lobby? GetByCode(string code);
    Lobby? GetById(Guid id);
    void Add(Lobby lobby);
    void Update(Lobby lobby);
    void SaveChanges();
}