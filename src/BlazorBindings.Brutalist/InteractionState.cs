using BlazorBindings.Brutalist.Elements;

namespace BlazorBindings.Brutalist;

public sealed class InteractionState
{
    public Element? ActiveElement { get; private set; }
    public Element? PointerCaptureElement { get; private set; }

    public event Action<Element?>? ActiveElementChanged;
    public event Action<Element?>? PointerCaptureChanged;

    public void SetActiveElement(Element? element)
    {
        if (ReferenceEquals(ActiveElement, element))
        {
            return;
        }

        ActiveElement = element;
        ActiveElementChanged?.Invoke(element);
    }

    public void SetPointerCapture(Element? element)
    {
        if (ReferenceEquals(PointerCaptureElement, element))
        {
            return;
        }

        PointerCaptureElement = element;
        PointerCaptureChanged?.Invoke(element);
    }

    private static string FormatElement(Element? element)
    {
        if (element is null)
        {
            return "<null>";
        }

        var typeName = element.GetType().Name;
        if (string.IsNullOrWhiteSpace(element.Id))
        {
            return typeName;
        }

        return $"{typeName} (Id={element.Id})";
    }
}
