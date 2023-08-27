using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchRecords;

public class AppConfig
{
    [Required]
    public required string TwitchChannelName { get; set; }
    [Required]
    public required string TwitchChannelId { get; set; }

    public string FilesFolder { get; set; } = "Cache";

    public TimeSpan DurationToKeep { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan PostScreenDuration { get; set; } = TimeSpan.FromSeconds(15);
    /// <summary>
    /// Как долго считать офнутый стрим не офнутым
    /// </summary>
    public TimeSpan StreamContinuationCheckTime { get; set; } = TimeSpan.FromSeconds(22);
    /// <summary>
    /// Как долго ждать переподруба
    /// </summary>
    public TimeSpan StreamRestartCheckTime { get; set; } = TimeSpan.FromHours(1);
}
