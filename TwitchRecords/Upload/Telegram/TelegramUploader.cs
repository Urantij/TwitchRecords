using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

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

    public Task UploadAsync(string text, string fileName, Stream fileContent, int width, int height, double duration)
    {
        // Я не уверен, в какой момент просит verification_code
        // Так что на всякий случай просто.
        return Task.Run(async () =>
        {
            logger.LogInformation("Загружаем видео...");

            await client.LoginUserIfNeeded();

            TL.Messages_Chats chats = await client.Messages_GetAllChats();

            TL.ChatBase chat = chats.chats.First(c => c.Key == config.ChatId).Value;

            TL.InputFileBase uploadedFile = await client.UploadFileAsync(fileContent, fileName);

            await UploadVideoAsync(chat, text, uploadedFile, width, height, duration);

            logger.LogInformation("Загрузили видео.");
        });
    }

    // https://stackoverflow.com/a/71845019/21555531
    Task UploadVideoAsync(TL.InputPeer peer, string text, TL.InputFileBase fileBase, int width, int height, double duration)
    {
        return client.SendMessageAsync(peer, text, new TL.InputMediaUploadedDocument
        {
            file = fileBase,
            mime_type = "video/mp4",
            attributes = new[] {
                new TL.DocumentAttributeVideo { duration = duration, w = width, h = height,
                flags = TL.DocumentAttributeVideo.Flags.supports_streaming }
            }
        });
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
