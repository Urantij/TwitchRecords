using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchRecords.Twitch.Api;

namespace TwitchRecords.Twitch.Checker;

class HelixChecker : BaseChecker
{
    private readonly TimeSpan checkDelay;
    private readonly TwitchApiService twitchApi;
    private readonly string channelId;

    public HelixChecker(TwitchApiService twitchApi, IOptions<AppConfig> appConfig, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        this.checkDelay = TimeSpan.FromSeconds(15);
        this.channelId = appConfig.Value.TwitchChannelId;
        this.twitchApi = twitchApi;
    }

    public void Start()
    {
        Task.Run(StartCheckLoopAsync);
    }

    private async Task StartCheckLoopAsync()
    {
        while (true)
        {
            TwitchCheckInfo? checkInfo = await CheckChannelAsync(channelId);

            //если ошибка, стоит подождать чуть больше обычного
            if (checkInfo == null)
            {
                await Task.Delay(checkDelay.Multiply(1.5));
                continue;
            }

            try
            {
                OnChannelChecked(checkInfo);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "CheckLoop");
            }

            await Task.Delay(checkDelay);
        }
    }

    /// <returns>null, если ошибка внеплановая</returns>
    private async Task<TwitchCheckInfo?> CheckChannelAsync(string channelId)
    {
        TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream stream;

        try
        {
            var response = await twitchApi.Api.Helix.Streams.GetStreamsAsync(userIds: new List<string>() { channelId }, first: 1);

            if (response.Streams.Length == 0)
            {
                return new TwitchCheckInfo(false, DateTime.UtcNow);
            }

            stream = response.Streams[0];

            if (!stream.Type.Equals("live", StringComparison.OrdinalIgnoreCase))
                return new TwitchCheckInfo(false, DateTime.UtcNow);
        }
        catch (TwitchLib.Api.Core.Exceptions.BadScopeException)
        {
            _logger.LogWarning($"CheckChannel exception опять BadScopeException");

            return null;
        }
        catch (TwitchLib.Api.Core.Exceptions.InternalServerErrorException)
        {
            _logger.LogWarning($"CheckChannel exception опять InternalServerErrorException");

            return null;
        }
        catch (HttpRequestException e)
        {
            _logger.LogWarning("CheckChannel HttpRequestException: \"{Message}\"", e.Message);

            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CheckChannel");

            return null;
        }

        return new HelixCheck(true, DateTime.UtcNow, new TwitchChannelInfo(stream.Title, stream.GameName, stream.GameId, stream.ViewerCount));
    }
}
