namespace CartonStash.Core.Services;

/// <summary>
/// 010 R3.18 (BR-06): counts the background tasks one process serves, so the process ends only when the last one is
/// done. Thread-safe.
/// </summary>
public sealed class ActiveTasks
{
    private int _count;

    public void Begin() => Interlocked.Increment(ref _count);

    /// <summary>True when this was the last task running.</summary>
    public bool End() => Interlocked.Decrement(ref _count) <= 0;
}
