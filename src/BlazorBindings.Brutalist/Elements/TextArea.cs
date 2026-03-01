using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;
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
        }
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
        var lines = BuildLineLayout(display, /* maxWidth */ 10000f, font, WrapText);

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
        var acc = 0f;
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
        var lines = BuildLineLayout(display, /* maxWidth */ 10000f, font, WrapText);

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
    }

    protected override void RenderCaret(SKCanvas canvas, SKRect textBounds)
    {
        if (!_isFocused || !_caretVisible)
        {
            return;
        }

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

        var caretX = textBounds.Left + font.MeasureText(caretPrefix) + 1f;
        var caretBaseline = firstBaseline + (caretLineIndex * lineHeight);
        var caretTop = caretBaseline + font.Metrics.Ascent;
        var caretBottom = caretBaseline + font.Metrics.Descent;

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

    private static List<TextAreaLineLayout> BuildLineLayout(string text, float maxWidth, SKFont font, bool wrapText)
    {
        var lines = new List<TextAreaLineLayout>();
        var source = text.Replace("\r\n", "\n").Replace('\r', '\n');

        if (source.Length == 0)
        {
            lines.Add(new TextAreaLineLayout(0, string.Empty));
            return lines;
        }

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