using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TwitchRecords.Files;
using TwitchRecords.Twitch.Chat;
using TwitchSimpleLib.Chat;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchRecords.Control;

public class ControlCenter : IDisposable
{
    readonly ILogger logger;
    readonly RecorderService recorder;

    readonly IDisposable? monitorDisposable;

    readonly ChatBot chatBot;

    ControlConfig config;

    public ControlCenter(RecorderService recorder, ChatBot chatBot, IOptionsMonitor<ControlConfig> optionsMonitor, ILoggerFactory loggerFactory)
    {
        this.logger = loggerFactory.CreateLogger(this.GetType());
        this.recorder = recorder;
        this.config = optionsMonitor.CurrentValue;

        monitorDisposable = optionsMonitor.OnChange(OptionsChanged);

        this.chatBot = chatBot;

        chatBot.channel.PrivateMessageReceived += PrivateMessageReceived;
    }

    private void OptionsChanged(ControlConfig config, string? arg2)
    {
        this.config = config;
    }

    private void PrivateMessageReceived(object? sender, TwitchPrivateMessage e)
    {
        if (!e.badges.ContainsKey("broadcaster") && !e.mod)
            return;

        if (!e.text.StartsWith(config.Prefix))
            return;

        string name = e.displayName ?? e.username;
        string command;
        string[] args;

        {
            var split = e.text.Split(' ');

            command = split[0][config.Prefix.Length..];
            args = split.Skip(1).ToArray();
        }

        logger.LogInformation("Используется команда {command} {who}", command, name);

        if (command.Equals("старт", StringComparison.OrdinalIgnoreCase))
        {
            string? text = args.Length > 0 ? string.Join(' ', args) : null;

            bool started = recorder.Start(text, who: name);

            if (started)
            {
                chatBot.TrySendTextAsync("Запустил!", e.id);
            }
            else
            {
                chatBot.TrySendTextAsync("Уже запущено.", e.id);
            }
        }
        else if (command.Equals("стоп", StringComparison.OrdinalIgnoreCase))
        {
            string? text = args.Length > 0 ? string.Join(' ', args) : null;

            bool stopped = recorder.Stop(text, who: name);

            if (stopped)
            {
                chatBot.TrySendTextAsync("Остановил!", e.id);
            }
            else
            {
                chatBot.TrySendTextAsync("Уже остановлено.", e.id);
            }
        }
        else if (command.Equals("снимок", StringComparison.OrdinalIgnoreCase))
        {
            string? text = args.Length > 0 ? string.Join(' ', args) : null;

            bool started = recorder.DoScreen(text, who: name);

            if (started)
            {
                chatBot.TrySendTextAsync("Сохраняю...", e.id);
            }
            else
            {
                chatBot.TrySendTextAsync("Уже сохранял, продлеваю...", e.id);
            }
        }
    }

    public void Dispose()
    {
        monitorDisposable?.Dispose();
    }
}
