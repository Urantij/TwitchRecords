using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchRecords.Helper;

public class FileContentInfo
{
    public readonly string name;
    public readonly Stream content;

    public FileContentInfo(string name, Stream content)
    {
        this.name = name;
        this.content = content;
    }
}
