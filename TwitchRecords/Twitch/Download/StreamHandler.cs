using System.Linq;
using System.Net.Http;
using ExtM3UPlaylistParser.Models;
using Microsoft.Extensions.Logging;
using TwitchLib.Api;
using TwitchRecords.Twitch.Chat;
using TwitchRecords.Twitch.Checker;
using TwitchSimpleLib.Chat.Messages;
using TwitchStreamDownloader.Download;
using TwitchStreamDownloader.Exceptions;
using TwitchStreamDownloader.Net;
using TwitchStreamDownloader.Queues;
using TwitchStreamDownloader.Resources;

namespace TwitchRecords.Twitch.Downloader;

/// <summary>
/// Хранит информацию о текущем стриме.
/// </summary>
class StreamHandler
{
    internal bool Finished { get; private set; } = false;
    internal bool Suspended { get; private set; } = false;

    readonly ILogger _logger;

    public readonly Guid guid;

    public readonly StreamDownloader streamDownloader;

    /// <summary>
    /// UTC
    /// </summary>
    internal readonly DateTime handlerCreationDate;

    public int ticker = 0;

    public StreamHandler(StreamDownloader streamDownloader, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());

        guid = Guid.NewGuid();

        handlerCreationDate = DateTime.UtcNow;

        this.streamDownloader = streamDownloader;
    }

    internal void Start()
    {
        _logger.LogInformation("Starting...");

        streamDownloader.Start();
    }

    /// <summary>
    /// Остановим загрузчики от ддоса твича
    /// </summary>
    internal void Suspend()
    {
        _logger.LogInformation("Suspending...");

        Suspended = true;

        streamDownloader.Suspend();
    }

    /// <summary>
    /// Загрузчики должны были умереть, так что запустим их
    /// </summary>
    internal void Resume()
    {
        _logger.LogInformation("Resuming...");

        Suspended = false;

        streamDownloader.Resume();
    }

    /// <summary>
    /// Конец.
    /// </summary>
    internal void Finish()
    {
        _logger.LogInformation("Finishing...");

        Finished = true;

        streamDownloader.Close();
    }
}
