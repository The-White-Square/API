using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using GameApp.Service.Dtos;
using GameApp.Service.Utils;
using GameApp.Application.Hubs;

namespace GameApp.Application.SignalR;

public sealed class SignalRDrawingRelay : IDrawingRelay
{
    private readonly IHubContext<LobbyHub> _hubContext;

    public SignalRDrawingRelay(IHubContext<LobbyHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task RelayStrokeStarted(StrokeStartedDto dto, string targetConnectionId) =>
        _hubContext.Clients.Client(targetConnectionId)
            .SendAsync("StrokeStarted", dto.StrokeId, dto.Color, dto.Width, dto.Tool, dto.LobbyId);

    public Task RelayStrokePoints(StrokePointsDto dto, string targetConnectionId) =>
        _hubContext.Clients.Client(targetConnectionId)
            .SendAsync("StrokePoints", dto.StrokeId, dto.Points, dto.LobbyId);

    public Task RelayStrokeEnded(StrokeEndedDto dto, string targetConnectionId) =>
        _hubContext.Clients.Client(targetConnectionId)
            .SendAsync("StrokeEnded", dto.StrokeId, dto.LobbyId);

    public Task RelayCanvasCleared(CanvasClearedDto dto, string targetConnectionId) =>
        _hubContext.Clients.Client(targetConnectionId)
            .SendAsync("CanvasCleared", dto.LobbyId);
}