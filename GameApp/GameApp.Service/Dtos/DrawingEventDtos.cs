namespace GameApp.Service.Dtos;

public abstract record DrawingEventBase(string Type);

public sealed record StrokeStartedEvent(
    string StrokeId,
    string Color,
    double Width,
    string Tool
) : DrawingEventBase("StrokeStarted");

public sealed record StrokePointsEvent(
    string StrokeId,
    IReadOnlyList<PointDto> Points
) : DrawingEventBase("StrokePoints");

public sealed record StrokeEndedEvent(
    string StrokeId
) : DrawingEventBase("StrokeEnded");

public sealed record CanvasClearedEvent() : DrawingEventBase("CanvasCleared");