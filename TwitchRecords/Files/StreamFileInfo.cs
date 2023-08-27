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
    // Возможно, не стоит хранить в каждом сегменте его время на стриме
    // Ведь есть стрим хендлер, там есть дата старта стрима. И можно посчитать, когда надо
    // Но как-нибудь в другой раз.
    public TimeSpan onStreamTime;

    public int requiredBy;

    public StreamFileInfo(string fileName, float duration, DateTimeOffset absoluteStartDate, TimeSpan onStreamTime)
    {
        this.fileName = fileName;
        this.duration = duration;
        this.absoluteStartDate = absoluteStartDate;
        this.onStreamTime = onStreamTime;
    }
}
