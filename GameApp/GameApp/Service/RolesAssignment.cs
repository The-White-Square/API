using GameApp.Controllers;
using GameApp.LobbySystem;

namespace GameApp.Service;

public record RolesAssignment(
    string? DescriberConnectionId,
    string? DrawerConnectionId,
    Player Describer,
    Player Drawer,
    ImageDto? Image
);