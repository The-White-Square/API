using System.Threading.Tasks;
using GameApp.Drawing;

namespace GameApp.Service;

public interface IDrawingRelay
{
    Task RelayStrokeStarted(StrokeStartedDto dto, string targetConnectionId);
    Task RelayStrokePoints(StrokePointsDto dto, string targetConnectionId);
    Task RelayStrokeEnded(StrokeEndedDto dto, string targetConnectionId);
    Task RelayCanvasCleared(CanvasClearedDto dto, string targetConnectionId);
}