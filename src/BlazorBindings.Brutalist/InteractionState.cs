using BlazorBindings.Brutalist.Elements;

namespace BlazorBindings.Brutalist;

public sealed class InteractionState
{
    public Element? ActiveElement { get; private set; }

    public event Action<Element?>? ActiveElementChanged;

    public void SetActiveElement(Element? element)
    {
        if (ReferenceEquals(ActiveElement, element))
        {
            return;
        }

        ActiveElement = element;
        Console.WriteLine($"[InteractionState] ActiveElement changed: {FormatElement(element)}");
        ActiveElementChanged?.Invoke(element);
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
