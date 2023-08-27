using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchRecords.Conversion;

public class ConversionHandler : IDisposable
{
    readonly Process process;

    public int ExitCode => process.ExitCode;

    /// <summary>
    /// Его пишем
    /// </summary>
    public Stream InputStream => process.StandardInput.BaseStream;

    public ConversionHandler(Process process)
    {
        this.process = process;
    }

    public async Task<bool> WaitAsync()
    {
        await process.WaitForExitAsync();

        return process.ExitCode == 0;
    }

    public void Dispose()
    {
        process.Dispose();
    }
}
