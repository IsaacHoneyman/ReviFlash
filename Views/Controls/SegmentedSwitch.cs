using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace ReviFlash.Views.Controls;

/// <summary> A two-way switch with a thumb that slides between its two labels, e.g. Sets | Notes. </summary>
public class SegmentedSwitch : UserControl
{
    public static readonly StyledProperty<string> FirstLabelProperty = AvaloniaProperty.Register<SegmentedSwitch, string>(nameof(FirstLabel), "");
    public static readonly StyledProperty<string> SecondLabelProperty = AvaloniaProperty.Register<SegmentedSwitch, string>(nameof(SecondLabel), "");
    public static readonly StyledProperty<bool> IsSecondProperty =
        AvaloniaProperty.Register<SegmentedSwitch, bool>(nameof(IsSecond), defaultBindingMode: BindingMode.TwoWay);

    public string FirstLabel { get => GetValue(FirstLabelProperty); set => SetValue(FirstLabelProperty, value); }
    public string SecondLabel { get => GetValue(SecondLabelProperty); set => SetValue(SecondLabelProperty, value); }
    public bool IsSecond { get => GetValue(IsSecondProperty); set => SetValue(IsSecondProperty, value); }

    private readonly Border _thumb;
    private readonly TextBlock _first = Label(0), _second = Label(1);

    public SegmentedSwitch()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        CornerRadius = new CornerRadius(10);

        _thumb = new Border
        {
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Transitions = [new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(160) }],
        };
        this.GetResourceObservable("GradientStart").Subscribe(new BrushObserver(brush => _thumb.Background = brush));
        Grid.SetColumnSpan(_thumb, 2);

        var lanes = new Grid { ColumnDefinitions = new ColumnDefinitions("*, *") };
        lanes.Children.Add(_thumb);
        lanes.Children.Add(_first);
        lanes.Children.Add(_second);
        lanes.SizeChanged += (_, _) => Place(animate: false);

        var track = new Border { CornerRadius = CornerRadius, Padding = new Thickness(3), Child = lanes };
        this.GetResourceObservable("BadgeBackground").Subscribe(new BrushObserver(brush => track.Background = brush));
        Content = track;

        _first[!TextBlock.TextProperty] = this[!FirstLabelProperty];
        _second[!TextBlock.TextProperty] = this[!SecondLabelProperty];
        Place(animate: false);
    }

    private static TextBlock Label(int column)
    {
        var label = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(16, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, column);
        return label;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSecondProperty) Place(animate: true);
    }

    private void Place(bool animate)
    {
        double half = (_thumb.Parent as Grid)?.Bounds.Width / 2 ?? 0;
        _thumb.Width = Math.Max(half, 0);

        var transitions = _thumb.Transitions;
        if (!animate) _thumb.Transitions = null;
        _thumb.RenderTransform = TransformOperations.Parse($"translateX({(IsSecond ? half : 0)}px)");
        if (!animate) _thumb.Transitions = transitions;

        SetLabelColour(_first, !IsSecond);
        SetLabelColour(_second, IsSecond);
    }

    private void SetLabelColour(TextBlock label, bool selected)
    {
        if (_labelSubscriptions.TryGetValue(label, out var old)) old?.Dispose();
        _labelSubscriptions[label] = this.GetResourceObservable(selected ? "PrimaryButtonForeground" : "TextPrimary")
            .Subscribe(new BrushObserver(brush => label.Foreground = brush));
    }

    private readonly System.Collections.Generic.Dictionary<TextBlock, IDisposable?> _labelSubscriptions = [];

    /// <summary> Follows a theme resource that may be a brush or a plain colour. </summary>
    private sealed class BrushObserver(Action<IBrush?> apply) : IObserver<object?>
    {
        public void OnNext(object? value) => apply(value switch
        {
            IBrush brush => brush,
            Color colour => new SolidColorBrush(colour),
            _ => null,
        });

        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        IsSecond = e.GetPosition(this).X > Bounds.Width / 2;
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        bool? next = e.Key switch
        {
            Key.Left => false,
            Key.Right => true,
            Key.Space or Key.Enter => !IsSecond,
            _ => null,
        };
        if (next is not { } value) return;

        IsSecond = value;
        e.Handled = true;
    }
}
