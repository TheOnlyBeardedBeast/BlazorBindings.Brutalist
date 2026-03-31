namespace BlazorBindings.Brutalist;

/// <summary>
/// Platform-neutral key codes used by <see cref="IBrutalistRenderSurface"/>.
/// Values are kept intentionally small and symbolic — each service maps its
/// native key type to this enum before raising events.
/// </summary>
public enum BrutalistKey
{
    Unknown = 0,

    Tab,
    Enter,
    KeyPadEnter,
    Backspace,
    Delete,

    Left,
    Right,
    Up,
    Down,

    LeftShift,
    RightShift,

    // Extend as needed — add new values at the end to avoid breaking switches.
}
