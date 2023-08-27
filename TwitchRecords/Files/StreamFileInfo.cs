using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchRecords.Files;

public class StreamFileInfo
{
    public string fileName;
    public float duration;
    public DateTimeOffset absoluteStartDate;

    public int requiredBy;

    public StreamFileInfo(string fileName, float duration, DateTimeOffset absoluteStartDate)
    {
        this.fileName = fileName;
        this.duration = duration;
        this.absoluteStartDate = absoluteStartDate;
    }
}
