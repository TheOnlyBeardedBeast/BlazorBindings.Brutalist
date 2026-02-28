using BlazorBindings.Brutalist.Elements;
using Microsoft.AspNetCore.Components;
using SkiaSharp;

namespace BlazorBindings.Brutalist.Material.Input;

public class TextFieldBase : YogaTextInput
{
    [Parameter]
    public string BottomBorderColor { get; set; } = "#9ca3af";

    [Parameter]
    public float BottomBorderWidth { get; set; } = 1f;

    [Parameter]
    public float FocusBottomBorderWidth { get; set; } = 2f;

    private float resolvedBottomBorderWidth => _isFocused ? FocusBottomBorderWidth : BottomBorderWidth;

    protected override void RenderPostMain(SKCanvas canvas, SKRect bounds, SKRect textBounds)
    {
        base.RenderPostMain(canvas, bounds, textBounds);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = resolvedBottomBorderWidth,
            IsAntialias = true,
            Color = string.IsNullOrWhiteSpace(BottomBorderColor)
                ? SKColor.Parse("#9ca3af")
                : SKColor.Parse(BottomBorderColor),
        };

        var y = bounds.Bottom - (paint.StrokeWidth / 2f);
        canvas.DrawLine(bounds.Left, y, bounds.Right, y, paint);
    }
}
