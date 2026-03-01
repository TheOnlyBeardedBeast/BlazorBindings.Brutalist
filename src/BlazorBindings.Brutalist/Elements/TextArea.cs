using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace BlazorBindings.Brutalist.Elements;

public class YogaTextArea : YogaTextInput
{
    [Parameter]
    public bool WrapText { get; set; } = true;

    [Parameter]
    public float? LineHeight { get; set; }

    public YogaTextArea() : base()
    {
        unsafe
        {
            Yoga.YG.NodeStyleSetMinHeight(Node, 96f);
            Yoga.YG.NodeSetMeasureFunc(Node, &MeasureNode);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe Yoga.YGSize MeasureNode(
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
        if (handle.Target is not YogaTextArea element)
        {
            return size;
        }

        var textToMeasure = string.IsNullOrEmpty(element._currentValue)
            ? (element.Placeholder ?? string.Empty)
            : element.GetDisplayValue();

        using var font = new SKFont
        {
            Size = element.FontSize ?? 16f,
        };

        var lineHeight = element.LineHeight ?? (font.Metrics.Descent - font.Metrics.Ascent);

        var (paddingTop, paddingRight, paddingBottom, paddingLeft) =
            string.IsNullOrWhiteSpace(element.Padding)
                ? (0f, 0f, 0f, 0f)
                : StyleParsers.ParseCssValue(element.Padding);

        var hasWidthConstraint = widthMode != Yoga.YGMeasureMode.YGMeasureModeUndefined;
        var contentWidth = hasWidthConstraint
            ? Math.Max(0f, width - paddingLeft - paddingRight)
            : 0f;

        var lines = BuildLineLayout(
            textToMeasure,
            contentWidth,
            font,
            element.WrapText && hasWidthConstraint);

        var measuredTextWidth = 0f;
        foreach (var line in lines)
        {
            measuredTextWidth = Math.Max(measuredTextWidth, font.MeasureText(line.Text));
        }

        var measuredTextHeight = Math.Max(1, lines.Count) * lineHeight;

        var measuredWidth = measuredTextWidth + paddingLeft + paddingRight;
        var measuredHeight = measuredTextHeight + paddingTop + paddingBottom;

        size.width = widthMode switch
        {
            Yoga.YGMeasureMode.YGMeasureModeExactly => width,
            Yoga.YGMeasureMode.YGMeasureModeAtMost => Math.Min(measuredWidth, width),
            _ => measuredWidth,
        };

        size.height = heightMode switch
        {
            Yoga.YGMeasureMode.YGMeasureModeExactly => height,
            Yoga.YGMeasureMode.YGMeasureModeAtMost => measuredHeight,
            _ => measuredHeight,
        };

        return size;
    }

    protected override bool HandleTextInput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var filtered = new string(normalized.Where(ch => ch == '\n' || ch == '\t' || !char.IsControl(ch)).ToArray());
        if (string.IsNullOrEmpty(filtered))
        {
            return false;
        }

        var insertionIndex = _currentValue.Length - _caretOffset;
        var newValue = _currentValue.Insert(insertionIndex, filtered);

        SetValue(newValue);
        ShowCaretNow();
        return true;
    }

    protected override bool HandleKeyDown(Keys key)
    {
        return key switch
        {
            Keys.Enter => InsertNewLine(),
            Keys.Left => HandleLeftArrow(),
            Keys.Right => HandleRightArrow(),
            Keys.Up => MoveCaretUp(),
            Keys.Down => MoveCaretDown(),
            Keys.Backspace => HandleBackspace(),
            Keys.Delete => HandleDelete(),
            _ => false,
        };
    }

    private bool MoveCaretUp()
    {
        using var font = new SKFont { Size = FontSize ?? 16f };
        var display = GetDisplayValue();
        var wrapWidth = GetCurrentWrapWidth();
        var lines = BuildLineLayout(display, wrapWidth, font, WrapText);

        var caretIndex = Math.Clamp(display.Length - _caretOffset, 0, display.Length);

        var lineIndex = 0;
        var column = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var start = lines[i].StartIndex;
            var end = start + lines[i].Text.Length;
            if (caretIndex <= end)
            {
                lineIndex = i;
                column = Math.Max(0, caretIndex - start);
                break;
            }
        }

        if (lineIndex <= 0)
        {
            // already at first line -> move to start of first line
            var newIndex = lines[0].StartIndex + 0;
            _caretOffset = Math.Max(0, display.Length - newIndex);
            ShowCaretNow();
            return false;
        }

        // desired x position
        var prefix = column <= 0 ? string.Empty : lines[lineIndex].Text[..Math.Min(column, lines[lineIndex].Text.Length)];
        var desiredX = font.MeasureText(prefix);

        var targetLine = lines[lineIndex - 1];
        var bestCol = 0;
        var bestDiff = float.MaxValue;
        for (var c = 0; c <= targetLine.Text.Length; c++)
        {
            var sample = targetLine.Text.Substring(0, c);
            var w = font.MeasureText(sample);
            var diff = Math.Abs(w - desiredX);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestCol = c;
            }
        }

