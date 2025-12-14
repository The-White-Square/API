using GameApp.Service.Models;
using GameApp.Service.Dtos;

namespace GameApp.Service.Models;

public record RolesAssignment(
    string? DescriberConnectionId,
    string? DrawerConnectionId,
    Player Describer,
    Player Drawer,
    ImageDto? Image
);