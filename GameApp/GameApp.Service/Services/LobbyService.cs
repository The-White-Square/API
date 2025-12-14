using GameApp.Service.Models;
using GameApp.Service.Extensions;
using GameApp.Service.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using GameApp.Service.Exceptions;
using GameApp.Service.Dtos;

namespace GameApp.Service.Services;

public class LobbyService : ILobbyService
{
    private readonly ConcurrentDictionary<string, Lobby> _lobbies = new();
    private readonly IGalleryService _gallery;
    private readonly ILobbyCodeGenerator _codeGenerator;
    private readonly ILogger<LobbyService> _logger;
    private readonly ILobbyRepository _lobbyRepo;
    private readonly IPlayerRepository _playerRepo;

    public LobbyService(
        IGalleryService gallery,
        ILobbyRepository lobbyRepo,
        IPlayerRepository playerRepo,
        ILobbyCodeGenerator codeGenerator,
        ILogger<LobbyService> logger)
    {
        _gallery = gallery;
        _lobbyRepo = lobbyRepo;
        _playerRepo = playerRepo;
        _codeGenerator = codeGenerator;
        _logger = logger;
        _logger.LogInformation("LobbyService initialized.");
    }

    public Lobby GetLobby(string lobbyId) => EnsureLobbyExists(lobbyId);

    public bool LobbyExists(string lobbyId)
    {
        // check in-memory
        if (_lobbies.ContainsKey(lobbyId))
            return true;

        // attempt to load from persistence
        var loaded = _lobbyRepo.GetByCode(lobbyId);
        if (loaded is not null)
        {
            // hydrate players from persistence
            var persistedPlayers = _playerRepo.GetByLobby(loaded.Id);
            foreach (var p in persistedPlayers)
            {
                // ensure ConnectionId is null (it will be set on join) but other properties are kept
                if (!_lobbies.TryGetValue(lobbyId, out var _))
                {
                    // will add lobby below
                }
            }

            _lobbies.TryAdd(lobbyId, loaded);

            // Add players into in-memory lobby
            var inMemLobby = _lobbies[lobbyId];
            if (inMemLobby.Players.Count == 0 && persistedPlayers.Count > 0)
            {
                inMemLobby.Players.AddRange(persistedPlayers);
                _logger.LogDebug("Hydrated {Count} players into lobby {LobbyId} from persistence.", persistedPlayers.Count, lobbyId);
            }

            _logger.LogDebug("Lobby {LobbyId} loaded from persistence into memory.", lobbyId);
            return true;
        }

        return false;
    }
    private Lobby EnsureLobbyExists(string lobbyId)
    {
        if (_lobbies.TryGetValue(lobbyId, out var existing))
            return existing;

        // attempt to load from persistence first
        var loaded = _lobbyRepo.GetByCode(lobbyId);
        if (loaded is not null)
        {
            _lobbies.TryAdd(lobbyId, loaded);

            // hydrate players from persistence
            var persistedPlayers = _playerRepo.GetByLobby(loaded.Id);
            if (persistedPlayers.Count > 0)
            {
                var inMemLobby = _lobbies[lobbyId];
                // avoid duplicates if any were already present
                var existingNames = new HashSet<string>(inMemLobby.Players.Select(p => p.DisplayName), StringComparer.OrdinalIgnoreCase);
                foreach (var p in persistedPlayers)
                {
                    if (!existingNames.Contains(p.DisplayName))
                        inMemLobby.Players.Add(p);
                }
                _logger.LogDebug("Hydrated {Count} players into lobby {LobbyId}.", persistedPlayers.Count, lobbyId);
            }
            return loaded;
        }

        // create if missing
        return CreateLobbyInternal(lobbyId);
    }

    public IEnumerable<Lobby> GetAllLobbies() => _lobbies.Values;

