using System.Threading.Tasks;
using GameApp.Application.Drawing;
using Microsoft.AspNetCore.SignalR;
using GameApp.Application.Hubs;

namespace GameApp.Application.Service;

public class SignalRDrawingRelay : IDrawingRelay
{
    private readonly IHubContext<LobbyHub> _hubContext;

    public SignalRDrawingRelay(IHubContext<LobbyHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task RelayStrokeStarted(StrokeStartedDto dto, string targetConnectionId) =>
        // TEMP: broadcast to group for testing
        _hubContext.Clients.Group(dto.LobbyId).SendAsync("StrokeStarted", dto.StrokeId, dto.Color, dto.Width, dto.Tool);

    public Task RelayStrokePoints(StrokePointsDto dto, string targetConnectionId) =>
        _hubContext.Clients.Group(dto.LobbyId).SendAsync("StrokePoints", dto.StrokeId, dto.Points);

    public Task RelayStrokeEnded(StrokeEndedDto dto, string targetConnectionId) =>
        _hubContext.Clients.Group(dto.LobbyId).SendAsync("StrokeEnded", dto.StrokeId);

    public Task RelayCanvasCleared(CanvasClearedDto dto, string targetConnectionId) =>
        _hubContext.Clients.Group(dto.LobbyId).SendAsync("CanvasCleared");
}