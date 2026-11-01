using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

public class WinGetSetupViewModelTests
{
    [Fact] // R3.2
    public void Explains_what_is_missing()
    {
        var vm = new WinGetSetupViewModel(false, () => false, () => true);

        Assert.Contains("needs WinGet", vm.Title);
        Assert.Contains("App Installer", vm.Message);
        Assert.Null(vm.Status);
    }

    [Fact] // R3.5
    public void Says_so_when_winget_is_installed_but_did_not_start()
    {
        var vm = new WinGetSetupViewModel(true, () => true, () => false);

        Assert.Contains("couldn't start", vm.Title);
    }

    [Fact]
    public void Check_again_stays_put_while_winget_is_missing()
    {
        var opened = 0;
        var vm = new WinGetSetupViewModel(false, () => false, () => { opened++; return true; });

        vm.CheckAgainCommand.Execute(null);

        Assert.Equal(0, opened); // R3.3: no activation while it's missing
        Assert.Contains("still wasn't found", vm.Status);
    }

    [Fact] // R3.4
    public void Check_again_opens_the_app_once_winget_is_there()
    {
        var opened = 0;
        var vm = new WinGetSetupViewModel(false, () => true, () => { opened++; return true; });

        vm.CheckAgainCommand.Execute(null);

        Assert.Equal(1, opened);
        Assert.Null(vm.Status);
    }

    [Fact] // R3.4
    public void Check_again_asks_for_a_restart_when_winget_still_will_not_start()
    {
        var vm = new WinGetSetupViewModel(false, () => true, () => false);

        vm.CheckAgainCommand.Execute(null);

        Assert.Contains("Restart", vm.Status);
    }

    [Fact] // 006 R3.5, R3.7
    public async Task Set_up_runs_only_on_request_and_opens_the_app()
    {
        var installed = false;
        var calls = 0;
        var opened = false;
        var vm = new WinGetSetupViewModel(false, () => installed, () => opened = true, (_, _) =>
        {
            calls++;
            installed = true;
            return Task.FromResult<string?>(null);
        });
        Assert.True(vm.CanSetUp);
        Assert.Contains("set it up", vm.Message);
        Assert.Equal(0, calls);

        await vm.SetUpCommand.ExecuteAsync(null);

        Assert.Equal(1, calls);
        Assert.True(opened);
    }

    [Fact] // 006 R3.7
    public async Task Failed_set_up_gives_the_reason_and_keeps_the_link()
    {
        var vm = new WinGetSetupViewModel(false, () => false, () => true, (_, _) => Task.FromResult<string?>("No network."));

        await vm.SetUpCommand.ExecuteAsync(null);

        Assert.Contains("No network.", vm.Status);
        Assert.Contains("Get App Installer", vm.Status);
        Assert.False(vm.IsSettingUp);
    }

    [Fact] // 011 R3.5
    public void Set_up_is_not_offered_for_a_broken_install()
    {
        var vm = new WinGetSetupViewModel(true, () => true, () => false, (_, _) => Task.FromResult<string?>(null));

        Assert.False(vm.CanSetUp);
    }
}
