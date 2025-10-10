using GameApp.LobbySystem;
using GameApp.LobbySystem.Requests;
using Microsoft.AspNetCore.Mvc;
using GameApp.Service;

namespace GameApp.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class LobbyController : ControllerBase
    {
        private readonly LobbyService _lobbiesService;

        public LobbyController(LobbyService lobbiesService)
        {
            _lobbiesService = lobbiesService;
        }
        
        [HttpGet]
        IActionResult JoinLobby([FromBody] LobbyJoinRequest request)    
        { 
        /// if lobbyId == empty -> create lobby
        /// if lobbyId wrong -> error
        /// else add player to lobby 
            string? lobbyId = request.LobbyId;
            if (string.IsNullOrEmpty(lobbyId))
            {// create
                Lobby lobby = _lobbiesService.CreateLobby();
                return Ok( new{ lobby.LobbyCode});
            }

            if (_lobbiesService.LobbyExists(lobbyId))
            {// join
                
            }
            
            // error
            return BadRequest("Lobby not found");
            
        }
    }
}