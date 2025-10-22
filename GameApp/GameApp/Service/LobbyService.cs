using GameApp.Controllers;
using GameApp.Hubs;
using GameApp.LobbySystem;
using GameApp.Service.Extensions;
using GameApp.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;

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

    // Add or update a player, link the SignalR connection id
    public void AddOrUpdatePlayerConnection(string lobbyId, string displayName, int iconId, string connectionId)
    {
        if (!_lobbies.ContainsKey(lobbyId))
            CreateLobby(lobbyId);

        var lobby = _lobbies[lobbyId];
        var player = lobby.Players.FirstOrDefault(p => string.Equals(p.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        if (player is null)
        {
            player = new Player(displayName, iconId) { ConnectionId = connectionId };
            lobby.Players.Add(player);
        }
        else
        {
            player.ConnectionId = connectionId;
            player.iconId = iconId;
        }
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
            // pick a new file if assigned file missing
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

    // result object for role assignment
    public record RolesAssignment(string? DescriberConnectionId, string? DrawerConnectionId, Player Describer, Player Drawer, ImageDto? Image);

    // assign roles for the lobby, pick or reuse a lobby image (returns null if not enough players / lobby missing).
    public RolesAssignment? AssignRoles(string lobbyId)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return null;
        if (lobby.Players.Count < 2) return null;

        var candidates = lobby.Players.Where(p => !string.IsNullOrEmpty(p.ConnectionId)).ToList();
        if (candidates.Count < 2)
        {
            // fallback: take first two players even without connection id
            candidates = lobby.Players.Take(2).ToList();
            if (candidates.Count < 2) return null;
        }

        // pick describer using existing extension GetRandom
        var describer = candidates.GetRandom()!; // non-null because candidates.Count >= 2
        var drawer = candidates.First(p => !ReferenceEquals(p, describer));

        // set roles
        describer.Role = PlayerRole.Explainer;
        drawer.Role = PlayerRole.Artist;

        // assign or get lobby image
        var image = GetOrAssignLobbyImage(lobbyId);

        return new RolesAssignment(describer.ConnectionId, drawer.ConnectionId, describer, drawer, image);
        }
    }