using System.ComponentModel.DataAnnotations;

namespace GameApp.LobbySystem.Requests;

public class LobbyJoinRequest
{
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; }
    
    public string LobbyId { get; set; }
    
    public byte iconId { get; set; }
}