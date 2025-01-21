using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using System.Text;

namespace TelegramRAT.Commands;

public class BotCommandModel
{
    public FileBase[] Files { get; init; } = Array.Empty<FileBase>();
    public string Filename { get; init; }
    public Message Message { get; init; }
    public string RawArgs { get; init; }
    public string Command { get; set; }
    public string[] Args { get; init; } = Array.Empty<string>();

    public static BotCommandModel FromMessage(Message message, string customCommandMarker = null)
    {
        if (message?.Text == null && message?.Caption == null)
            return null;

        string text = message.Type == MessageType.Text ? message.Text : message.Caption;
        if (!text.StartsWith('/') && (customCommandMarker == null || !text.StartsWith(customCommandMarker)))
            return null;

        string marker = customCommandMarker ?? "/";
        if (customCommandMarker != null && text.StartsWith(customCommandMarker))
            marker = customCommandMarker;

        var split = text[marker.Length..].Split(' ', 2);
        var command = split[0].Split('@')[0].ToLower();
        var rawArgs = split.Length > 1 ? split[1].Trim() : string.Empty;
        var args = ParseArgs(rawArgs);

        var files = new List<FileBase>();
        string filename = null;

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
