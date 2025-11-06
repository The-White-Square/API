using GameApp.Controllers;
using GameApp.Hubs;
using GameApp.LobbySystem;
using GameApp.Service.Extensions;
using GameApp.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using GameApp.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace GameApp.Service;

public class LobbyService
{
    private readonly Dictionary<string, Lobby> _lobbies = new();
    private readonly GalleryService _gallery;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public LobbyService(GalleryService gallery, IDbContextFactory<AppDbContext> dbFactory)
    {
        _gallery = gallery;
        _dbFactory = dbFactory;
    }

    public Lobby GetLobby(string lobbyId) => _lobbies[lobbyId];

    public bool LobbyExists(string lobbyId) => _lobbies.ContainsKey(lobbyId);
    public IEnumerable<Lobby> GetAllLobbies() => _lobbies.Values;

    public void AddPlayer(Player player, string lobbyId)
    {
        if (!_lobbies.ContainsKey(lobbyId))
            CreateLobby(lobbyId);
        var lobby = _lobbies[lobbyId];

        // in-memory
        lobby.Players.Add(player);

        // persist
        using var db = _dbFactory.CreateDbContext();
        var existing = db.Players.FirstOrDefault(p => p.LobbyId == lobby.Id && p.DisplayName == player.DisplayName);
        if (existing is null)
        {
            var dbPlayer = new Player(player.DisplayName, player.iconId)
            {
                LobbyId = lobby.Id,
                Role = player.Role,
                ConnectionId = player.ConnectionId
            };
            db.Players.Add(dbPlayer);
        }
        else
        {
            existing.iconId = player.iconId;
            existing.Role = player.Role;
            existing.ConnectionId = player.ConnectionId;
            db.Players.Update(existing);
        }
        db.SaveChanges();
    }

    public Lobby CreateLobby() => CreateLobby(Utils.LobbyUtils.GenerateLobbyCode());

    private Lobby CreateLobby(string lobbyId)
    {
        var lobby = new Lobby(lobbyId);
        _lobbies[lobbyId] = lobby;

        using var db = _dbFactory.CreateDbContext();
        db.Lobbies.Add(lobby);
        db.SaveChanges();

        return lobby;
    }

    public void JoinLobby(string lobbyId)
    {
        // optional: track joins
    }

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

        using var db = _dbFactory.CreateDbContext();
        var dbPlayer = db.Players.FirstOrDefault(p => p.LobbyId == lobby.Id && p.DisplayName == displayName);
        if (dbPlayer is null)
        {
            dbPlayer = new Player(displayName, iconId)
            {
                LobbyId = lobby.Id,
                ConnectionId = connectionId
            };
            db.Players.Add(dbPlayer);
        }
        else
        {
            dbPlayer.iconId = iconId;
            dbPlayer.ConnectionId = connectionId;
            db.Players.Update(dbPlayer);
        }
        db.SaveChanges();
    }

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

        // persist selected image on lobby
        using var db = _dbFactory.CreateDbContext();
        var lobbyRow = db.Lobbies.FirstOrDefault(l => l.Id == lobby.Id);
        if (lobbyRow is not null)
        {
            lobbyRow.SelectedImageId = lobby.SelectedImageId;
            lobbyRow.SelectedImageUrl = lobby.SelectedImageUrl;
            db.SaveChanges();
        }

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

        var describer = candidates.GetRandom()!;
        var drawer = candidates.First(p => !ReferenceEquals(p, describer));

        describer.Role = PlayerRole.Explainer;
        drawer.Role = PlayerRole.Artist;

        var image = GetOrAssignLobbyImage(lobbyId);

        using var db = _dbFactory.CreateDbContext();
        var dbPlayers = db.Players.Where(p => p.LobbyId == lobby.Id &&
            (p.DisplayName == describer.DisplayName || p.DisplayName == drawer.DisplayName)).ToList();
        foreach (var p in dbPlayers)
        {
            if (p.DisplayName == describer.DisplayName) p.Role = PlayerRole.Explainer;
            if (p.DisplayName == drawer.DisplayName) p.Role = PlayerRole.Artist;
        }
        if (dbPlayers.Count > 0) db.SaveChanges();

        return new RolesAssignment(describer.ConnectionId, drawer.ConnectionId, describer, drawer, image);
    }
}