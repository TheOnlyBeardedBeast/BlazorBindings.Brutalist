using SkiaSharp;

namespace BlazorBindings.Brutalist.Elements;

public sealed class DragPreviewEventArgs
{
    public required Element Source { get; init; }
    public Element? HoverTarget { get; init; }
    public required SKPoint Pointer { get; init; }
    public required SKRect PreviewRect { get; init; }
    public required bool IsDragging { get; init; }
    public bool CanDrop { get; init; }
    public string? DragType { get; init; }
    public string? Scope { get; init; }
    public object? DragData { get; init; }
}

public sealed class CanDropEventArgs
{
    public required Element Source { get; init; }
    public required Element Target { get; init; }
    public string? DragType { get; init; }
    public string? Scope { get; init; }
    public object? DragData { get; init; }
}

public sealed class DropTargetStateChangedEventArgs
{
    public Element? Source { get; init; }
    public required Element Target { get; init; }
    public required bool IsDragOver { get; init; }
    public required bool CanDrop { get; init; }
    public string? DragType { get; init; }
    public string? Scope { get; init; }
    public object? DragData { get; init; }
}

public sealed class DropEventArgs
{
    public required Element Source { get; init; }
    public required Element Target { get; init; }
    public required SKPoint Pointer { get; init; }
    public required SKRect PreviewRect { get; init; }
    public string? DragType { get; init; }
    public string? Scope { get; init; }
    public object? DragData { get; init; }
}