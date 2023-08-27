using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtM3UPlaylistParser.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchStreamDownloader.Download;
using TwitchStreamDownloader.Exceptions;
using TwitchStreamDownloader.Net;
using TwitchStreamDownloader.Queues;
using TwitchStreamDownloader.Resources;

namespace TwitchRecords.Twitch.Downloader;

/// <summary>
/// Отвечает за загрузку стрима куда надо.
/// Плюс пишет инфу о полученных сегментах в бд.
/// </summary>
public class StreamDownloader
{
    readonly ILogger _logger;

    readonly HttpClient httpClient;

    readonly SegmentsDownloader segmentsDownloader;
    readonly DownloadQueue downloadQueue;

    public bool Working { get; private set; }

    StreamSegment? lastSegment = null;

    /// <summary>
    /// Сколько секунд рекламы поели.
    /// Не точное время, так как не по миссинг сегментам, а ожидаемому времени.
    /// </summary>
    internal TimeSpan AdvertismentTime { get; private set; } = TimeSpan.Zero;

    public event Action<QueueItem>? ItemDownloaded;

    public StreamDownloader(IOptions<AppConfig> appOptions, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());

        httpClient = new HttpClient(new HttpClientHandler()
        {
            Proxy = null,
            UseProxy = false
        });

        SegmentsDownloaderSettings settings = new();

        segmentsDownloader = new SegmentsDownloader(httpClient, settings, appOptions.Value.TwitchChannelName, null, null);
        segmentsDownloader.UnknownPlaylistLineFound += UnknownPlaylistLineFound;
        segmentsDownloader.CommentPlaylistLineFound += CommentPlaylistLineFound;

        segmentsDownloader.MasterPlaylistExceptionOccured += MasterPlaylistExceptionOccured;
        segmentsDownloader.MediaPlaylistExceptionOccured += MediaPlaylistExceptionOccured;

        segmentsDownloader.TokenAcquired += TokenAcquired;
        segmentsDownloader.TokenAcquiringExceptionOccured += TokenAcquiringExceptionOccured;

        segmentsDownloader.MediaQualitySelected += MediaQualitySelected;
        segmentsDownloader.PlaylistEnded += PlaylistEnded;

        downloadQueue = new DownloadQueue(TimeSpan.FromSeconds(5));
        downloadQueue.ItemDequeued += ItemDequeued;

        segmentsDownloader.SegmentArrived += SegmentArrived;
    }

    internal void Start()
    {
        _logger.LogInformation("Starting...");

        Working = true;

        segmentsDownloader.Start();
    }

    internal void Suspend()
    {
        Working = false;

        segmentsDownloader.Stop();
    }

    internal void Resume()
    {
        Working = true;

        segmentsDownloader.Start();
    }

    internal void Close()
    {
        segmentsDownloader.Dispose();
        downloadQueue.Dispose();

        httpClient.Dispose();
    }

    private async void SegmentArrived(object? sender, StreamSegment segment)
    {
        if (!segment.IsLive)
        {
            AdvertismentTime += TimeSpan.FromSeconds(segment.duration);
            return;
        }

        QueueItem queueItem = downloadQueue.Queue(segment, new MemoryStream());

        try
        {
            await downloadQueue.DownloadAsync(httpClient, queueItem);
        }
        catch (Exception e)
        {
            SegmentDownloadExceptionOccured(sender, e);
        }
    }

    private async void ItemDequeued(object? sender, QueueItem qItem)
    {
        try
        {
            if (qItem.Written)
            {
                qItem.bufferWriteStream.Position = 0;

                if (lastSegment != null)
                {
                    var lastSegmentEnd = lastSegment.programDate.AddSeconds(lastSegment.duration);

                    var difference = qItem.segment.programDate - lastSegmentEnd;

                    if (difference >= TimeSpan.FromSeconds(0.2))
                    {
                        _logger.LogWarning("Skip Detected! Skipped {TotalSeconds:N0} seconds ({lastSegmentId} -> {segmentId}) :(", difference.TotalSeconds, lastSegment.mediaSequenceNumber, qItem.segment.mediaSequenceNumber);
                    }
                }
                lastSegment = qItem.segment;

                try
                {
                    ItemDownloaded?.Invoke(qItem);
                }
                catch (Exception exception)
                {
                    LogException("Unable to putdata", exception);
                }
            }
            else
            {
                // пропущен сегмент

                _logger.LogWarning("Missing downloading segment {title}", qItem.segment.title);
            }
        }
        finally
        {
            await qItem.bufferWriteStream.DisposeAsync();
        }
    }

    private void TokenAcquired(object? sender, AccessToken e)
    {
        //да не может он быть нулл.
        var downloader = (SegmentsDownloader)sender!;

        if (e.parsedValue.expires == null)
        {
            _logger.LogWarning("Got no playback token!");
            return;
        }

        var left = DateTimeOffset.FromUnixTimeSeconds(e.parsedValue.expires.Value) - DateTimeOffset.UtcNow;

        _logger.LogInformation("Got playback token! left {TotalMinutes:N1} minutes", left.TotalMinutes);
    }

    private void MediaQualitySelected(object? sender, MediaQualitySelectedEventArgs args)
    {
        //да не может он быть нулл.
        var downloader = (SegmentsDownloader)sender!;

        if (downloader.LastStreamQuality?.Same(args.Quality) == true)
            return;

        if (downloader.LastStreamQuality == null)
        {
            _logger.LogInformation("Quality selected: {format}", args.Quality);
        }
        else
        {
            _logger.LogWarning("New quality selected: {format} ({oldFormat})", args.Quality, downloader.LastStreamQuality);
        }
    }

    #region Logs
    private void UnknownPlaylistLineFound(object? sender, LineEventArgs e)
    {
        _logger.LogWarning("Unknown line ({master}): \"{line}\"", e.Master, e.Line);
    }

    private void CommentPlaylistLineFound(object? sender, LineEventArgs e)
    {
        _logger.LogWarning("Comment line ({master}): \"{line}\"", e.Master, e.Line);
    }

    private void MasterPlaylistExceptionOccured(object? sender, Exception e)
    {
        LogException($"Master Exception", e);
    }

    private void MediaPlaylistExceptionOccured(object? sender, Exception e)
    {
        LogException($"Media Exception", e);
    }

    private void SegmentDownloadExceptionOccured(object? sender, Exception e)
    {
        LogException($"Segment Exception", e);
    }

    private void TokenAcquiringExceptionOccured(object? sender, TokenAcquiringExceptionEventArgs args)
    {
        //да не может он быть нулл.
        var downloader = (SegmentsDownloader)sender!;

        LogException($"TokenAcq Failed ({args.Attempts})", args.Exception);
    }

    private void PlaylistEnded(object? sender, EventArgs e)
    {
        _logger.LogInformation("Playlist End");
    }

    private void LogException(string message, Exception e)
    {
        if (e is BadCodeException be)
        {
            _logger.LogError("{message} Bad Code ({statusCode})", message, be.statusCode);
        }
        else if (e is HttpRequestException re)
        {
            if (re.InnerException is IOException io)
            {
                if (io.Message == "Unable to read data from the transport connection: Connection reset by peer.")
                {
                    _logger.LogError("{message} Connection reset by peer.", message);
                }
                else
                {
                    _logger.LogError("{message} HttpRequestException.IOException: \"{ioMessage}\"", message, io.Message);
                }
            }
            else
            {
                _logger.LogError(re, "{message}", message);
            }
        }
        else
        {
            _logger.LogError(e, "{message}", message);
        }
    }
    #endregion
}
