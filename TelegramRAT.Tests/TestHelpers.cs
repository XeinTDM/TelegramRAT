using System.Text.Json;
using Telegram.Bot.Types;

namespace TelegramRAT.Tests;

public static class TestHelpers
{
    public static Message CreateMessage(long chatId, int messageId)
    {
        var json = $"{{\"message_id\": {messageId}, \"id\": {messageId}, \"chat\": {{\"id\": {chatId}}}}}";
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        return JsonSerializer.Deserialize<Message>(json, options)!;
    }
}