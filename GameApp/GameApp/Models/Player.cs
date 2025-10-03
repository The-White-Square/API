namespace GameApp.LobbySystem;

public class Player
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public PlayerRole Role { get; set; } = PlayerRole.None;
    public byte iconId { get; set; } = 0;
}
