namespace GameApp.LobbySystem;
public readonly record struct LobbyCode(string Value) : IEquatable<LobbyCode>
{
    public override string ToString()
    {
        return Value;
    }
}
