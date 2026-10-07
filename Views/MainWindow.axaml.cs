using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ReviFlash.Models;
using ReviFlash.ViewModels;
using ReviFlash.Data.Local;
using ReviFlash.Data.Online;

namespace ReviFlash.Views;

public partial class MainWindow : Window
{
    // The background swirl is stepped by a timer instead of a style animation. Every step repaints the
    // whole window, so a 60fps animation kept a CPU core busy even while minimised. It turns slowly enough
    // that a low frame rate looks the same, and it pauses whenever the window is out of focus or minimised.
    private const double SwirlFramesPerSecond = 20;
    private const double OuterSwirlSeconds = 14;
    private const double InnerSwirlSeconds = 11;

    /// <summary> Outer swirl angle at each quarter of its turn, so it speeds up and slows down as it rotates. </summary>
    private static readonly double[] OuterSwirlAngles = [0, 105, 190, 282, 360];

    private readonly DispatcherTimer _swirlTimer = new(DispatcherPriority.Render)
    {
        Interval = TimeSpan.FromSeconds(1 / SwirlFramesPerSecond)
    };
    private readonly Stopwatch _swirlClock = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;

        _swirlTimer.Tick += (_, _) => StepSwirl();
        MetaDataManager.Data.PropertyChanged += Metadata_PropertyChanged;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // IsActive rather than the Activated event, which is raised before IsActive is updated.
        if (change.Property == IsActiveProperty || change.Property == WindowStateProperty) UpdateSwirl();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        // A note saves a moment after typing stops; closing the app in that moment shouldn't lose the last words.
        if ((DataContext as DashboardViewModel)?.CurrentPage is NoteViewModel note) note.SaveBeforeExit();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _swirlTimer.Stop();
        MetaDataManager.Data.PropertyChanged -= Metadata_PropertyChanged;
    }

    private void Metadata_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppMetaData.ShowBackgroundSwirl)) UpdateSwirl();
    }

    /// <summary> Runs the swirl only while it is switched on and the window is in use, keeping its place when paused. </summary>
    private void UpdateSwirl()
    {
        bool run = MetaDataManager.Data.ShowBackgroundSwirl && IsActive && WindowState != WindowState.Minimized;
        if (run == _swirlTimer.IsEnabled) return;

        if (run)
        {
            _swirlClock.Start();
            _swirlTimer.Start();
        }
        else
        {
            _swirlTimer.Stop();
            _swirlClock.Stop();
        }
    }

    private void StepSwirl()
    {
        double seconds = _swirlClock.Elapsed.TotalSeconds;

        double quarter = seconds % OuterSwirlSeconds / OuterSwirlSeconds * 4;
        int index = (int)quarter;
        double outer = OuterSwirlAngles[index] + (OuterSwirlAngles[index + 1] - OuterSwirlAngles[index]) * (quarter - index);

        ((RotateTransform)OuterSwirl.RenderTransform!).Angle = outer;
        ((RotateTransform)InnerSwirl.RenderTransform!).Angle = 360 - seconds % InnerSwirlSeconds / InnerSwirlSeconds * 360;
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        if (!MetaDataManager.Data.CheckForUpdatesOnStartup) return;
        var updateClient = new UpdateClient();
        var updateInfo = await updateClient.CheckForUpdatesAsync();

        if (updateInfo != null)
        {
            var confirmDialog = new ConfirmDialogWindow(
                $"Version {updateInfo.TargetFullRelease.Version} is available. Download and restart now?"
            );

            bool confirmed = await confirmDialog.ShowDialog<bool>(this);

            if (confirmed)
            {
                await new UpdateProgressWindow(updateClient, updateInfo).ShowDialog(this);
            }
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            if (WindowState == WindowState.FullScreen)
            {
                WindowState = WindowState.Normal;
                SystemDecorations = SystemDecorations.Full;
            }
            else
            {
                WindowState = WindowState.FullScreen;
                SystemDecorations = SystemDecorations.None;
            }

            e.Handled = true;
        }
    }
}
