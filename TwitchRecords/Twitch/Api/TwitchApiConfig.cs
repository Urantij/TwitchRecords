using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchRecords.Twitch.Api;

public class TwitchApiConfig
{
    [Required]
    public required string ClientId { get; set; }
    [Required]
    public required string Secret { get; set; }
}
