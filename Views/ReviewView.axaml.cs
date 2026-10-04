using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReviFlash.ViewModels;

namespace ReviFlash.Views;

public partial class ReviewView : UserControl
{
    private ReviewViewModel? _watchedViewModel;
    private TopLevel? _topLevel;

    public ReviewView()
    {
        InitializeComponent();
    }

    // --- Keyboard ---

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // On the window, so keys work wherever focus is (or isn't) on the review screen.
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, Review_KeyDown, RoutingStrategies.Tunnel);
        FocusTypedAnswerIfNeeded();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel?.RemoveHandler(KeyDownEvent, Review_KeyDown);
        _topLevel = null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_watchedViewModel is not null) _watchedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _watchedViewModel = GetReviewVM();
        if (_watchedViewModel is not null) _watchedViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReviewViewModel.CurrentCard)) FocusTypedAnswerIfNeeded();
    }

    /// <summary> Puts the cursor in the answer box on Type to Answer questions, so you can just start typing. </summary>
    private void FocusTypedAnswerIfNeeded()
    {
        if (GetReviewVM() is { IsTypeCard: true, IsAnswerChecked: false })
            Dispatcher.UIThread.Post(() => TypedAnswerBox.Focus(), DispatcherPriority.Background);
    }

    /// <summary>
    /// Space reveals, Left/Right (or 1/2) mark incorrect/correct or pick True/False, 1-8 tick options,
    /// Enter submits and then moves on, S skips and R retries later. While typing an answer only Enter counts.
    /// </summary>
    private void Review_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsEffectivelyVisible || GetReviewVM() is not { } vm) return;
        if (e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift)) return;

        var typing = _topLevel?.FocusManager?.GetFocusedElement() is TextBox;
        if (typing && e.Key != Key.Enter) return;

        e.Handled = HandleKey(vm, e.Key);
    }

    private static bool HandleKey(ReviewViewModel vm, Key key)
    {
        var left = key is Key.Left or Key.D1 or Key.NumPad1;
        var right = key is Key.Right or Key.D2 or Key.NumPad2;
        var confirm = key is Key.Enter or Key.Space;

        if (vm.IsAnswerChecked)
        {
            if (confirm || key == Key.Right) vm.NextCard();
            else if (key == Key.R && vm.CanRetryLater) vm.RetryLater();
            else return false;
            return true;
        }

        if (key == Key.S)
        {
            vm.SkipCard();
            return true;
        }

        if (vm.IsFlipCard)
        {
            if (!vm.IsAnswerRevealed)
            {
                if (!confirm) return false;
                vm.Reveal();
            }
            else if (left) vm.MarkIncorrect();
            else if (right) vm.MarkCorrect();
            else if (key == Key.R && vm.CanRetryLater) vm.RetryLater();
            else return false;
            return true;
        }

        if (vm.IsTrueFalseCard)
        {
            if (!left && !right) return false;
            vm.CheckTrueFalseAnswer(left);
            return true;
        }

        if (vm.IsMultiChoiceCard && OptionNumber(key) is { } option)
        {
            vm.ToggleMultiChoiceOption(option - 1);
            return true;
        }

        if (key != Key.Enter && !(confirm && !vm.IsTypeCard)) return false;

        if (vm.IsMultiChoiceCard) vm.CheckMultiChoiceAnswer();
        else if (vm.IsMatchCard) vm.CheckMatchAnswer();
        else if (vm.IsTypeCard) vm.CheckTypedAnswer();
        else return false;
        return true;
    }

    /// <summary> 1-9 from the number row or keypad, or null for any other key. </summary>
    private static int? OptionNumber(Key key) => key switch
    {
        >= Key.D1 and <= Key.D9 => key - Key.D0,
        >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad0,
        _ => null,
    };

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
