using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TwitchLib.Api;

namespace TwitchRecords.Twitch.Api;

public class TwitchApiService
{
    public TwitchAPI Api { get; private set; }

    public TwitchApiService(IOptions<TwitchApiConfig> config)
    {
        Api = new TwitchAPI(loggerFactory: null);

        Api.Settings.ClientId = config.Value.ClientId;
        Api.Settings.Secret = config.Value.Secret;
    }
}
