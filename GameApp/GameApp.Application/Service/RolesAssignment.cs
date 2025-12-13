using GameApp.Application.Controllers;
using GameApp.Application.LobbySystem;

namespace GameApp.Application.Service;

public record RolesAssignment(
    string? DescriberConnectionId,
    string? DrawerConnectionId,
    Player Describer,
    Player Drawer,
    ImageDto? Image
);