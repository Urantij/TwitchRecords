using Microsoft.Extensions.Options;
using TwitchRecords.Control;
using TwitchRecords.Conversion;
using TwitchRecords.Files;
using TwitchRecords.Helper;
using TwitchRecords.Twitch;
using TwitchRecords.Twitch.Api;
using TwitchRecords.Twitch.Chat;
using TwitchRecords.Twitch.Checker;
using TwitchRecords.Twitch.Downloader;
using TwitchRecords.Upload.Telegram;

namespace TwitchRecords;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        {
            var file = builder.Configuration.AddJsonFile("config.json")
                                .Build();

            builder.Services.AddValidatedOptions<AppConfig>(file);

            builder.Services.AddSingleton<StreamsManager>();
            builder.Services.AddScoped<StreamHandler>();
            builder.Services.AddScoped<StreamDownloader>();

            builder.Services.AddSingleton<TwitchStatuser>();
            builder.Services.AddSingleton<HelixChecker>();
            builder.Services.AddSingleton<PubsubChecker>();
            builder.Services.AddSingleton<TwitchApiService>();
            builder.Services.AddValidatedOptions<TwitchApiConfig>(file.GetSection("Twitch").GetSection("Api"));

            builder.Services.AddSingleton<StreamFilesManager>();
            builder.Services.AddSingleton<RecorderService>();

            builder.Services.AddSingleton<ControlCenter>();
            builder.Services.AddSingleton<ChatBot>();
            builder.Services.AddValidatedOptions<ChatConfig>(file.GetSection("Twitch").GetSection("Chat"));

            builder.Services.AddSingleton<Ffmpeger>();
            builder.Services.AddValidatedOptions<ConversionConfig>(file.GetSection("Conversion"));

            builder.Services.AddSingleton<TelegramUploader>();
            builder.Services.AddValidatedOptions<TelegramConfig>(file.GetSection("Telegram"));
        }

        var app = builder.Build();

        {
            var appOptions = app.Services.GetRequiredService<IOptions<AppConfig>>();
            Directory.CreateDirectory(appOptions.Value.FilesFolder);
            if (appOptions.Value.ClearCacheOnStart)
            {
                var files = Directory.GetFiles(appOptions.Value.FilesFolder);
                foreach (var filePath in files)
                {
                    File.Delete(filePath);
                }
            }

            var ffmpeg = app.Services.GetRequiredService<Ffmpeger>();
            bool ffmpegTest = await ffmpeg.CheckAsync();

            if (!ffmpegTest)
            {
                throw new Exception("Ффмпег не прошёл проверку.");
            }

            var uploader = app.Services.GetRequiredService<TelegramUploader>();
            await uploader.TestAsync();

            var statuser = app.Services.GetRequiredService<TwitchStatuser>();
            statuser.Init();

            var chat = app.Services.GetRequiredService<ChatBot>();
            await chat.StartAsync();

            app.Services.GetRequiredService<ControlCenter>();
            app.Services.GetRequiredService<StreamsManager>();
        }

        app.Run();
    }
}
