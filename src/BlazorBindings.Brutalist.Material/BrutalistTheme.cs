namespace BlazorBindings.Brutalist.Material;

public sealed class BrutalistTheme
{
    public string SurfaceBackground { get; init; } = "#0f172a";
    public string HeadingColor { get; init; } = "#f8fafc";
    public string BodyColor { get; init; } = "#cbd5e1";

    public string CardBackground { get; init; } = "#1e293b";
    public string CardBorderColor { get; init; } = "#334155";

    public string ButtonBackground { get; init; } = "#3b82f6";
    public string ButtonTextColor { get; init; } = "#ffffff";

    public static BrutalistTheme Dark { get; } = new()
    {
        SurfaceBackground = "#0f172a",
        HeadingColor = "#f8fafc",
        BodyColor = "#cbd5e1",
        CardBackground = "#1e293b",
        CardBorderColor = "#334155",
        ButtonBackground = "#3b82f6",
        ButtonTextColor = "#ffffff",
    };

    public static BrutalistTheme Light { get; } = new()
    {
        SurfaceBackground = "#f8fafc",
        HeadingColor = "#0f172a",
        BodyColor = "#334155",
        CardBackground = "#ffffff",
        CardBorderColor = "#d0d7e2",
        ButtonBackground = "#2563eb",
        ButtonTextColor = "#ffffff",
    };
}
