using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using GameApp.Application.Hubs;
using GameApp.Application.Requests;
using GameApp.Application.Models;
using GameApp.Service.Models;
using GameApp.Service.Services;
using GameApp.Service.Exceptions;

namespace GameApp.Application.Controllers
{
    [ApiController]
    [Route("lobby")]
    public class LobbyController : ControllerBase
    {
        private readonly ILobbyService _lobbiesService;
        private readonly IHubContext<LobbyHub> _hubContext;

        public LobbyController(ILobbyService lobbiesService, IHubContext<LobbyHub> hubContext)
        {
            _lobbiesService = lobbiesService;
            _hubContext = hubContext;
        }

        [HttpPost("join")]
        public async Task<IActionResult> JoinLobby([FromBody] LobbyJoinRequest request)
        {
            var lobbyId = request.LobbyId?.ToLower() ?? string.Empty;
            if (string.IsNullOrEmpty(lobbyId))
            {
                var lobby = _lobbiesService.CreateLobby();
                _lobbiesService.AddPlayer(new Player(request.Username, request.IconId), lobby.LobbyCode);
                return Ok(new { lobby.LobbyCode });
            }

            if (_lobbiesService.LobbyExists(lobbyId))
            {// join
                try
                {
                    _lobbiesService.AddPlayer(new Player(request.Username, request.IconId), lobbyId);
                    await _hubContext.Clients.Group(lobbyId).SendAsync("PlayerJoined", request.Username);
                    return Ok(request.Username);
                }
                catch (LobbyFullException)
                {
                    return Conflict("Lobby is full");
                }
            }
            
            // error
            return BadRequest("Lobby not found");
            
        }
        // return lobby selected image, assigns if not yet assigned
        [HttpGet("{lobbyId}/image")]
        public ActionResult<ImageResponse> GetLobbyImage(string lobbyId)
        {
            if (!_lobbiesService.LobbyExists(lobbyId))
                return NotFound("Lobby not found");

            var dto = _lobbiesService.GetOrAssignLobbyImage(lobbyId);
            if (dto is null)
                return NotFound("No images available.");

            var response = new ImageResponse { Url = dto.Url, Id = dto.Id };
            return Ok(response);
        }

        [HttpGet("{lobbyId}/players")]
        public ActionResult<IEnumerable<PlayerResponse>> GetLobbyPlayers(string lobbyId)
        {
            if (!_lobbiesService.LobbyExists(lobbyId))
                return NotFound("Lobby not found");

            var lobby = _lobbiesService.GetLobby(lobbyId);
            var players = lobby.Players
                .Select(p => new PlayerResponse { Id = p.Id.ToString(), DisplayName = p.DisplayName, IconId = p.iconId })
                .ToList();

            return Ok(players);
        }
    }
}