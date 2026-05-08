using Moq;
using Telegram.Bot;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using TelegramRAT.Commands;
using TelegramRAT.Services;
using Xunit;

namespace TelegramRAT.Tests.Commands;

public class CommandDispatcherTests
{
    private readonly Mock<ITelegramBotClient> _botMock;
    private readonly Mock<IBotNotificationService> _notificationMock;
    private readonly Mock<IBotSession> _sessionMock;
    private readonly long _ownerId = 12345;

    public CommandDispatcherTests()
    {
        _botMock = new Mock<ITelegramBotClient>();
        _notificationMock = new Mock<IBotNotificationService>();
        _sessionMock = new Mock<IBotSession>();
        _sessionMock.Setup(s => s.OwnerId).Returns(_ownerId);
    }

    [Fact]
    public async Task DispatchAsync_WithUnauthorizedUser_SendsErrorMessage()
    {
        // Arrange
        var commands = new List<IBotCommand>();
        var dispatcher = new CommandDispatcher(commands, _botMock.Object, _notificationMock.Object, _sessionMock.Object);
        
        var update = new Update
        {
            Message = new Message
            {
                From = new User { Id = 999 }, // Different from owner
                Chat = new Chat { Id = 999 },
                Text = "/ping"
            }
        };

        // Act
        await dispatcher.DispatchAsync(update);

        // Assert
        _botMock.Verify(b => b.SendRequest(
            It.Is<Telegram.Bot.Requests.SendMessageRequest>(r => r.Text.Contains("You are not authorized")),
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithAuthorizedUserAndValidCommand_ExecutesCommand()
    {
        // Arrange
        var mockCommand = new Mock<IBotCommand>();
        mockCommand.Setup(c => c.Command).Returns("ping");
        mockCommand.Setup(c => c.Validate(It.IsAny<BotCommandModel>())).Returns(true);
        
        var commands = new List<IBotCommand> { mockCommand.Object };
        var dispatcher = new CommandDispatcher(commands, _botMock.Object, _notificationMock.Object, _sessionMock.Object);

        var update = new Update
        {
            Message = new Message
            {
                From = new User { Id = _ownerId },
                Chat = new Chat { Id = _ownerId },
                Text = "/ping"
            }
        };

        // Act
        await dispatcher.DispatchAsync(update);

        // Assert
        mockCommand.Verify(c => c.ExecuteAsync(It.IsAny<BotCommandModel>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WithInvalidCommand_DoesNothing()
    {
        // Arrange
        var commands = new List<IBotCommand>();
        var dispatcher = new CommandDispatcher(commands, _botMock.Object, _notificationMock.Object, _sessionMock.Object);

        var update = new Update
        {
            Message = new Message
            {
                From = new User { Id = _ownerId },
                Chat = new Chat { Id = _ownerId },
                Text = "/unknown"
            }
        };

        // Act
        await dispatcher.DispatchAsync(update);

        // Assert
        // No command executed, no error message sent (following current logic)
        _botMock.Verify(b => b.SendRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
