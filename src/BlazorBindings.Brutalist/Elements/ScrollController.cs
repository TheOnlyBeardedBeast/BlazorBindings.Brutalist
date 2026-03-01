using System;

namespace BlazorBindings.Brutalist.Elements;

public sealed class ScrollRequest
{
    public Element? Element { get; }
    public float LocalTop { get; }
    public float LocalBottom { get; }

    public ScrollRequest(Element? element, float localTop, float localBottom)
    {
        Element = element;
        LocalTop = localTop;
        LocalBottom = localBottom;
    }
}

public sealed class ScrollController
{
    public event Action<ScrollRequest>? ScrollRequested;

    public void EnsureVisible(Element element, float localTop, float localBottom)
    {
        ScrollRequested?.Invoke(new ScrollRequest(element, localTop, localBottom));
    }

    public void ScrollTo(float y)
    {
        // Convenience: request a scroll where top==bottom==y
        ScrollRequested?.Invoke(new ScrollRequest(null, y, y));
    }
}
