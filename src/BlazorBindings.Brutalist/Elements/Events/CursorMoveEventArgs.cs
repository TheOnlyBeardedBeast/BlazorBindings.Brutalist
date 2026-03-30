namespace BlazorBindings.Brutalist.Elements;

public sealed class CursorMoveEventArgs
{
    public required Element Element { get; init; }
    public required float ElementLeft { get; init; }
    public required float ElementTop { get; init; }
    public required float ElementWidth { get; init; }
    public required float ElementHeight { get; init; }
    public required float CursorX { get; init; }
    public required float CursorTop { get; init; }
    public required float CursorBottom { get; init; }
    public required float CursorLocalX { get; init; }
    public required float CursorLocalTop { get; init; }
    public required float CursorLocalBottom { get; init; }
}