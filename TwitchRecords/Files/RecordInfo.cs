using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchRecords.Files;

public class RecordInfo
{
    public readonly List<StreamFileInfo> files = new();

    /// <summary>
    /// Кто использовал команду, например.
    /// </summary>
    public List<string> people = new();

    public string? text;

    public RecordInfo()
    {
    }

    public RecordInfo(string? text)
    {
        this.text = text;
    }

    public void TryAddPeople(string who)
    {
        if (!people.Contains(who))
            people.Add(who);
    }
}