    public void AddPlayer(Player player, string lobbyId)
    {
        var lobby = EnsureLobbyExists(lobbyId);

        // First check if a player with the same display name exists in-memory
        var existingInMemory = lobby.Players.FirstOrDefault(p => string.Equals(p.DisplayName, player.DisplayName, StringComparison.OrdinalIgnoreCase));
        if (existingInMemory is null)
        {
            // If adding a new player would exceed maximum allowed players, throw
            if (lobby.Players.Count >= 2)
            {
                _logger.LogWarning("Cannot add player {Player} to lobby {LobbyId}: lobby full (max 2).", player.DisplayName, lobbyId);
                throw new LobbyFullException(lobbyId, 2);
            }

            player.LobbyId = lobby.Id;
            lobby.Players.Add(player);
            _logger.LogInformation("Player {Player} added to lobby {LobbyId}. Player count now {Count}.",
                player.DisplayName, lobbyId, lobby.Players.Count);
        }
        else
        {
            // Update in-memory player to keep consistency with persistence update logic below
            existingInMemory.iconId = player.iconId;
            existingInMemory.Role = player.Role;
            if (!string.IsNullOrEmpty(player.ConnectionId))
                existingInMemory.ConnectionId = player.ConnectionId;

            _logger.LogInformation("Player {Player} updated in-memory in lobby {LobbyId}.", player.DisplayName, lobbyId);
        }

        // Persist add/update
        var existing = _playerRepo.GetByLobbyAndName(lobby.Id, player.DisplayName);
        if (existing is null)
        {
            var dbPlayer = new Player(player.DisplayName, player.iconId)
            {
                LobbyId = lobby.Id,
                Role = player.Role,
                ConnectionId = player.ConnectionId
            };
            _playerRepo.Add(dbPlayer);
            _logger.LogDebug("Persisted new player {Player} in lobby {LobbyId}.", player.DisplayName, lobbyId);
        }
        else
        {
            existing.iconId = player.iconId;
            existing.Role = player.Role;
            existing.ConnectionId = player.ConnectionId;
            _playerRepo.Update(existing);
            _logger.LogDebug("Updated existing player {Player} in lobby {LobbyId}.", player.DisplayName, lobbyId);
        }
        _playerRepo.SaveChanges();
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
            _lobbyRepo.Add(lobby);
            _lobbyRepo.SaveChanges();
            return lobby;
        }

        return _lobbies[lobbyCode];
    }

    public void JoinLobby(string lobbyId)
    {
        // Ensure lobby exists so subsequent operations have a consistent view
        var lobby = EnsureLobbyExists(lobbyId);
        _logger.LogDebug("JoinLobby invoked for {LobbyId}. Players: {Count}", lobbyId, lobby.Players.Count);
    }

    public void AddOrUpdatePlayerConnection(string lobbyId, string displayName, int iconId, string connectionId)
    {
        var lobby = EnsureLobbyExists(lobbyId);

        var player = lobby.Players.FirstOrDefault(p => string.Equals(p.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        if (player is null)
        {
            player = new Player(displayName, iconId) { ConnectionId = connectionId, LobbyId = lobby.Id };
            lobby.Players.Add(player);
            _logger.LogInformation("Player connection added: {Player} to lobby {LobbyId}", displayName, lobbyId);
        }
        else
        {
            player.ConnectionId = connectionId;
            player.iconId = iconId;
            _logger.LogInformation("Player connection updated: {Player} in lobby {LobbyId}", displayName, lobbyId);
        }

        var dbPlayer = _playerRepo.GetByLobbyAndName(lobby.Id, displayName);
        if (dbPlayer is null)
        {
            dbPlayer = new Player(displayName, iconId)
            {
                LobbyId = lobby.Id,
                ConnectionId = connectionId
            };
            _playerRepo.Add(dbPlayer);
        }
        else
        {
            dbPlayer.iconId = iconId;
            dbPlayer.ConnectionId = connectionId;
            _playerRepo.Update(dbPlayer);
        }
        _playerRepo.SaveChanges();
    }

    public ImageDto? GetOrAssignLobbyImage(string lobbyId)
    {
        var lobby = EnsureLobbyExists(lobbyId);

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
        var lobbyRow = _lobbyRepo.GetById(lobby.Id);
        if (lobbyRow is not null)
        {
            lobbyRow.SelectedImageId = lobby.SelectedImageId;
            lobbyRow.SelectedImageUrl = lobby.SelectedImageUrl;
            _lobbyRepo.Update(lobbyRow);
            _lobbyRepo.SaveChanges();
        }

        _logger.LogInformation("Assigned image {ImageId} to lobby {LobbyId}.", picked.Id, lobbyId);
        return picked;
    }

    public string? GetLobbySelectedImagePath(string lobbyId)
    {
        var lobby = EnsureLobbyExists(lobbyId);
        if (string.IsNullOrEmpty(lobby.SelectedImageId))
            return null;
        return _gallery.GetImageFilePath(lobby.SelectedImageId);
    }

    // assign roles for the lobby, pick or reuse a lobby image (returns null if not enough players / lobby missing).
    public RolesAssignment? AssignRoles(string lobbyId)
    {
        var lobby = EnsureLobbyExists(lobbyId);

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

        var dbPlayers = _playerRepo.GetByLobbyIds(lobby.Id, new[] { describer.DisplayName, drawer.DisplayName });
        foreach (var p in dbPlayers)
        {
            if (p.DisplayName == describer.DisplayName) p.Role = PlayerRole.Explainer;
            if (p.DisplayName == drawer.DisplayName) p.Role = PlayerRole.Artist;
            _playerRepo.Update(p);
        }
        if (dbPlayers.Count > 0) _playerRepo.SaveChanges();

        _logger.LogInformation("Roles assigned in lobby {LobbyId}: Describer={Describer}, Drawer={Drawer}, ImageAssigned={HasImage}",
            lobbyId, describer.DisplayName, drawer.DisplayName, image is not null);

        return new RolesAssignment(describer.ConnectionId, drawer.ConnectionId, describer, drawer, image);
    }
}