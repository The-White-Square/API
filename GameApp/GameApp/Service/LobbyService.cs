using GameApp.Controllers;
using GameApp.LobbySystem;
using GameApp.Service.Extensions;
using GameApp.Utils;
using GameApp.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace GameApp.Service;

public class LobbyService : ILobbyService
{
    private readonly ConcurrentDictionary<string, Lobby> _lobbies = new();
    private readonly IGalleryService _gallery;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILobbyCodeGenerator _codeGenerator;
    private readonly ILogger<LobbyService> _logger;

    public LobbyService(
        IGalleryService gallery,
        IDbContextFactory<AppDbContext> dbFactory,
        ILobbyCodeGenerator codeGenerator,
        ILogger<LobbyService> logger)
    {
        _gallery = gallery;
        _dbFactory = dbFactory;
        _codeGenerator = codeGenerator;
        _logger = logger;
        _logger.LogInformation("LobbyService initialized.");
    }

    public Lobby GetLobby(string lobbyId) => _lobbies[lobbyId];

    public bool LobbyExists(string lobbyId) => _lobbies.ContainsKey(lobbyId);
    public IEnumerable<Lobby> GetAllLobbies() => _lobbies.Values;

    public void AddPlayer(Player player, string lobbyId)
    {
        var lobby = EnsureLobbyExists(lobbyId);

        lobby.Players.Add(player);
        _logger.LogInformation("Player {Player} added to lobby {LobbyId}. Player count now {Count}.",
            player.DisplayName, lobbyId, lobby.Players.Count);

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
            _logger.LogDebug("Persisted new player {Player} in lobby {LobbyId}.", player.DisplayName, lobbyId);
        }
        else
        {
            existing.iconId = player.iconId;
            existing.Role = player.Role;
            existing.ConnectionId = player.ConnectionId;
            db.Players.Update(existing);
            _logger.LogDebug("Updated existing player {Player} in lobby {LobbyId}.", player.DisplayName, lobbyId);
        }
        db.SaveChanges();
    }

    public Lobby CreateLobby()
    {
        var code = _codeGenerator.Generate();
        var lobby = CreateLobbyInternal(code);
        _logger.LogInformation("Created lobby {LobbyCode}", lobby.LobbyCode);
        return lobby;
    }

    private Lobby CreateLobbyInternal(string lobbyCode)
    {
        // Atomically add to in-memory store, then persist only if we were the thread that added it
        var lobby = new Lobby(lobbyCode);
        if (_lobbies.TryAdd(lobbyCode, lobby))
        {
            using var db = _dbFactory.CreateDbContext();
            db.Lobbies.Add(lobby);
            db.SaveChanges();
            return lobby;
        }

        return _lobbies[lobbyCode];
    }

    private Lobby EnsureLobbyExists(string lobbyId)
    {
        if (_lobbies.TryGetValue(lobbyId, out var existing))
            return existing;

        // Attempt to create if missing; handles races safely
        return CreateLobbyInternal(lobbyId);
    }

    public void JoinLobby(string lobbyId)
    {
        _logger.LogDebug("JoinLobby invoked for {LobbyId}. Exists: {Exists}", lobbyId, LobbyExists(lobbyId));
    }

    public void AddOrUpdatePlayerConnection(string lobbyId, string displayName, int iconId, string connectionId)
    {
        var lobby = EnsureLobbyExists(lobbyId);

        var player = lobby.Players.FirstOrDefault(p => string.Equals(p.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        if (player is null)
        {
            player = new Player(displayName, iconId) { ConnectionId = connectionId };
            lobby.Players.Add(player);
            _logger.LogInformation("Player connection added: {Player} to lobby {LobbyId}", displayName, lobbyId);
        }
        else
        {
            player.ConnectionId = connectionId;
            player.iconId = iconId;
            _logger.LogInformation("Player connection updated: {Player} in lobby {LobbyId}", displayName, lobbyId);
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
        {
            _logger.LogWarning("GetOrAssignLobbyImage: Lobby {LobbyId} not found.", lobbyId);
            return null;
        }

        if (!string.IsNullOrEmpty(lobby.SelectedImageId))
        {
            var path = _gallery.GetImageFilePath(lobby.SelectedImageId);
            if (path is not null)
            {
                _logger.LogDebug("Reusing existing lobby image {ImageId} for lobby {LobbyId}.",
                    lobby.SelectedImageId, lobbyId);
                return new ImageDto(lobby.SelectedImageId!, $"/images/{lobby.SelectedImageId}", new FileInfo(path).Length);
            }
            lobby.SelectedImageId = null;
            lobby.SelectedImageUrl = null;
            _logger.LogInformation("Previously assigned image missing; reselecting for lobby {LobbyId}.", lobbyId);
        }

        var picked = _gallery.GetRandomImage();
        if (picked is null)
        {
            _logger.LogWarning("No images available to assign to lobby {LobbyId}.", lobbyId);
            return null;
        }

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

        _logger.LogInformation("Assigned image {ImageId} to lobby {LobbyId}.", picked.Id, lobbyId);
        return picked;
    }

    public string? GetLobbySelectedImagePath(string lobbyId)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby) || string.IsNullOrEmpty(lobby.SelectedImageId))
            return null;
        return _gallery.GetImageFilePath(lobby.SelectedImageId);
    }

    // assign roles for the lobby, pick or reuse a lobby image (returns null if not enough players / lobby missing).
    public RolesAssignment? AssignRoles(string lobbyId)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby))
        {
            _logger.LogWarning("AssignRoles: Lobby {LobbyId} not found.", lobbyId);
            return null;
        }
        if (lobby.Players.Count < 2)
        {
            _logger.LogWarning("AssignRoles: Not enough players in lobby {LobbyId}. Count: {Count}", lobbyId, lobby.Players.Count);
            return null;
        }

        var candidates = lobby.Players.Where(p => !string.IsNullOrEmpty(p.ConnectionId)).ToList();
        if (candidates.Count < 2)
        {
            candidates = lobby.Players.Take(2).ToList();
            if (candidates.Count < 2)
            {
                _logger.LogWarning("AssignRoles fallback failed: lobby {LobbyId} still lacks players.", lobbyId);
                return null;
            }
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

        _logger.LogInformation("Roles assigned in lobby {LobbyId}: Describer={Describer}, Drawer={Drawer}, ImageAssigned={HasImage}",
            lobbyId, describer.DisplayName, drawer.DisplayName, image is not null);

        return new RolesAssignment(describer.ConnectionId, drawer.ConnectionId, describer, drawer, image);
    }
}