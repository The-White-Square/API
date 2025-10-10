using GameApp.Controllers;
using GameApp.Hubs;
using GameApp.LobbySystem;
using GameApp.Utils;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Hosting;

namespace GameApp.Service;

public class LobbyService
{
    private readonly Dictionary<string, Lobby> _lobbies = new();
    private readonly GalleryService _gallery;

    public LobbyService(GalleryService gallery)
    {
        _gallery = gallery;
    }

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

    public Lobby CreateLobby() => CreateLobby(LobbyUtils.GenerateLobbyCode());

    private Lobby CreateLobby(string lobbyId)
    {
        _lobbies[lobbyId] = new Lobby(lobbyId);
        return _lobbies[lobbyId];
    }

    public void JoinLobby(string lobbyId)
    {
        
    }

    // get random image from gallery (GalleryService call)
    public ImageDto? GetOrAssignLobbyImage(string lobbyId)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby))
            return null;

        if (!string.IsNullOrEmpty(lobby.SelectedImageId))
        {
            var path = _gallery.GetImageFilePath(lobby.SelectedImageId);
            if (path is not null)
            {
                return new ImageDto(lobby.SelectedImageId!, $"/images/{lobby.SelectedImageId}", new FileInfo(path).Length);
            }
            // previously assigned file missing -> clear and pick a new one
            lobby.SelectedImageId = null;
            lobby.SelectedImageUrl = null;
        }

        var picked = _gallery.GetRandomImage();
        if (picked is null) return null;

        lobby.SelectedImageId = picked.Id;
        lobby.SelectedImageUrl = picked.Url;
        return picked;
    }

    public string? GetLobbySelectedImagePath(string lobbyId)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby) || string.IsNullOrEmpty(lobby.SelectedImageId))
            return null;
        return _gallery.GetImageFilePath(lobby.SelectedImageId);
    }
}