using System.ComponentModel.DataAnnotations;
namespace GameApp.Application.Requests;

public class LobbyJoinRequest
{
    [Required(ErrorMessage = "Username is required")]
    public required string Username { get; set; }

    public string LobbyId { get; set; } = "";

    public required int IconId { get; set; }

}
