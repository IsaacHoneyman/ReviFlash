using ReviFlash.ViewModels;

namespace ReviFlash.Tests.ViewModels;

public class LoginViewModelTests
{
    [Fact]
    public void Resending_the_code_is_blocked_while_a_request_is_running()
    {
        var vm = new LoginViewModel();
        Assert.True(vm.ResendCodeCommand.CanExecute(null));

        vm.IsBusy = true;

        Assert.False(vm.ResendCodeCommand.CanExecute(null));
        Assert.False(vm.SubmitCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("ab", false)]
    [InlineData("abc", true)]
    [InlineData("  abc  ", true)]
    [InlineData("abcdefghijklmnopqrstuvwxy", false)]
    public void Usernames_must_be_the_right_length(string username, bool ok)
    {
        Assert.Equal(ok, LoginViewModel.UsernameProblem(username) is null);
    }
}
