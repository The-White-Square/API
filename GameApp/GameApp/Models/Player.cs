namespace GameApp.LobbySystem;

public class Player
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public PlayerRole Role { get; set; } = PlayerRole.None;
    public int iconId { get; set; } = 0;

    public string? ConnectionId { get; set; } // SignalR connection id

    public Player(string displayName, int iconId)
    {
        this.DisplayName = displayName;
        this.iconId = iconId;
    }
}
