using System;

namespace GameApp.Service.Exceptions
{
    public class LobbyFullException : InvalidOperationException
    {
        public string LobbyId { get; }
        public int MaxPlayers { get; }

        public LobbyFullException(string lobbyId, int maxPlayers)
            : base($"Lobby '{lobbyId}' is full. Maximum players: {maxPlayers}.")
        {
            LobbyId = lobbyId;
            MaxPlayers = maxPlayers;
        }

        public LobbyFullException(string lobbyId, int maxPlayers, string message)
            : base(message)
        {
            LobbyId = lobbyId;
            MaxPlayers = maxPlayers;
        }

        public LobbyFullException(string lobbyId, int maxPlayers, string message, Exception inner)
            : base(message, inner)
        {
            LobbyId = lobbyId;
            MaxPlayers = maxPlayers;
        }
    }
}