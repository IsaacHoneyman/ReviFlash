using Avalonia.Controls;
using Avalonia.Interactivity;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class ReviewView : UserControl
{
    public ReviewView()
    {
        InitializeComponent();
    }

    private void ShowAnswer_Click(object sender, RoutedEventArgs e) => GetReviewVM()?.Reveal();
    private void SubmitAnswer_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetReviewVM();
        if (vm is null)
        {
            return;
        }

        if (vm.IsMultiChoiceCard)
        {
            vm.CheckMultiChoiceAnswer();
            return;
        }

        if (vm.IsMatchCard)
        {
            vm.CheckMatchAnswer();
            return;
        }

        if (vm.IsTrueFalseCard)
        {
            return;
        }

        vm.CheckTypedAnswer();
    }

    private void TrueAnswer_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetReviewVM();
        if (vm?.IsTrueFalseCard == true)
        {
            vm.CheckTrueFalseAnswer(true);
        }
    }

    private void FalseAnswer_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetReviewVM();
        if (vm?.IsTrueFalseCard == true)
        {
            vm.CheckTrueFalseAnswer(false);
            return;
        }
    }
    private void NextCard_Click(object sender, RoutedEventArgs e) => GetReviewVM()?.NextCard();
    private void SkipCard_Click(object sender, RoutedEventArgs e) => GetReviewVM()?.SkipCard();
    private void RetryLater_Click(object sender, RoutedEventArgs e) => GetReviewVM()?.RetryLater();
    private void Correct_Click(object sender, RoutedEventArgs e) => GetReviewVM()?.MarkCorrect();
    private void Incorrect_Click(object sender, RoutedEventArgs e) => GetReviewVM()?.MarkIncorrect();

    private async void QuitSession_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConfirmDialogWindow("Quit this session and save your progress so far?");
        bool confirmed = await dialog.ShowDialog<bool>((Window)TopLevel.GetTopLevel(this)!);
        if (confirmed)
        {
            GetReviewVM()?.QuitSession();
        }
    }

    private ReviewViewModel? GetReviewVM() => DataContext as ReviewViewModel;
}
