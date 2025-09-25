namespace GameApp.LobbySystem;

public class Lobby
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public LobbyCode Code { get; init; }
    public string Phase { get; set; } = "Waiting"; // Waiting, GameInProgress, End
    public List<Player> Players { get; } = new();

    public Lobby(LobbyCode code) => Code = code;
}
