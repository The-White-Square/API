namespace GameApp.Utils;

public class LobbyUtils
{
    public static string GenerateLobbyCode()
    {
        int lenght = 8;
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        Random random = new Random();
        string code = "";
        for (int i = 0; i < lenght; i++)
        {
            code += chars[random.Next(0, chars.Length)];
        }
        return code;
    }
}