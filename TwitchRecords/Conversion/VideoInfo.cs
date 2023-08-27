using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchRecords.Conversion;

public class VideoInfo
{
    public int width;
    public int height;
    public double duration;

    public VideoInfo(int width, int height, double duration)
    {
        this.width = width;
        this.height = height;
        this.duration = duration;
    }
}
