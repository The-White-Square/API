using GameApp.Hubs;
using GameApp.LobbySystem;
using GameApp.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using GameApp.Service;
using Microsoft.AspNetCore.SignalR;

namespace GameApp.Controllers   
{

    [ApiController]
    [Route("lobby")]
    public class LobbyController : ControllerBase
    {
        private readonly LobbyService _lobbiesService;
        private readonly IHubContext<LobbyHub> _hubContext;

        public LobbyController(LobbyService lobbiesService, IHubContext<LobbyHub> hubContext)
        {
            _lobbiesService = lobbiesService;
            _hubContext =  hubContext;
        }
        
        [HttpPost("join")]
        public async Task<IActionResult> JoinLobby([FromBody] LobbyJoinRequest request)    
        { 
        // if lobbyId == empty -> create lobby
        // if lobbyId right -> add to lobby
        // else error
            var lobbyId = request.LobbyId;
            if (string.IsNullOrEmpty(lobbyId))
            {// create
                Lobby lobby = _lobbiesService.CreateLobby();
                _lobbiesService.AddPlayer(new Player(request.Username, request.IconId), lobby.LobbyCode);
                return Ok( new{ lobby.LobbyCode});
            }

            if (_lobbiesService.LobbyExists(lobbyId))
            {// join
                _lobbiesService.AddPlayer(new Player(request.Username, request.IconId), lobbyId);
                await _hubContext.Clients.Group(lobbyId).SendAsync("PlayerJoined", request.Username);
                return Ok(request.Username);
            }
            
            // error
            return BadRequest("Lobby not found");
            
        }
        // return lobby selected image, assigns if not yet assigned
        [HttpGet("{lobbyId}/image")]
        public ActionResult<ImageDto> GetLobbyImage(string lobbyId)
        {
            if (!_lobbiesService.LobbyExists(lobbyId))
                return NotFound("Lobby not found");

            var dto = _lobbiesService.GetOrAssignLobbyImage(lobbyId);
            if (dto is null)
                return NotFound("No images available.");

            return Ok(dto);
        }
    }
}