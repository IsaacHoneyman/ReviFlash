using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace ReviFlash.Views.Controls;

/// <summary> A busy arc that turns on the render thread, so it keeps spinning while the UI thread is busy laying out. </summary>
public class BusySpinner : Arc
{
    protected override Type StyleKeyOverride => typeof(Arc);

    public BusySpinner()
    {
        Classes.Add("spinner");
        SizeChanged += (_, _) => Start();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Start();
    }

    private void Start()
    {
        if (ElementComposition.GetElementVisual(this) is not { } visual || Bounds.Width <= 0) return;

        visual.CenterPoint = new Vector3D(Bounds.Width / 2, Bounds.Height / 2, 0);

        var turn = visual.Compositor.CreateScalarKeyFrameAnimation();
        turn.InsertKeyFrame(0f, 0f);
        turn.InsertKeyFrame(1f, (float)(2 * Math.PI), new Avalonia.Animation.Easings.LinearEasing());
        turn.Duration = TimeSpan.FromSeconds(0.9);
        turn.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation("RotationAngle", turn);
    }
}
