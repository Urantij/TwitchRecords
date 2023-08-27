using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TwitchRecords.Conversion;
using TwitchRecords.Helper;

namespace TwitchRecords.Upload.Telegram;

public class TelegramUploader : IDisposable
{
    readonly ILogger<TelegramUploader> logger;
    readonly TelegramConfig config;

    readonly WTelegram.Client client;

    public TelegramUploader(IOptions<TelegramConfig> options, ILogger<TelegramUploader> logger)
    {
        this.logger = logger;
        this.config = options.Value;

        client = new WTelegram.Client(Config);

        WTelegram.Helpers.Log = (lvl, str) => logger.Log((LogLevel)lvl, str);
    }

    public async Task TestAsync()
    {
        await client.LoginUserIfNeeded();
    }

    public Task UploadAsync(string text, FileContentInfo videoFile, VideoInfo videoInfo, FileContentInfo? thumbFile)
    {
        // Я не уверен, в какой момент просит verification_code
        // Так что на всякий случай просто.
        return Task.Run(async () =>
        {
            logger.LogInformation("Загружаем видео...");

            await client.LoginUserIfNeeded();

            TL.Messages_Chats chats = await client.Messages_GetAllChats();

            TL.ChatBase chat = chats.chats.First(c => c.Key == config.ChatId).Value;

            TL.InputFileBase uploadedVideo = await client.UploadFileAsync(videoFile.content, videoFile.name);

            TL.InputFileBase? uploadedThumbnail;
            if (thumbFile != null)
            {
                uploadedThumbnail = await client.UploadFileAsync(thumbFile.content, thumbFile.name);
            }
            else
            {
                uploadedThumbnail = null;
            }

            await PostVideoAsync(chat, text, uploadedVideo, uploadedThumbnail, videoInfo.width, videoInfo.height, videoInfo.duration);

            logger.LogInformation("Загрузили видео.");
        });
    }

    // https://stackoverflow.com/a/71845019/21555531
    Task PostVideoAsync(TL.InputPeer peer, string text, TL.InputFileBase fileBase, TL.InputFileBase? thumb, int width, int height, double duration)
    {
        TL.InputMediaUploadedDocument mediaDocument = new()
        {
            file = fileBase,
            mime_type = "video/mp4",
            attributes = new[] {
                new TL.DocumentAttributeVideo()
                {
                    duration = duration,
                    w = width,
                    h = height,
                    flags = TL.DocumentAttributeVideo.Flags.supports_streaming
                }
            }
        };

        if (thumb != null)
        {
            mediaDocument.thumb = thumb;
            mediaDocument.flags = TL.InputMediaUploadedDocument.Flags.has_thumb;
        }

        return client.SendMessageAsync(peer, text, mediaDocument);
    }

    string? Config(string what)
    {
        switch (what)
        {
            case "api_id": return config.AppId;
            case "api_hash": return config.AppHash;
            case "phone_number": return config.Phone;
            case "verification_code":
                logger.LogCritical("verification_code: ");
                return Console.ReadLine();
            case "session_pathname": return config.SessionPath;
            default: return null;                  // let WTelegramClient decide the default config
        }
    }

    public void Dispose()
    {
        client.Dispose();
    }
}
