using SkiaSharp;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace BlazorBindings.Brutalist.Elements;

public unsafe class YogaText : YogaView, IHandleChildContentText
{
    private static readonly ConcurrentDictionary<string, SKTypeface?> TypefaceCache = new();

    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public string? Color { get; set; }

    [Parameter]
    public float? FontSize { get; set; }

    [Parameter]
    public float? LineHeight { get; set; }
    [Parameter]
    public bool CenterText { get; set; } = true;

    [Parameter]
    public string? TextAlign { get; set; }

    [Parameter]
    public bool Ellipse { get; set; }

    [Parameter]
    public bool WrapText { get; set; }

    [Parameter]
    public string? FontFamily { get; set; }

    public YogaText() : base()
    {
        Yoga.YG.NodeSetMeasureFunc(Node, &MeasureNode);
    }

    private SKTypeface? GetOrCreateTypeface()
    {
        if (string.IsNullOrWhiteSpace(FontFamily))
        {
            return null;
        }

        // Check cache first
        if (TypefaceCache.TryGetValue(FontFamily, out var cached))
        {
            return cached;
        }

        SKTypeface? typeface = null;

        // Try to load from file path if it exists
        if (System.IO.File.Exists(FontFamily))
        {
            try
            {
                typeface = SKTypeface.FromFile(FontFamily);
            }
            catch
            {
                // Fall through to try as font family name
            }
        }

        // Try to load as system font family if file load failed
        if (typeface == null)
        {
            try
            {
                typeface = SKTypeface.FromFamilyName(FontFamily);
            }
            catch
            {
                // typeface remains null
            }
        }

        // Cache the result (including null values to avoid repeated lookups)
        TypefaceCache.TryAdd(FontFamily, typeface);
        return typeface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static Yoga.YGSize MeasureNode(
        Yoga.YGNode* node,
        float width,
        Yoga.YGMeasureMode widthMode,
        float height,
        Yoga.YGMeasureMode heightMode)
    {
        var size = new Yoga.YGSize { width = 0, height = 0 };

        var ptr = Yoga.YG.NodeGetContext(node);
        if (ptr is null)
        {
            return size;
        }

        var handle = GCHandle.FromIntPtr((IntPtr)ptr);
        if (handle.Target is not YogaText element || string.IsNullOrWhiteSpace(element.Text))
        {
            return size;
        }

        using var font = new SKFont
        {
            Size = element.FontSize ?? 16f,
            Typeface = element.GetOrCreateTypeface(),
        };

        var measuredWidth = font.MeasureText(element.Text);
        var defaultLineHeight = font.Metrics.Descent - font.Metrics.Ascent;
        var lineHeight = element.LineHeight ?? defaultLineHeight;
        var measuredHeight = lineHeight;

        if (element.WrapText && widthMode != Yoga.YGMeasureMode.YGMeasureModeUndefined)
        {
            var wrapWidth = Math.Max(0, width);
            var lines = BuildWrappedLines(element.Text, wrapWidth, font);
            if (lines.Count > 0)
            {
                measuredWidth = 0;
                foreach (var line in lines)
                {
                    measuredWidth = Math.Max(measuredWidth, font.MeasureText(line));
                }

                measuredHeight = lines.Count * lineHeight;
            }
        }

        size.width = widthMode switch
        {
            Yoga.YGMeasureMode.YGMeasureModeExactly => width,
            Yoga.YGMeasureMode.YGMeasureModeAtMost => Math.Min(measuredWidth, width),
            _ => measuredWidth,
        };

        size.height = heightMode switch
        {
            Yoga.YGMeasureMode.YGMeasureModeExactly => height,
            Yoga.YGMeasureMode.YGMeasureModeAtMost => Math.Min(measuredHeight, height),
            _ => measuredHeight,
        };

        return size;
    }

    private Element? TryGetParentElement()
    {
        var parent = Yoga.YG.NodeGetParent(Node);
        if (parent is null)
        {
            return null;
        }

        var ptr = Yoga.YG.NodeGetContext(parent);
        if (ptr is null)
        {
            return null;
        }

        var handle = GCHandle.FromIntPtr((IntPtr)ptr);
        return handle.Target as Element;
    }

    private SKTextAlign GetResolvedTextAlign()
    {
        if (!string.IsNullOrWhiteSpace(TextAlign))
        {
            var normalized = TextAlign.Trim().Replace("-", "").Replace("_", "").ToLowerInvariant();
            return normalized switch
            {
                "left" or "start" => SKTextAlign.Left,
                "center" => SKTextAlign.Center,
                "right" or "end" => SKTextAlign.Right,
                _ => CenterText ? SKTextAlign.Center : SKTextAlign.Left,
            };
        }

        return CenterText ? SKTextAlign.Center : SKTextAlign.Left;
    }

    private static List<string> BuildWrappedLines(string text, float maxWidth, SKFont font)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return lines;
        }

