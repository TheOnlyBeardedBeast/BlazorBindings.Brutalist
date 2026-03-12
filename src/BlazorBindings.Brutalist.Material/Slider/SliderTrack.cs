using BlazorBindings.Brutalist.Elements;
using Microsoft.AspNetCore.Components;
using SkiaSharp;

namespace BlazorBindings.Brutalist.Material.Slider;

public sealed class SliderTrack : YogaClickableView
{
    [Parameter]
    public int SegmentCount { get; set; } = 2;

    [Parameter]
    public float SegmentGap { get; set; } = 4f;

    [Parameter]
    public bool Segmented { get; set; } = true;

    [Parameter]
    public float ActiveRatio { get; set; }

    [Parameter]
    public string ActiveColor { get; set; } = "#6750A4";

    [Parameter]
    public string InactiveColor { get; set; } = "#E8DEF8";

    [Parameter]
    public float TrackHeight { get; set; } = 6f;

    protected override void RenderPostMain(SKCanvas canvas, SKRect bounds)
    {
        var safeTrackHeight = Math.Clamp(TrackHeight, 2f, Math.Max(2f, bounds.Height));
        var trackTop = bounds.MidY - (safeTrackHeight / 2f);
        var trackRect = SKRect.Create(bounds.Left, trackTop, bounds.Width, safeTrackHeight);

        if (trackRect.Width <= 0 || trackRect.Height <= 0)
        {
            return;
        }

        var activeColor = SKColor.Parse(ActiveColor);
        var inactiveColor = SKColor.Parse(InactiveColor);
        var clampedRatio = Math.Clamp(ActiveRatio, 0f, 1f);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        if (!Segmented || SegmentCount <= 1)
        {
            paint.Color = inactiveColor;
            canvas.DrawRoundRect(trackRect, trackRect.Height / 2f, trackRect.Height / 2f, paint);

            var activeWidth = trackRect.Width * clampedRatio;
            if (activeWidth <= 0f)
            {
                return;
            }

            var activeRect = SKRect.Create(trackRect.Left, trackRect.Top, activeWidth, trackRect.Height);
            paint.Color = activeColor;
            canvas.DrawRoundRect(activeRect, trackRect.Height / 2f, trackRect.Height / 2f, paint);
            return;
        }

        var safeSegmentCount = Math.Max(2, SegmentCount);
        var safeGap = Math.Max(0f, SegmentGap);
        var totalGap = safeGap * (safeSegmentCount - 1);
        var availableWidth = trackRect.Width - totalGap;
        if (availableWidth <= 0f)
        {
            return;
        }

        var segmentWidth = availableWidth / safeSegmentCount;
        var maxActiveIndex = (int)Math.Round(clampedRatio * (safeSegmentCount - 1), MidpointRounding.AwayFromZero);

        for (var segmentIndex = 0; segmentIndex < safeSegmentCount; segmentIndex++)
        {
            var left = trackRect.Left + (segmentIndex * (segmentWidth + safeGap));
            var segmentRect = SKRect.Create(left, trackRect.Top, segmentWidth, trackRect.Height);

            paint.Color = segmentIndex <= maxActiveIndex ? activeColor : inactiveColor;
            canvas.DrawRoundRect(segmentRect, trackRect.Height / 2f, trackRect.Height / 2f, paint);
        }
    }
}
