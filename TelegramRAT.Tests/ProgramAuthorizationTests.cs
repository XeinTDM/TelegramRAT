using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using TelegramRAT;
using TelegramRAT.Commands;
using Xunit;

namespace TelegramRAT.Tests;

public class ProgramAuthorizationTests
{
    private static Mock<ITelegramBotClient> CreateBotMock(List<string> sentMessages)
    {
        var botMock = new Mock<ITelegramBotClient>(MockBehavior.Strict);

        botMock
            .Setup(b => b.MakeRequest(It.IsAny<IRequest<Message>>(), It.IsAny<CancellationToken>()))
            .Returns<IRequest<Message>, CancellationToken>((request, _) =>
            {
                if (request is SendMessageRequest messageRequest)
                {
                    sentMessages.Add(messageRequest.Text);
                }

                return Task.FromResult(new Message
                {
                    MessageId = 1,
                    Chat = new Chat { Id = request switch
                    {
                        SendMessageRequest send => send.ChatId.Identifier ?? 0,
                        _ => 0
                    } }
                });
            });

        botMock
            .Setup(b => b.MakeRequest(It.IsAny<IRequest<bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return botMock;
    }

    [Fact]
    public async Task UpdateWorkerAsync_RejectsNonOwnerMessages()
    {
        var sentMessages = new List<string>();
        var botMock = CreateBotMock(sentMessages);

        Program.SetBotClient(botMock.Object);
        Program.SetOwnerId(123456);
        Program.Commands.Clear();

        bool commandExecuted = false;
        Program.Commands.Add(new BotCommand
        {
            Command = "test",
            Description = string.Empty,
            Example = string.Empty,
            Execute = _ =>
            {
                commandExecuted = true;
                return Task.CompletedTask;
            }
        });

        try
        {
            var unauthorizedUpdate = new Update
            {
                Id = 1,
                Message = new Message
                {
                    MessageId = 10,
                    Date = DateTime.UtcNow,
                    Chat = new Chat { Id = 987654 },
                    From = new User { Id = 987654, FirstName = "intruder" },
                    Text = "/test"
                }
            };

            await Program.ProcessUpdatesAsync(new[] { unauthorizedUpdate });

            Assert.False(commandExecuted);
            Assert.Contains("You are not authorized to control this bot.", sentMessages);

            sentMessages.Clear();
            commandExecuted = false;

            var ownerUpdate = new Update
            {
                Id = 2,
                Message = new Message
                {
                    MessageId = 11,
                    Date = DateTime.UtcNow,
                    Chat = new Chat { Id = 123456 },
                    From = new User { Id = 123456, FirstName = "owner" },
                    Text = "/test"
                }
            };

            await Program.ProcessUpdatesAsync(new[] { ownerUpdate });

            Assert.True(commandExecuted);
            Assert.DoesNotContain("You are not authorized to control this bot.", sentMessages);
        }
        finally
        {
            Program.Commands.Clear();
            Program.SetOwnerId(0);
        }
    }
}