        if (maxWidth <= 0)
        {
            lines.Add(string.Empty);
            return lines;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            lines.Add(string.Empty);
            return lines;
        }

        var current = string.Empty;
        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (font.MeasureText(candidate) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
                current = string.Empty;
            }

            if (font.MeasureText(word) <= maxWidth)
            {
                current = word;
                continue;
            }

            var chunk = string.Empty;
            foreach (var ch in word)
            {
                var chunkCandidate = chunk + ch;
                if (font.MeasureText(chunkCandidate) <= maxWidth)
                {
                    chunk = chunkCandidate;
                }
                else
                {
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        lines.Add(chunk);
                    }
                    chunk = ch.ToString();
                }
            }

            if (!string.IsNullOrEmpty(chunk))
            {
                current = chunk;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            lines.Add(current);
        }

        return lines;
    }

    private bool TryGetParentBounds(out SKRect parentBounds)
    {
        var parent = Yoga.YG.NodeGetParent(Node);
        var parentElement = TryGetParentElement();
        if (parent is null || parentElement is null)
        {
            parentBounds = SKRect.Empty;
            return false;
        }

        var parentOffset = parentElement.GetOffset();
        parentBounds = SKRect.Create(
            parentOffset.left,
            parentOffset.top,
            Yoga.YG.NodeLayoutGetWidth(parent),
            Yoga.YG.NodeLayoutGetHeight(parent));

        return parentBounds.Width > 0 && parentBounds.Height > 0;
    }

    void IHandleChildContentText.HandleText(int index, string text)
    {
        // Razor child-content often includes indentation/newlines.
        // Normalize multiline content so visual alignment is based on the actual text.
        Text = text.Contains('\n') || text.Contains('\r')
            ? text.Trim()
            : text;
        Yoga.YG.NodeMarkDirty(Node);
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        var task = base.SetParametersAsync(parameters);
        Yoga.YG.NodeMarkDirty(Node);
        return task;
    }

    public override void RenderSkia()
    {
        base.RenderSkia();

        if (string.IsNullOrWhiteSpace(Text))
        {
            return;
        }

        var canvas = OpenTkService.Canvas;
        var offset = GetOffset();
        var bounds = SKRect.Create(offset.left, offset.top, Yoga.YG.NodeLayoutGetWidth(Node), Yoga.YG.NodeLayoutGetHeight(Node));
        var (paddingTop, paddingRight, paddingBottom, paddingLeft) =
            string.IsNullOrWhiteSpace(Padding)
                ? (0f, 0f, 0f, 0f)
                : StyleParsers.ParseCssValue(Padding);

        var contentBounds = SKRect.Create(
            bounds.Left + paddingLeft,
            bounds.Top + paddingTop,
            Math.Max(0, bounds.Width - paddingLeft - paddingRight),
            Math.Max(0, bounds.Height - paddingTop - paddingBottom));

        var clipBounds = bounds;
        var visibleContentBounds = contentBounds;
        if (TryGetParentBounds(out var parentBounds))
        {
            clipBounds = SKRect.Intersect(bounds, parentBounds);
            visibleContentBounds = SKRect.Intersect(contentBounds, parentBounds);
        }

        if (visibleContentBounds.Width <= 0 || visibleContentBounds.Height <= 0)
        {
            return;
        }

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = string.IsNullOrWhiteSpace(Color) ? SKColors.Black : SKColor.Parse(Color),
            Style = SKPaintStyle.Fill,
        };
        using var font = new SKFont
        {
            Size = FontSize ?? 16f,
            Typeface = GetOrCreateTypeface(),
        };

        var resolvedTextAlign = GetResolvedTextAlign();
        var y = visibleContentBounds.MidY - ((font.Metrics.Ascent + font.Metrics.Descent) / 2);
        var x = resolvedTextAlign switch
        {
            SKTextAlign.Center => visibleContentBounds.MidX,
            SKTextAlign.Right => visibleContentBounds.Right,
            _ => visibleContentBounds.Left,
        };

        var textToDraw = Text;
        var drawEllipsis = false;
        const string ellipsis = "...";

        if (Ellipse && visibleContentBounds.Width > 0)
        {
            var fullTextWidth = font.MeasureText(textToDraw);
            if (fullTextWidth > visibleContentBounds.Width)
            {
                drawEllipsis = true;
                var ellipsisWidth = font.MeasureText(ellipsis);
                var availableWidthForText = Math.Max(0, visibleContentBounds.Width - ellipsisWidth);

                if (availableWidthForText <= 0)
                {
                    textToDraw = string.Empty;
                }
                else
                {
                    var low = 0;
                    var high = textToDraw.Length;
                    while (low < high)
                    {
                        var mid = (low + high + 1) / 2;
                        var candidate = textToDraw[..mid];
                        if (font.MeasureText(candidate) <= availableWidthForText)
                        {
                            low = mid;
                        }
                        else
                        {
                            high = mid - 1;
                        }
                    }

                    textToDraw = textToDraw[..low];
                }
            }
        }

        canvas.Save();
        canvas.ClipRect(clipBounds);

        if (WrapText)
        {
            var lines = BuildWrappedLines(Text, visibleContentBounds.Width, font);
            var defaultLineHeight = font.Metrics.Descent - font.Metrics.Ascent;
            var lineHeight = LineHeight ?? defaultLineHeight;
            var lineY = visibleContentBounds.Top - font.Metrics.Ascent;

            foreach (var line in lines)
            {
                if (lineY > visibleContentBounds.Bottom)
                {
                    break;
                }

                var lineX = resolvedTextAlign switch
                {
                    SKTextAlign.Center => visibleContentBounds.MidX,
                    SKTextAlign.Right => visibleContentBounds.Right,
                    _ => visibleContentBounds.Left,
                };
                canvas.DrawText(line, lineX, lineY, resolvedTextAlign, font, paint);
                lineY += lineHeight;
            }
        }
        else if (drawEllipsis)
        {
            var textWidth = font.MeasureText(textToDraw);
            var ellipsisWidth = font.MeasureText(ellipsis);
            var totalWidth = textWidth + ellipsisWidth;
            var startX = resolvedTextAlign switch
            {
                SKTextAlign.Center => visibleContentBounds.Left + Math.Max(0, (visibleContentBounds.Width - totalWidth) / 2f),
                SKTextAlign.Right => visibleContentBounds.Right - totalWidth,
                _ => visibleContentBounds.Left,
            };

            canvas.DrawText(textToDraw, startX, y, SKTextAlign.Left, font, paint);

            var ellipsisX = startX + textWidth;
            canvas.DrawText(ellipsis, ellipsisX, y, SKTextAlign.Left, font, paint);
        }
        else
        {
            canvas.DrawText(textToDraw, x, y, resolvedTextAlign, font, paint);
        }

        canvas.Restore();
    }
}
