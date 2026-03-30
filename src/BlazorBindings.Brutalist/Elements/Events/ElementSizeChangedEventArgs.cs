using SkiaSharp;

namespace BlazorBindings.Brutalist.Elements;

public sealed class ElementSizeChangedEventArgs
{
    public ElementSizeChangedEventArgs(Element element, SKRect oldRect, SKRect newRect)
    {
        Element = element;
        OldRect = oldRect;
        NewRect = newRect;
    }

    public Element Element { get; }

    public SKRect OldRect { get; }

    public SKRect NewRect { get; }

    public float OldWidth => OldRect.Width;
    public float OldHeight => OldRect.Height;
    public float NewWidth => NewRect.Width;
    public float NewHeight => NewRect.Height;
}
