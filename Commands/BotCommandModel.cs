using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using System.Text;

namespace TelegramRAT.Commands;

public class BotCommandModel
{
    public FileBase[] Files { get; init; } = Array.Empty<FileBase>();
    public string? Filename { get; init; }
    public Message? Message { get; init; }
    public string? RawArgs { get; init; }
    public string? Command { get; set; }
    public string[] Args { get; init; } = Array.Empty<string>();

    public static BotCommandModel? FromMessage(Message message, string? customCommandMarker = null)
    {
        if (message?.Text == null && message?.Caption == null)
            return null;

        string? text = message.Type == MessageType.Text ? message.Text : message.Caption;
        if (string.IsNullOrEmpty(text) || (!text.StartsWith('/') && (customCommandMarker == null || !text.StartsWith(customCommandMarker))))
            return null;

        string marker = customCommandMarker ?? "/";
        if (customCommandMarker != null && text.StartsWith(customCommandMarker))
            marker = customCommandMarker;

        var commandText = text.AsSpan(marker.Length);
        
        var firstSpaceIndex = commandText.IndexOf(' ');
        var commandPart = firstSpaceIndex >= 0 ? commandText[..firstSpaceIndex] : commandText;
        var rawArgsSpan = firstSpaceIndex >= 0 ? commandText[(firstSpaceIndex + 1)..].Trim() : default;

        var atIndex = commandPart.IndexOf('@');
        var command = (atIndex >= 0 ? commandPart[..atIndex] : commandPart).ToString().ToLower();
        var rawArgs = rawArgsSpan.ToString();
        var args = ParseArgs(rawArgs);

        var files = new List<FileBase>();
        string? filename = null;

        if (message.ReplyToMessage != null)
        {
            if (message.ReplyToMessage.Photo != null)
                files.AddRange(message.ReplyToMessage.Photo);
            if (message.ReplyToMessage.Document != null)
            {
                files.Add(message.ReplyToMessage.Document);
                filename = message.ReplyToMessage.Document.FileName;
            }
        }

        if (message.Document != null)
        {
            files.Clear();
            files.Add(message.Document);
            filename = message.Document.FileName;
        }

        if (message.Photo != null)
        {
            files.Clear();
            files.AddRange(message.Photo);
        }

        return new BotCommandModel
        {
            Command = command,
            Args = args,
            RawArgs = rawArgs,
            Message = message,
            Files = files.ToArray(),
            Filename = filename
        };
    }

    private static string[] ParseArgs(string rawArgs)
    {
        var args = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        foreach (var ch in rawArgs)
        {
            switch (ch)
            {
                case '"':
                    inQuotes = !inQuotes;
                    if (!inQuotes && sb.Length > 0)
                    {
                        args.Add(sb.ToString());
                        sb.Clear();
                    }
                    break;
                case ' ' when !inQuotes:
                    if (sb.Length > 0)
                    {
                        args.Add(sb.ToString());
                        sb.Clear();
                    }
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }

        if (sb.Length > 0)
            args.Add(sb.ToString());

        return args.ToArray();
    }
}
