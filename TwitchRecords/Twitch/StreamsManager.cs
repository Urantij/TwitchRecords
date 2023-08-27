using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchRecords.Files;
using TwitchRecords.Twitch.Checker;
using TwitchRecords.Twitch.Downloader;
using TwitchStreamDownloader.Queues;

namespace TwitchRecords.Twitch;

/// <summary>
/// Следит за появлением стрима чтобы создать хендлер
/// </summary>
class StreamsManager
{
    readonly ILogger _logger;
    readonly StreamFilesManager filesManager;
    readonly IServiceProvider serviceProvider;

    readonly AppConfig config;

    CancellationTokenSource? currentStreamOfflineCancelSource;
    IServiceScope? currentStreamScope = null;
    StreamHandler? CurrentStream => currentStreamScope?.ServiceProvider.GetService<StreamHandler>();

    private readonly object locker = new();

    public StreamsManager(TwitchStatuser twitchStatuser, StreamFilesManager filesManager, IServiceProvider serviceProvider, IOptions<AppConfig> appOptions, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());
        this.filesManager = filesManager;
        this.serviceProvider = serviceProvider;

        this.config = appOptions.Value;

        twitchStatuser.ChannelWentOnline += StatuserOnline;
        twitchStatuser.ChannelWentOffline += StatuserOffline;
    }

    public void EndStream()
    {
        lock (locker)
        {
            if (CurrentStream == null)
            {
                _logger.LogWarning("There is no stream.");
                return;
            }

            if (!CurrentStream.Suspended)
            {
                _logger.LogWarning("Stream isnt suspended.");
                return;
            }

            StartStreamFinishing();
        }
    }

    /// <summary>
    /// Текущий стрим заменяется на нул и у него вызывается финиш
    /// </summary>
    private void StartStreamFinishing()
    {
        IServiceScope finishingStreamScope;
        lock (locker)
        {
            //не может быть нул
            finishingStreamScope = currentStreamScope!;

            currentStreamScope = null;
            ClearCurrentCancellationSource();
        }

        try
        {
            var streamHandler = finishingStreamScope.ServiceProvider.GetRequiredService<StreamHandler>();
            streamHandler.streamDownloader.ItemDownloaded -= FileDownloaded;

            _logger.LogInformation("Завершаем стрим {guid}...", streamHandler.guid);

            streamHandler.Finish();

            _logger.LogInformation("Стрим {guid} завершён.", streamHandler.guid);

            finishingStreamScope.Dispose();
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Не удалось зафинишировать стрим.");
        }
    }

    private void StatuserOnline(object? sender, EventArgs e)
    {
        lock (locker)
        {
            if (CurrentStream != null)
            {
                // Может ли сурс быть не нул?
                // Чтобы снова сработал онлайн, нужно, чтобы сработал офлаин.
                // Который всегда ставит сурс.
                // Я ставлю краш программы на то, что тут всегда не нулл.
                ClearCurrentCancellationSource();

                if (CurrentStream.Suspended)
                {
                    CurrentStream.Resume();
                }
            }
            else
            {
                currentStreamScope = serviceProvider.CreateScope();

                var streamHandler = currentStreamScope.ServiceProvider.GetRequiredService<StreamHandler>();
                streamHandler.streamDownloader.ItemDownloaded += FileDownloaded;
                streamHandler.Start();
            }
        }
    }

    private async void StatuserOffline(object? sender, EventArgs e)
    {
        CancellationTokenSource thatSource;
        lock (locker)
        {
            if (CurrentStream == null)
                return;

            thatSource = currentStreamOfflineCancelSource = new CancellationTokenSource();
        }

        try
        {
            await Task.Delay(config.StreamContinuationCheckTime, thatSource.Token);
        }
        catch { return; }

        lock (locker)
        {
            if (thatSource.IsCancellationRequested)
                return;

            CurrentStream.Suspend();
        }

        try
        {
            await Task.Delay(config.StreamRestartCheckTime, thatSource.Token);
        }
        catch { return; }

        lock (locker)
        {
            if (thatSource.IsCancellationRequested)
                return;

            StartStreamFinishing();
        }
    }

    private void FileDownloaded(QueueItem item)
    {
        StreamHandler? handler = CurrentStream;

        if (handler == null)
        {
            _logger.LogCritical($"Этого не может быц {nameof(FileDownloaded)}");
            return;
        }

        int tick = handler.ticker++;

        string fileName = $"{handler.guid:N}.{tick}.ts";

        string filePath = Path.Combine(config.FilesFolder, fileName);

        using (FileStream fs = new(filePath, FileMode.CreateNew, FileAccess.Write))
        {
            item.bufferWriteStream.CopyTo(fs);
            item.bufferWriteStream.Dispose();
        }

        StreamFileInfo fileInfo = new(fileName, item.segment.duration, item.segment.programDate);

        filesManager.AddFile(fileInfo);
    }

    private void ClearCurrentCancellationSource()
    {
        //этого не должно быть, чтобы иде не ныла
        if (currentStreamOfflineCancelSource == null)
            return;

        try { currentStreamOfflineCancelSource.Cancel(); } catch { };
        currentStreamOfflineCancelSource.Dispose();
        currentStreamOfflineCancelSource = null;
    }
}