        var newCaretIndex = targetLine.StartIndex + bestCol;
        _caretOffset = Math.Max(0, display.Length - newCaretIndex);
        ShowCaretNow();
        return false;
    }

    private bool MoveCaretDown()
    {
        using var font = new SKFont { Size = FontSize ?? 16f };
        var display = GetDisplayValue();
        var wrapWidth = GetCurrentWrapWidth();
        var lines = BuildLineLayout(display, wrapWidth, font, WrapText);

        var caretIndex = Math.Clamp(display.Length - _caretOffset, 0, display.Length);

        var lineIndex = 0;
        var column = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var start = lines[i].StartIndex;
            var end = start + lines[i].Text.Length;
            if (caretIndex <= end)
            {
                lineIndex = i;
                column = Math.Max(0, caretIndex - start);
                break;
            }
        }

        if (lineIndex >= lines.Count - 1)
        {
            // already at last line -> move to end of last line
            var last = lines[^1];
            var newIndex = last.StartIndex + last.Text.Length;
            _caretOffset = Math.Max(0, display.Length - newIndex);
            ShowCaretNow();
            return false;
        }

        var prefix = column <= 0 ? string.Empty : lines[lineIndex].Text[..Math.Min(column, lines[lineIndex].Text.Length)];
        var desiredX = font.MeasureText(prefix);

        var targetLine = lines[lineIndex + 1];
        var bestCol = 0;
        var bestDiff = float.MaxValue;
        for (var c = 0; c <= targetLine.Text.Length; c++)
        {
            var sample = targetLine.Text.Substring(0, c);
            var w = font.MeasureText(sample);
            var diff = Math.Abs(w - desiredX);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestCol = c;
            }
        }

        var newCaretIndex = targetLine.StartIndex + bestCol;
        _caretOffset = Math.Max(0, display.Length - newCaretIndex);
        ShowCaretNow();
        return false;
    }

    private float GetCurrentWrapWidth()
    {
        float width;
        unsafe
        {
            width = Yoga.YG.NodeLayoutGetWidth(Node);
        }

        var (_, paddingRight, _, paddingLeft) =
            string.IsNullOrWhiteSpace(Padding)
                ? (0f, 0f, 0f, 0f)
                : StyleParsers.ParseCssValue(Padding);

        return Math.Max(0f, width - paddingLeft - paddingRight);
    }

    private bool InsertNewLine()
    {
        var insertionIndex = _currentValue.Length - _caretOffset;
        var newValue = _currentValue.Insert(insertionIndex, "\n");
        SetValue(newValue);
        ShowCaretNow();
        return true;
    }

    protected override void RenderText(SKCanvas canvas, SKRect textBounds)
    {
        var isEmpty = string.IsNullOrEmpty(_currentValue);
        var displayValue = GetDisplayValue();
        var textToDraw = isEmpty ? (Placeholder ?? string.Empty) : displayValue;

        using var font = new SKFont
        {
            Size = FontSize ?? 16f,
        };

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            Color = isEmpty
                ? (string.IsNullOrWhiteSpace(PlaceholderColor) ? SKColor.Parse("#999999") : SKColor.Parse(PlaceholderColor))
                : (string.IsNullOrWhiteSpace(Color) ? SKColors.Black : SKColor.Parse(Color)),
        };

        var lines = BuildLineLayout(textToDraw, textBounds.Width, font, WrapText);
        var lineHeight = LineHeight ?? (font.Metrics.Descent - font.Metrics.Ascent);
        var baseline = textBounds.Top - font.Metrics.Ascent;

        canvas.Save();
        canvas.ClipRect(textBounds);

        for (var i = 0; i < lines.Count; i++)
        {
            var lineBaseline = baseline + (i * lineHeight);
            if (lineBaseline + font.Metrics.Ascent > textBounds.Bottom)
            {
                break;
            }

            if (lineBaseline + font.Metrics.Descent < textBounds.Top)
            {
                continue;
            }

            canvas.DrawText(lines[i].Text, textBounds.Left, lineBaseline, SKTextAlign.Left, font, paint);
        }

        canvas.Restore();

        // Scrolling request for caret moved to RenderCaret where caret metrics are available.
    }

    protected override void RenderCaret(SKCanvas canvas, SKRect textBounds)
    {
        if (!_isFocused || !_caretVisible)
        {
            return;
        }

        if (!TryGetCursorMetrics(out var caretX, out var caretTop, out var caretBottom))
        {
            return;
        }

        using var caretPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = false,
            Color = string.IsNullOrWhiteSpace(Color) ? SKColors.Black : SKColor.Parse(Color),
        };

        canvas.Save();
        canvas.ClipRect(textBounds);
        canvas.DrawLine(caretX, caretTop, caretX, caretBottom, caretPaint);
        canvas.Restore();
    }

    protected override bool TryGetCursorMetrics(out float cursorX, out float cursorTop, out float cursorBottom)
    {
        cursorX = 0f;
        cursorTop = 0f;
        cursorBottom = 0f;

        if (!_isFocused)
        {
            return false;
        }

        var offset = GetOffset();
        float width;
        float height;
        unsafe
        {
            width = Yoga.YG.NodeLayoutGetWidth(Node);
            height = Yoga.YG.NodeLayoutGetHeight(Node);
        }

        var bounds = SKRect.Create(offset.left, offset.top, width, height);
        var (paddingTop, paddingRight, paddingBottom, paddingLeft) =
            string.IsNullOrWhiteSpace(Padding)
                ? (0f, 0f, 0f, 0f)
                : StyleParsers.ParseCssValue(Padding);

        var textBounds = SKRect.Create(
            bounds.Left + paddingLeft,
            bounds.Top + paddingTop,
            Math.Max(0, bounds.Width - paddingLeft - paddingRight),
            Math.Max(0, bounds.Height - paddingTop - paddingBottom));

        using var font = new SKFont
        {
            Size = FontSize ?? 16f,
        };

        var displayValue = GetDisplayValue();
        var lines = BuildLineLayout(displayValue, textBounds.Width, font, WrapText);
        var caretIndex = Math.Clamp(displayValue.Length - _caretOffset, 0, displayValue.Length);
        var lineHeight = LineHeight ?? (font.Metrics.Descent - font.Metrics.Ascent);
        var firstBaseline = textBounds.Top - font.Metrics.Ascent;

        var caretLineIndex = 0;
        var caretColumn = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            var lineStart = lines[i].StartIndex;
            var lineEnd = lineStart + lines[i].Text.Length;

            if (caretIndex <= lineEnd)
            {
                caretLineIndex = i;
                caretColumn = Math.Max(0, caretIndex - lineStart);
                break;
            }

            if (i == lines.Count - 1)
            {
                caretLineIndex = i;
                caretColumn = lines[i].Text.Length;
            }
        }

        var caretLine = lines[Math.Clamp(caretLineIndex, 0, lines.Count - 1)].Text;
        var caretPrefix = caretColumn <= 0
            ? string.Empty
            : caretLine[..Math.Min(caretColumn, caretLine.Length)];

        cursorX = textBounds.Left + font.MeasureText(caretPrefix) + 1f;
        var caretBaseline = firstBaseline + (caretLineIndex * lineHeight);
        cursorTop = caretBaseline + font.Metrics.Ascent;
        cursorBottom = caretBaseline + font.Metrics.Descent;
        return true;
    }

    private static List<TextAreaLineLayout> BuildLineLayout(string source, float maxWidth, SKFont font, bool wrapText)
    {
        if (string.IsNullOrEmpty(source))
        {
            return [new TextAreaLineLayout(0, string.Empty)];
        }

        var lines = new List<TextAreaLineLayout>();
        var current = new StringBuilder();
        var currentStartIndex = 0;

        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];

            if (ch == '\n')
            {
                lines.Add(new TextAreaLineLayout(currentStartIndex, current.ToString()));
                current.Clear();
                currentStartIndex = i + 1;
                continue;
            }

            var shouldWrap = wrapText && maxWidth > 0;
            if (shouldWrap && current.Length > 0)
            {
                var candidate = current.ToString() + ch;
                if (font.MeasureText(candidate) > maxWidth)
                {
                    lines.Add(new TextAreaLineLayout(currentStartIndex, current.ToString()));
                    current.Clear();
                    currentStartIndex = i;
                }
            }

            current.Append(ch);
        }

        lines.Add(new TextAreaLineLayout(currentStartIndex, current.ToString()));

        return lines;
    }

    private sealed record TextAreaLineLayout(int StartIndex, string Text);
}