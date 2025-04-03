namespace DataFlow.Dataverse.IntegrationTests;
using System;

public interface ITimeKeeper
{
    void StartNew();

    bool IsRunning();

    TimeSpan Elapsed { get; }

    long ElapsedMilliseconds { get; }
}
