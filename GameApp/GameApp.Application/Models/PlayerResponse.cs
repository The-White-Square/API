namespace GameApp.Application.Models;

public sealed class PlayerResponse
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int IconId { get; init; }
}