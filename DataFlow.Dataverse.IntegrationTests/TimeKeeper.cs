namespace DataFlow.Dataverse.IntegrationTests;
using System;
using System.Diagnostics;

internal sealed class TimeKeeper : ITimeKeeper
{
    public Stopwatch? Stopwatch;

    public void StartNew()
    {
        Stopwatch = Stopwatch.StartNew();
    }

    public bool IsRunning()
    {
        return Stopwatch != null && Stopwatch.IsRunning;
    }

    public TimeSpan Elapsed
    {
        get
        {
            if (Stopwatch == null)
            {
                throw new InvalidOperationException($"The timekeeper has not been started");
            }

            return Stopwatch.Elapsed;
        }
    }

    public long ElapsedMilliseconds
    {
        get
        {
            if (Stopwatch == null)
            {
                throw new InvalidOperationException($"The timekeeper has not been started");
            }

            return Stopwatch.ElapsedMilliseconds;
        }
    }
}
