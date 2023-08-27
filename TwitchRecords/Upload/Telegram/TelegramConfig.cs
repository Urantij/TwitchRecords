using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchRecords.Upload.Telegram;

public class TelegramConfig
{
    [Required]
    public required string AppId { get; set; }
    [Required]
    public required string AppHash { get; set; }
    [Required]
    public required string Phone { get; set; }

    [Required]
    public required long ChatId { get; set; }

    public string SessionPath { get; set; } = "tg.session";
}
