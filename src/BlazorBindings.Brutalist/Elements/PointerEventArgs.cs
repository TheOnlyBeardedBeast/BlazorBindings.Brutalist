using SkiaSharp;

namespace BlazorBindings.Brutalist.Elements;

public sealed class PointerEventArgs
{
    public required Element Element { get; init; }
    public required float ElementLeft { get; init; }
    public required float ElementTop { get; init; }
    public required float ElementWidth { get; init; }
    public required float ElementHeight { get; init; }
    public required float PointerX { get; init; }
    public required float PointerY { get; init; }
    public required float PointerLocalX { get; init; }
    public required float PointerLocalY { get; init; }

    public SKPoint PointerPoint => new(PointerX, PointerY);
}
