using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace TwitchRecords.Files;

public class StreamFilesManager
{
    readonly object locker = new();

    readonly TimeSpan durationToKeep;
    readonly string filesFolder;

    readonly List<StreamFileInfo> files = new();

    /// <summary>
    /// Ивент в локе, осторожно.
    /// </summary>
    public event Action<StreamFileInfo>? FileAppeared;

    public StreamFilesManager(IOptions<AppConfig> config)
    {
        durationToKeep = config.Value.DurationToKeep;
        filesFolder = config.Value.FilesFolder;

        Directory.CreateDirectory(filesFolder);
    }

    public void AddFile(StreamFileInfo fileInfo)
    {
        lock (locker)
        {
            files.Add(fileInfo);

            FileAppeared?.Invoke(fileInfo);
        }

        ClearFiles();
    }

    public StreamFileInfo[] RequireFiles()
    {
        StreamFileInfo[] result;
        lock (locker)
        {
            result = files.Where(file => DateTimeOffset.UtcNow - file.absoluteStartDate <= durationToKeep).ToArray();

            foreach (var file in result)
            {
                file.requiredBy++;
            }
        }

        return result;
    }

    void ClearFiles()
    {
        List<StreamFileInfo> filesToRemove = new();

        lock (locker)
        {
            foreach (var file in files.ToArray())
            {
                var passed = DateTimeOffset.UtcNow - file.absoluteStartDate;

                if (passed <= durationToKeep)
                    break;

                if (file.requiredBy > 0)
                    continue;

                filesToRemove.Add(file);
                files.Remove(file);
            }
        }

        foreach (var file in filesToRemove)
        {
            string path = Path.Combine(filesFolder, file.fileName);

            File.Delete(path);
        }
    }
}
