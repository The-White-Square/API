namespace GameApp.Utils;

public class RandomLobbyCodeGenerator : ILobbyCodeGenerator
{
    private const int Length = 8;
    private const string Chars = "abcdefghijklmnopqrstuvwxyz0123456789";

    public string Generate()
    {
        var rng = Random.Shared;
        var chars = new char[Length];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = Chars[rng.Next(Chars.Length)];
        }
        return new string(chars);
    }
}