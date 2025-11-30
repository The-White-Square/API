using System.Threading.Tasks;
using GameApp.Drawing;
using Microsoft.AspNetCore.SignalR;
using GameApp.Hubs;

namespace GameApp.Service;

public class SignalRDrawingRelay : IDrawingRelay
{
    private readonly IHubContext<LobbyHub> _hubContext;

    public SignalRDrawingRelay(IHubContext<LobbyHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task RelayStrokeStarted(StrokeStartedDto dto, string targetConnectionId) =>
        _hubContext.Clients.Client(targetConnectionId).SendAsync("StrokeStarted", dto.StrokeId, dto.Color, dto.Width, dto.Tool);

    public Task RelayStrokePoints(StrokePointsDto dto, string targetConnectionId) =>
        _hubContext.Clients.Client(targetConnectionId).SendAsync("StrokePoints", dto.StrokeId, dto.Points);

    public Task RelayStrokeEnded(StrokeEndedDto dto, string targetConnectionId) =>
        _hubContext.Clients.Client(targetConnectionId).SendAsync("StrokeEnded", dto.StrokeId);

    public Task RelayCanvasCleared(CanvasClearedDto dto, string targetConnectionId) =>
        _hubContext.Clients.Client(targetConnectionId).SendAsync("CanvasCleared");
}