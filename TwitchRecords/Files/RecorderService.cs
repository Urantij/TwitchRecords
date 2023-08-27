using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TwitchRecords.Conversion;
using TwitchRecords.Helper;
using TwitchRecords.Upload.Telegram;

namespace TwitchRecords.Files;

public class RecorderService
{
    readonly object locker = new();

    readonly AppConfig config;

    private readonly ILogger<RecorderService> logger;
    private readonly StreamFilesManager filesManager;
    private readonly TelegramUploader uploader;
    private readonly Ffmpeger ffmpeger;

    RecordInfo? currentRecordInfo;

    RecordInfo? currentScreenInfo;
    DateTime screenEndDate = DateTime.UtcNow;

    public RecorderService(StreamFilesManager filesManager, TelegramUploader uploader, Ffmpeger ffmpeger, IOptions<AppConfig> options, ILogger<RecorderService> logger)
    {
        this.logger = logger;
        this.config = options.Value;
        this.filesManager = filesManager;
        this.uploader = uploader;
        this.ffmpeger = ffmpeger;
    }

    public bool Start(string? text, string? who = null)
    {
        lock (locker)
        {
            if (currentRecordInfo != null)
            {
                currentRecordInfo.text ??= text;
                if (who != null)
                    currentRecordInfo.TryAddPeople(who);
                return false;
            }

            currentRecordInfo = new RecordInfo(text);
            if (who != null)
                currentRecordInfo.TryAddPeople(who);

            filesManager.FileAppeared += FileAppearedRecord;
        }

        return true;
    }

    public bool Stop(string? who = null)
    {
        lock (locker)
        {
            if (currentRecordInfo == null)
                return false;

            var record = currentRecordInfo;
            if (who != null)
                currentRecordInfo.TryAddPeople(who);

            currentRecordInfo = null;
            filesManager.FileAppeared -= FileAppearedRecord;

            Task.Run(async () =>
            {
                try
                {
                    await ProcessVideoAsync(record);
                }
                catch (Exception e)
                {
                    logger.LogCritical(e, "Процесс рекорда провалился.");
                }
            });
        }

        return true;
    }

    public bool DoScreen(string? text, string? who = null)
    {
        lock (locker)
        {
            screenEndDate = DateTime.UtcNow + config.PostScreenDuration;

            if (currentScreenInfo != null)
            {
                currentScreenInfo.text ??= text;
                if (who != null)
                    currentScreenInfo.TryAddPeople(who);
                return false;
            }

            currentScreenInfo = new RecordInfo(text);
            if (who != null)
                currentScreenInfo.TryAddPeople(who);

            currentScreenInfo.files.AddRange(filesManager.RequireFiles());
            filesManager.FileAppeared += FileAppearedScreen;
        }

        return true;
    }

    private void FileAppearedScreen(StreamFileInfo info)
    {
        lock (locker)
        {
            if (currentScreenInfo == null)
                return;

            info.requiredBy++;
            currentScreenInfo.files.Add(info);

            if (info.absoluteStartDate < screenEndDate)
                return;

            var record = currentScreenInfo;
            currentScreenInfo = null;

            filesManager.FileAppeared -= FileAppearedScreen;

            Task.Run(async () =>
            {
                try
                {
                    await ProcessVideoAsync(record);
                }
                catch (Exception e)
                {
                    logger.LogCritical(e, "Процесс скрина провалился.");
                }
            });
        }
    }

    private void FileAppearedRecord(StreamFileInfo info)
    {
        lock (locker)
        {
            if (currentRecordInfo == null)
                return;

            info.requiredBy++;
            currentRecordInfo.files.Add(info);
        }
    }

    async Task ProcessVideoAsync(RecordInfo record)
    {
        var newGuid = Guid.NewGuid();

        string resultFileName = $"{newGuid:N}.mp4";
        string resultFilePath = Path.Combine(config.FilesFolder, resultFileName);

        using var conversion = ffmpeger.CreateConversionTsToMp4(resultFilePath);

        foreach (StreamFileInfo info in record.files)
        {
            string filePath = Path.Combine(config.FilesFolder, info.fileName);

            using (FileStream fs = new(filePath, FileMode.Open, FileAccess.Read))
            {
                await fs.CopyToAsync(conversion.InputStream);
            }

            info.requiredBy--;
        }

        await conversion.InputStream.DisposeAsync();
        await conversion.WaitAsync();

        string thumbnailPath = await ffmpeger.MakeThumbnailAsync(resultFilePath);

        string text = record.text ?? "Без текста";
        if (record.people.Count > 0)
        {
            text += "\n" + string.Join(", ", record.people);
        }

        VideoInfo videoInfo = await ffmpeger.TestVideoAsync(resultFilePath);

        {
            using var videoFs = new FileStream(resultFilePath, FileMode.Open, FileAccess.Read);
            using var thumbFs = new FileStream(thumbnailPath, FileMode.Open, FileAccess.Read);

            FileContentInfo videoFileInfo = new(resultFileName, videoFs);
            FileContentInfo thumbnailFileInfo = new(thumbnailPath, thumbFs);

            await uploader.UploadAsync(text, videoFileInfo, videoInfo, thumbnailFileInfo);
        }

        File.Delete(resultFilePath);
    }
}
