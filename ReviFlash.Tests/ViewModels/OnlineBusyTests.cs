using ReviFlash.Data.Online;
using ReviFlash.ViewModels;

namespace ReviFlash.Tests.ViewModels;

[Collection(DatabaseCollection.Name)]
public class OnlineBusyTests
{
    private sealed class TestOnlineViewModel : OnlineViewModelBase
    {
        public Task Run(Func<Task<string>> work) => RunWithStatusAsync("Working", work, "Failed");
    }

    [Fact]
    public async Task A_sign_in_check_shows_on_its_own_button_and_clears_when_done()
    {
        using var database = new TestDatabase();
        var vm = new DashboardViewModel();
        var restore = new TaskCompletionSource<SessionState>();

        var check = vm.CheckSignInAsync(DashboardViewModel.OnlineEntry.Export, () => restore.Task);

        Assert.True(vm.IsCheckingSignIn);
        Assert.True(vm.IsCheckingSignInForExport);
        Assert.False(vm.IsCheckingSignInForImport);

        restore.SetResult(SessionState.Active);

        Assert.Equal(SessionState.Active, await check);
        Assert.False(vm.IsCheckingSignIn);
        Assert.False(vm.IsCheckingSignInForExport);
    }

    [Fact]
    public async Task A_second_click_during_a_sign_in_check_is_ignored()
    {
        using var database = new TestDatabase();
        var vm = new DashboardViewModel();
        var restore = new TaskCompletionSource<SessionState>();
        int calls = 0;
        Task<SessionState> Restore() { calls++; return restore.Task; }

        var first = vm.CheckSignInAsync(DashboardViewModel.OnlineEntry.Import, Restore);
        var second = await vm.CheckSignInAsync(DashboardViewModel.OnlineEntry.Export, Restore);
        restore.SetResult(SessionState.Offline);

        Assert.Null(second);
        Assert.Equal(SessionState.Offline, await first);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task A_failed_sign_in_check_still_clears_the_spinner()
    {
        using var database = new TestDatabase();
        var vm = new DashboardViewModel();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            vm.CheckSignInAsync(DashboardViewModel.OnlineEntry.SignIn, () => throw new InvalidOperationException()));

        Assert.False(vm.IsCheckingSignIn);
    }

    [Fact]
    public async Task Online_work_shows_its_busy_text_with_a_spinner_then_its_result()
    {
        var vm = new TestOnlineViewModel();
        var work = new TaskCompletionSource<string>();

        var run = vm.Run(() => work.Task);

        Assert.True(vm.IsWorking);
        Assert.Equal("Working...", vm.StatusMessage);

        work.SetResult("Done.");
        await run;

        Assert.False(vm.IsWorking);
        Assert.Equal("Done.", vm.StatusMessage);
    }

    [Fact]
    public async Task Failed_online_work_stops_the_spinner_and_shows_the_error()
    {
        var vm = new TestOnlineViewModel();

        await vm.Run(() => throw new HttpRequestException("offline"));

        Assert.False(vm.IsWorking);
        Assert.Equal("Failed: offline", vm.StatusMessage);
    }

    [Fact]
    public async Task The_spinner_keeps_going_until_every_overlapping_call_finishes()
    {
        var vm = new TestOnlineViewModel();
        var search = new TaskCompletionSource<string>();
        var download = new TaskCompletionSource<string>();

        var first = vm.Run(() => search.Task);
        var second = vm.Run(() => download.Task);

        search.SetResult("Found 3 decks.");
        await first;
        Assert.True(vm.IsWorking);

        download.SetResult("Imported.");
        await second;
        Assert.False(vm.IsWorking);
    }
}
