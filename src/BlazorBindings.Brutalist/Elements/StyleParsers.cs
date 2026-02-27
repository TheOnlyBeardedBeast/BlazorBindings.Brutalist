using System.Globalization;
using System.Linq;
using Yoga;

namespace BlazorBindings.Brutalist.Elements;

internal static class StyleParsers
{
    internal enum CssLengthKind
    {
        Point,
        Percent,
        Auto,
    }

    internal readonly record struct CssLength(CssLengthKind Kind, float Value = 0);

    internal static (float top, float right, float bottom, float left) ParseCssValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (0, 0, 0, 0);

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => float.TryParse(p.Trim(), out var f) ? f : 0f)
                        .ToArray();

        return parts.Length switch
        {
            1 => (parts[0], parts[0], parts[0], parts[0]), // all sides
            2 => (parts[0], parts[1], parts[0], parts[1]), // vertical, horizontal
            3 => (parts[0], parts[1], parts[2], parts[1]), // top, horizontal, bottom
            4 => (parts[0], parts[1], parts[2], parts[3]), // top, right, bottom, left
            _ => (0, 0, 0, 0)
        };
    }

    internal static (float topLeft, float topRight, float bottomRight, float bottomLeft) ParseBorderRadius(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (0, 0, 0, 0);

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => float.TryParse(p.Trim(), out var f) ? f : 0f)
                        .ToArray();

        return parts.Length switch
        {
            1 => (parts[0], parts[0], parts[0], parts[0]), // all corners
            2 => (parts[0], parts[1], parts[0], parts[1]), // top-left/bottom-right, top-right/bottom-left
            3 => (parts[0], parts[1], parts[2], parts[1]), // top-left, top-right/bottom-left, bottom-right
            4 => (parts[0], parts[1], parts[2], parts[3]), // top-left, top-right, bottom-right, bottom-left
            _ => (0, 0, 0, 0)
        };
    }

    internal static YGWrap? ParseFlexWrap(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().Replace("-", "").Replace("_", "").ToLowerInvariant();

        return normalized switch
        {
            "nowrap" => YGWrap.YGWrapNoWrap,
            "wrap" => YGWrap.YGWrapWrap,
            "wrapreverse" => YGWrap.YGWrapWrapReverse,
            _ => null
        };
    }

    internal static float? ParseFloat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    internal static CssLength? ParseCssLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        if (normalized.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return new CssLength(CssLengthKind.Auto);
        }

        if (normalized.EndsWith("%", StringComparison.Ordinal))
        {
            var number = ParseFloat(normalized[..^1]);
            return number.HasValue ? new CssLength(CssLengthKind.Percent, number.Value) : null;
        }

        var point = ParseFloat(normalized);
        return point.HasValue ? new CssLength(CssLengthKind.Point, point.Value) : null;
    }

    internal static YGJustify? ParseJustifyContent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().Replace("-", "").Replace("_", "").ToLowerInvariant();

        return normalized switch
        {
            "flexstart" or "start" => YGJustify.YGJustifyFlexStart,
            "center" => YGJustify.YGJustifyCenter,
            "flexend" or "end" => YGJustify.YGJustifyFlexEnd,
            "spacebetween" => YGJustify.YGJustifySpaceBetween,
            "spacearound" => YGJustify.YGJustifySpaceAround,
            "spaceevenly" => YGJustify.YGJustifySpaceEvenly,
            _ => null
        };
    }

    internal static YGAlign? ParseAlign(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().Replace("-", "").Replace("_", "").ToLowerInvariant();

        return normalized switch
        {
            "auto" => YGAlign.YGAlignAuto,
            "flexstart" or "start" => YGAlign.YGAlignFlexStart,
            "center" => YGAlign.YGAlignCenter,
            "flexend" or "end" => YGAlign.YGAlignFlexEnd,
            "stretch" => YGAlign.YGAlignStretch,
            "baseline" => YGAlign.YGAlignBaseline,
            "spacebetween" => YGAlign.YGAlignSpaceBetween,
            "spacearound" => YGAlign.YGAlignSpaceAround,
            _ => null
        };
    }

    internal static YGPositionType? ParsePositionType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().Replace("-", "").Replace("_", "").ToLowerInvariant();
        return normalized switch
        {
            "relative" => YGPositionType.YGPositionTypeRelative,
            "absolute" => YGPositionType.YGPositionTypeAbsolute,
            _ => null,
        };
    }

    internal static YGOverflow? ParseOverflow(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().Replace("-", "").Replace("_", "").ToLowerInvariant();
        return normalized switch
        {
            "visible" => YGOverflow.YGOverflowVisible,
            "hidden" => YGOverflow.YGOverflowHidden,
            "scroll" => YGOverflow.YGOverflowScroll,
            _ => null,
        };
    }

    internal static YGDisplay? ParseDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().Replace("-", "").Replace("_", "").ToLowerInvariant();
        return normalized switch
        {
            "flex" => YGDisplay.YGDisplayFlex,
            "none" => YGDisplay.YGDisplayNone,
            _ => null,
        };
    }
}
