using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramRAT.Commands.System;
using TelegramRAT.Commands;
using TelegramRAT.Services;
using Xunit;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;

namespace TelegramRAT.Tests.Commands;

public class InfoCommandTests
{
    [Fact]
    public async Task ExecuteAsync_SendsSystemInfo()
    {
        // Arrange
        var botMock = new Mock<ITelegramBotClient>();
        botMock.Setup(b => b.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Message());
        var notificationMock = new Mock<IBotNotificationService>();
        var winApiMock = new Mock<IWinApiService>();

        winApiMock.Setup(w => w.GetWindowsVersion()).Returns("Windows 10 Test Edition");

        var command = new InfoCommand(botMock.Object, notificationMock.Object, winApiMock.Object);
        var model = new BotCommandModel
        {
            Command = "info",
            Message = TestHelpers.CreateMessage(123, 456)
        };

        // Act
        await command.ExecuteAsync(model);

        // Assert
        botMock.Verify(b => b.SendRequest(
            It.Is<SendMessageRequest>(r => 
                r.Text.Contains("User name:") && 
                r.Text.Contains("PC name:") &&
                r.Text.Contains("Windows 10 Test Edition")),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
