namespace GameApp.Service.Dtos;

public record StrokeStartedDto(string LobbyId, string StrokeId, string Color, double Width, string Tool);
public record StrokePointsDto(string LobbyId, string StrokeId, IReadOnlyList<PointDto> Points);
public record StrokeEndedDto(string LobbyId, string StrokeId);
public record CanvasClearedDto(string LobbyId);
public record PointDto(double X, double Y);