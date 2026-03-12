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

    [Parameter]
    public bool ShowHandle { get; set; }

    [Parameter]
    public float HandleSize { get; set; } = 14f;

    [Parameter]
    public string HandleColor { get; set; } = "#6750A4";

    [Parameter]
    public string? HandleBorderColor { get; set; }

    [Parameter]
    public float HandleBorderWidth { get; set; } = 2f;

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
            if (activeWidth > 0f)
            {
                var activeRect = SKRect.Create(trackRect.Left, trackRect.Top, activeWidth, trackRect.Height);
                paint.Color = activeColor;
                canvas.DrawRoundRect(activeRect, trackRect.Height / 2f, trackRect.Height / 2f, paint);
            }

            RenderHandle(canvas, trackRect, clampedRatio);
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

        RenderHandle(canvas, trackRect, clampedRatio);
    }

    private void RenderHandle(SKCanvas canvas, SKRect trackRect, float ratio)
    {
        if (!ShowHandle)
        {
            return;
        }

        var size = Math.Max(4f, HandleSize);
        var radius = size / 2f;
        var centerX = trackRect.Left + (trackRect.Width * Math.Clamp(ratio, 0f, 1f));
        var centerY = trackRect.MidY;

        if (!string.IsNullOrWhiteSpace(HandleBorderColor) && HandleBorderWidth > 0f)
        {
            using var borderPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = SKColor.Parse(HandleBorderColor),
            };

            canvas.DrawCircle(centerX, centerY, radius + Math.Max(1f, HandleBorderWidth), borderPaint);
        }

        using var handlePaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            Color = SKColor.Parse(HandleColor),
        };

        canvas.DrawCircle(centerX, centerY, radius, handlePaint);
    }
}
