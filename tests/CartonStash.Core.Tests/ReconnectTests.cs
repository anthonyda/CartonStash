using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>BR-25: WinGet's server went away: a new manager and one more try, then a message that says what to do.</summary>
public class ReconnectTests
{
    private static Exception Gone() => new System.Runtime.InteropServices.COMException("The RPC server is unavailable.", unchecked((int)0x800706BA));

    [Fact]
    public async Task The_call_is_tried_once_more_on_a_new_manager()
    {
        var calls = 0;
        var reconnects = 0;

        var result = await CatalogConnection.RetryOnceAsync(() => ++calls == 1 ? Task.FromException<int>(Gone()) : Task.FromResult(42), () => reconnects++);

        Assert.Equal(42, result);
        Assert.Equal(1, reconnects);
    }

    [Fact]
    public async Task Gone_again_says_to_restart()
    {
        var ex = await Assert.ThrowsAsync<WinGetException>(() => CatalogConnection.RetryOnceAsync(() => Task.FromException<int>(Gone()), () => { }));

        Assert.Equal("WinGet stopped responding. Restart CartonStash.", ex.Message);
    }

    [Fact]
    public async Task Other_errors_are_not_retried()
    {
        var calls = 0;

        await Assert.ThrowsAsync<WinGetException>(() => CatalogConnection.RetryOnceAsync(() =>
        {
            calls++;
            return Task.FromException<int>(new WinGetException("A source failed.", null));
        }, () => { }));

        Assert.Equal(1, calls);
    }
}
