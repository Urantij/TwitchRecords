using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchSimpleLib.Chat;

namespace TwitchRecords.Twitch.Chat;

public class ChatBot
{
    readonly ILogger _logger;

    public readonly TwitchChatClient client;
    public readonly ChatAutoChannel channel;

    public ChatBot(IOptions<AppConfig> appOptions, IOptions<ChatConfig> chatOptions, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());

        TwitchChatClientOpts opts;

        var appConfig = appOptions.Value;
        var chatConfig = chatOptions.Value;

        if (chatConfig.Username != null && chatConfig.Token != null)
        {
            opts = new(chatConfig.Username, chatConfig.Token);
        }
        else
        {
            opts = new();
        }

        client = new(true, opts, loggerFactory);
        channel = client.AddAutoJoinChannel(appConfig.TwitchChannelName);

        client.AuthFailed += AuthFailed;
        client.ChannelJoined += ChannelJoind;
        client.ConnectionClosed += ConnectionClosed;
    }

    public async Task StartAsync()
    {
        await client.ConnectAsync();
    }

    public Task TrySendTextAsync(string text, string peer)
    {
        if (client.opts.Username == null)
            return Task.CompletedTask;

        return channel.SendMessageAsync(text, peer);
    }

    private void AuthFailed(object? sender, EventArgs e)
    {
        _logger.LogCritical("Чат бот AuthFail");
    }

    private void ChannelJoind(object? sender, string e)
    {
        _logger.LogInformation("Присоединился к каналу.");
    }

    private void ConnectionClosed(Exception? e)
    {
        _logger.LogWarning("Соединение закрыто {message}", e?.Message ?? "без сообщения");
    }

    public void Dispose()
    {
        client.Close();
    }
}
