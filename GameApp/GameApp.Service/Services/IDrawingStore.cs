using System.Collections.Generic;
using GameApp.Service.Dtos;

namespace GameApp.Service.Services;

public interface IDrawingStore
{
    void AppendStrokeStarted(string lobbyId, string strokeId, string color, double width, string tool);
    void AppendStrokePoints(string lobbyId, string strokeId, IReadOnlyList<PointDto> points);
    void AppendStrokeEnded(string lobbyId, string strokeId);
    void AppendCanvasCleared(string lobbyId);

    IReadOnlyList<DrawingEventBase> GetActiveEvents(string lobbyId);

    // Undo/Redo act on action boundaries: StrokeEnded and CanvasCleared
    bool UndoLast(string lobbyId);
    bool RedoLast(string lobbyId);

    void ResetLobby(string lobbyId);
}