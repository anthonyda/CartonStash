using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>010 R3.18 (BR-06): a second trigger in the same process doesn't end the first check.</summary>
public class ActiveTasksTests
{
    [Fact]
    public void Only_the_last_task_to_end_ends_the_process_R3_18()
    {
        var tasks = new ActiveTasks();
        tasks.Begin(); // the late daily timer
        tasks.Begin(); // "user present", seconds later

        Assert.False(tasks.End()); // the second finds the check running and returns
        Assert.True(tasks.End());  // the first check ends
    }

    [Fact]
    public void One_task_ends_the_process_R3_18()
    {
        var tasks = new ActiveTasks();
        tasks.Begin();

        Assert.True(tasks.End());
    }
}
