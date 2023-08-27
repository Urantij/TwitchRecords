using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchRecords.Files;

public class RecordInfo
{
    public readonly List<StreamFileInfo> files = new();

    public string? text;

    public RecordInfo()
    {
    }

    public RecordInfo(string? text)
    {
        this.text = text;
    }
}
