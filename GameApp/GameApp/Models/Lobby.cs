
namespace GameApp.LobbySystem;

public class Lobby
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string LobbyCode { get; set; }
    public string Phase { get; set; } = "Waiting"; // Waiting, GameInProgress, End
    public List<Player> Players { get; } = new();

    public Lobby(string lobbyCode) =>  this.LobbyCode = lobbyCode;
}
