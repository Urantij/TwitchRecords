using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace TwitchRecords.Conversion;

public class Ffmpeger
{
    static readonly Regex validLastLineRegex = new(@"^video:.+?\saudio:.+?\ssubtitle:.+?\sother\sstreams:.+?\sglobal\sheaders:.+?\smuxing\soverhead:\s", RegexOptions.Compiled);

    readonly ILogger _logger;

    readonly string? ffmpegFolderPath;

    public Ffmpeger(IOptions<ConversionConfig> options, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());

        ffmpegFolderPath = options.Value.FfmpegFolderPath;
    }

    public ConversionHandler CreateConversion(string resultFilePath)
    {
        Process process = new();

        process.StartInfo.FileName = MakePath("ffmpeg");

        process.StartInfo.Arguments = $"-f mpegts -i pipe:0 -c copy -f mp4 {resultFilePath}";

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden; //написано, что должно быть че то тру, а оно фолс. ну похуй, работает и ладно
        process.StartInfo.CreateNoWindow = true;
        process.Start();

        return new ConversionHandler(process);
    }

    public async Task<bool> CheckAsync()
    {
        if (ffmpegFolderPath != null && !File.Exists(MakePath("ffmpeg")))
        {
            _logger.LogCritical("Не удаётся найти ффмпег");
            return false;
        }

        using Process process = new();
        process.StartInfo.FileName = MakePath("ffmpeg");

        process.StartInfo.Arguments = "-version";

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        process.StartInfo.CreateNoWindow = true;
        process.Start();

        string? firstLine = await process.StandardOutput.ReadLineAsync();

        if (firstLine?.Contains("ffmpeg version") != true)
        {
            _logger.LogCritical("Это не ффмпег какой-то.");
            return false;
        }

        _logger.LogInformation("{output}", firstLine);

        await process.StandardOutput.ReadToEndAsync();

        await process.WaitForExitAsync();

        return true;
    }

    /// <summary>
    /// Костыль быстрый, я устал.
    /// </summary>
    public async Task<VideoInfo> TestVideoAsync(string filePath)
    {
        using Process process = new();
        process.StartInfo.FileName = MakePath("ffprobe");

        process.StartInfo.Arguments = $"-v quiet -select_streams v:0 -show_entries stream=width,height,duration -of csv=s=x:p=0 {filePath}";

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        process.StartInfo.CreateNoWindow = true;
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();

        string[] split = output.Split('x');

        await process.WaitForExitAsync();

        return new VideoInfo(int.Parse(split[0]), int.Parse(split[1]), double.Parse(split[2]));
    }

    public static bool CheckLastLine(string? line)
        => line != null && validLastLineRegex.IsMatch(line);

    string MakePath(string name)
    {
        if (ffmpegFolderPath != null)
            return Path.Combine(ffmpegFolderPath, name);

        return name;
    }
}
