namespace BlazorBindings.Brutalist.Components;

public static class UIntColorExtensions
{
    public static string ToHexColor(this uint argb, bool includeAlpha = false)
    {
        var a = (byte)(argb >> 24);
        var r = (byte)(argb >> 16);
        var g = (byte)(argb >> 8);
        var b = (byte)argb;

        return includeAlpha
            ? $"#{a:X2}{r:X2}{g:X2}{b:X2}"
            : $"#{r:X2}{g:X2}{b:X2}";
    }

    public static string ToHexColor(this uint? argb, bool includeAlpha = false, string fallback = "#000000")
    {
        return argb.HasValue ? argb.Value.ToHexColor(includeAlpha) : fallback;
    }
}
