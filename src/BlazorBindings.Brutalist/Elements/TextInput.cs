using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BlazorBindings.Brutalist.Elements;

public class YogaTextInput : YogaView, IDisposable
{
    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public EventCallback<string> OnInput { get; set; }

    [Parameter]
    public EventCallback OnFocus { get; set; }

    [Parameter]
    public EventCallback OnBlur { get; set; }

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public string? Color { get; set; }

    [Parameter]
    public string? PlaceholderColor { get; set; }

    [Parameter]
    public float? FontSize { get; set; }

    [Parameter]
    public bool IsPassword { get; set; }

    [Parameter]
    public string PasswordMask { get; set; } = "•";

    [Parameter]
    public EventCallback<CursorMoveEventArgs> OnCursorMove { get; set; }

    [Inject]
    protected InteractionState InteractionState { get; set; } = default!;

    [Inject]
    protected AnimationTicker AnimationTicker { get; set; } = default!;

    protected string _currentValue = string.Empty;
    protected bool _subscriptionsInitialized;
    protected bool _isFocused;
    protected bool _caretVisible = true;
    protected float _caretBlinkElapsed;
    protected const float CaretBlinkIntervalSeconds = 0.5f;

    protected int _caretOffset = 0;
    private float _lastCursorX = float.NaN;
    private float _lastCursorTop = float.NaN;
    private float _lastCursorBottom = float.NaN;

    public YogaTextInput()
    {
        unsafe
        {
            Yoga.YG.NodeStyleSetMinHeight(Node, 36f);
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
        if (handle.Target is not YogaTextInput element)
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

        var measuredTextWidth = string.IsNullOrEmpty(textToMeasure) ? 0f : font.MeasureText(textToMeasure);
        var measuredTextHeight = font.Metrics.Descent - font.Metrics.Ascent;

        var (paddingTop, paddingRight, paddingBottom, paddingLeft) =
            string.IsNullOrWhiteSpace(element.Padding)
                ? (0f, 0f, 0f, 0f)
                : StyleParsers.ParseCssValue(element.Padding);

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
            Yoga.YGMeasureMode.YGMeasureModeAtMost => Math.Min(measuredHeight, height),
            _ => measuredHeight,
        };

        return size;
    }

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        var hasValue = parameters.TryGetValue<string>(nameof(Value), out var incomingValue);
        await base.SetParametersAsync(parameters);

        EnsureSubscriptions();

        if (hasValue)
        {
            _currentValue = incomingValue ?? string.Empty;
        }

        unsafe
        {
            Yoga.YG.NodeMarkDirty(Node);
        }
    }

    public override void RenderSkia()
    {
        base.RenderSkia();

        var canvas = OpenTkService.Canvas;
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

        RenderText(canvas, textBounds);
        RenderCaret(canvas, textBounds);
        RenderPostMain(canvas, bounds, textBounds);
    }

    protected virtual void RenderPostMain(SKCanvas canvas, SKRect bounds, SKRect textBounds)
    {
    }

    protected virtual void RenderText(SKCanvas canvas, SKRect textBounds)
    {
        var isEmpty = string.IsNullOrEmpty(_currentValue);
        var displayValue = GetDisplayValue();
        var textToDraw = isEmpty ? (Placeholder ?? string.Empty) : displayValue;

        using var font = new SKFont
        {
            Size = FontSize ?? 16f,
        };

        var scrollOffset = CalculateScrollOffset(font, textBounds, displayValue);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            Color = isEmpty
                ? (string.IsNullOrWhiteSpace(PlaceholderColor) ? SKColor.Parse("#999999") : SKColor.Parse(PlaceholderColor))
                : (string.IsNullOrWhiteSpace(Color) ? SKColors.Black : SKColor.Parse(Color)),
        };

        var lineHeight = font.Metrics.Descent - font.Metrics.Ascent;
        var baseline = textBounds.Top + ((textBounds.Height - lineHeight) / 2f) - font.Metrics.Ascent;

        canvas.Save();
        canvas.ClipRect(textBounds);
        canvas.DrawText(textToDraw, textBounds.Left + scrollOffset, baseline, SKTextAlign.Left, font, paint);
        canvas.Restore();
    }

    protected virtual void RenderCaret(SKCanvas canvas, SKRect textBounds)
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

    protected virtual bool TryGetCursorMetrics(out float cursorX, out float cursorTop, out float cursorBottom)
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
        var scrollOffset = CalculateScrollOffset(font, textBounds, displayValue);
        var lineHeight = font.Metrics.Descent - font.Metrics.Ascent;
        var baseline = textBounds.Top + ((textBounds.Height - lineHeight) / 2f) - font.Metrics.Ascent;
        var caretText = displayValue.Substring(0, displayValue.Length - _caretOffset);
        var textWidth = font.MeasureText(caretText);

        cursorX = textBounds.Left + scrollOffset + textWidth + 1f;
        cursorTop = baseline + font.Metrics.Ascent;
        cursorBottom = baseline + font.Metrics.Descent;
        return true;
    }

    protected void NotifyCursorMove()
    {
        if (!OnCursorMove.HasDelegate)
        {
            return;
        }

        if (!TryGetCursorMetrics(out var cursorX, out var cursorTop, out var cursorBottom))
        {
            return;
        }

        if (Math.Abs(_lastCursorX - cursorX) < 0.1f
            && Math.Abs(_lastCursorTop - cursorTop) < 0.1f
            && Math.Abs(_lastCursorBottom - cursorBottom) < 0.1f)
        {
            return;
        }

        _lastCursorX = cursorX;
        _lastCursorTop = cursorTop;
        _lastCursorBottom = cursorBottom;

        var offset = GetOffset();
        float width;
        float height;
        unsafe
        {
            width = Yoga.YG.NodeLayoutGetWidth(Node);
            height = Yoga.YG.NodeLayoutGetHeight(Node);
        }

        var elementLeft = offset.left;
        var elementTop = offset.top;

        var args = new CursorMoveEventArgs
        {
            Element = this,
            ElementLeft = elementLeft,
            ElementTop = elementTop,
            ElementWidth = width,
            ElementHeight = height,
            CursorX = cursorX,
            CursorTop = cursorTop,
            CursorBottom = cursorBottom,
            CursorLocalX = cursorX - elementLeft,
            CursorLocalTop = cursorTop - elementTop,
            CursorLocalBottom = cursorBottom - elementTop,
        };

        _ = InvokeEventCallbackAsync(OnCursorMove, args);
    }

    protected float CalculateScrollOffset(SKFont font, SKRect textBounds, string displayValue)
    {
        if (string.IsNullOrEmpty(displayValue))
        {
            return 0f;
        }

        // Calculate caret position in the text
        var caretText = displayValue.Substring(0, displayValue.Length - _caretOffset);
        var caretPixelPosition = font.MeasureText(caretText);

        // Padding for visual feedback
        const float caretPadding = 10f;
        var visibleWidth = textBounds.Width - caretPadding;

        // If caret is beyond the right edge, scroll left
        if (caretPixelPosition > visibleWidth)
        {
            return -(caretPixelPosition - visibleWidth);
        }

        // If caret is before the left edge, scroll right
        if (caretPixelPosition < 0)
        {
            return -caretPixelPosition;
        }

        return 0f;
    }

    protected string GetDisplayValue()
    {
        if (string.IsNullOrEmpty(_currentValue) || !IsPassword)
        {
            return _currentValue;
        }

        var maskChar = string.IsNullOrEmpty(PasswordMask) ? '•' : PasswordMask[0];
        return new string(maskChar, _currentValue.Length);
    }

    protected override bool HandleTextInput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var filtered = new string(text.Where(ch => !char.IsControl(ch)).ToArray());
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
            Keys.Left => HandleLeftArrow(),
            Keys.Right => HandleRightArrow(),
            Keys.Backspace => HandleBackspace(),
            Keys.Delete => HandleDelete(),
            _ => false,
        };
    }

    protected bool HandleLeftArrow()
    {
        if (_caretOffset < _currentValue.Length)
        {
            _caretOffset++;
            ShowCaretNow();
            return false;
        }

        return false;
    }

    protected bool HandleRightArrow()
    {
        if (_caretOffset > 0)
        {
            _caretOffset--;
            ShowCaretNow();
            return false;
        }

        return false;
    }

    protected bool HandleBackspace()
    {
        if (string.IsNullOrEmpty(_currentValue))
        {
            return true;
        }

        var deletionIndex = _currentValue.Length - _caretOffset - 1;
        if (deletionIndex < 0)
        {
            return true;
        }

        var newValue = _currentValue.Remove(deletionIndex, 1);
        SetValue(newValue);
        ShowCaretNow();
        return true;
    }

    protected bool HandleDelete()
    {
        if (string.IsNullOrEmpty(_currentValue))
        {
            return true;
        }

        var deletionIndex = _currentValue.Length - _caretOffset;
        if (deletionIndex >= _currentValue.Length)
        {
            return true;
        }

        var newValue = _currentValue.Remove(deletionIndex, 1);

        _caretOffset = Math.Max(0, _caretOffset - 1);

        SetValue(newValue);
        ShowCaretNow();
        return true;
    }

    protected void EnsureSubscriptions()
    {
        if (_subscriptionsInitialized)
        {
            return;
        }

        InteractionState.ActiveElementChanged += OnActiveElementChanged;
        AnimationTicker.Tick += OnAnimationTick;
        _subscriptionsInitialized = true;
        _isFocused = ReferenceEquals(InteractionState.ActiveElement, this);
        if (_isFocused)
        {
            ShowCaretNow();
        }
    }

    protected void OnActiveElementChanged(Element? element)
    {
        var focused = ReferenceEquals(element, this);
        if (_isFocused == focused)
        {
            return;
        }

        _isFocused = focused;
        if (_isFocused)
        {
            if (OnFocus.HasDelegate)
            {
                _ = InvokeEventCallbackAsync(OnFocus);
            }

            ShowCaretNow();
        }
        else
        {
            if (OnBlur.HasDelegate)
            {
                _ = InvokeEventCallbackAsync(OnBlur);
            }

            _caretVisible = false;
            _caretBlinkElapsed = 0;
            _lastCursorX = float.NaN;
            _lastCursorTop = float.NaN;
            _lastCursorBottom = float.NaN;
        }

        _ = InvokeAsync(StateHasChanged);
    }

    protected void OnAnimationTick(float deltaSeconds, double elapsedSeconds)
    {
        if (!_isFocused)
        {
            return;
        }

        _caretBlinkElapsed += deltaSeconds;
        if (_caretBlinkElapsed < CaretBlinkIntervalSeconds)
        {
            return;
        }

        while (_caretBlinkElapsed >= CaretBlinkIntervalSeconds)
        {
            _caretBlinkElapsed -= CaretBlinkIntervalSeconds;
            _caretVisible = !_caretVisible;
        }

        _ = InvokeAsync(StateHasChanged);
    }

    protected void ShowCaretNow()
    {
        _caretVisible = true;
        _caretBlinkElapsed = 0;

        if (_isFocused)
        {
            _ = InvokeAsync(NotifyCursorMove);
        }
    }

    protected void SetValue(string value)
    {
        Console.WriteLine($"Setting value: '{value}'");
        if (_currentValue == value)
        {
            return;
        }

        _currentValue = value;

        unsafe
        {
            Yoga.YG.NodeMarkDirty(Node);
        }

        if (ValueChanged.HasDelegate)
        {
            _ = InvokeEventCallbackAsync(ValueChanged, _currentValue);
        }

        if (OnInput.HasDelegate)
        {
            _ = InvokeEventCallbackAsync(OnInput, _currentValue);
        }

        StateHasChanged();
    }

    public void Focus()
    {
        EnsureSubscriptions();
        InteractionState.SetActiveElement(this);
        ShowCaretNow();
        _ = InvokeAsync(StateHasChanged);
    }

    public void Blur()
    {
        EnsureSubscriptions();

        if (ReferenceEquals(InteractionState.ActiveElement, this))
        {
            InteractionState.SetActiveElement(null);
        }

        _ = InvokeAsync(StateHasChanged);
    }

    protected override bool IsFocusable => true;

    public void Dispose()
    {
        if (!_subscriptionsInitialized)
        {
            return;
        }

        InteractionState.ActiveElementChanged -= OnActiveElementChanged;
        AnimationTicker.Tick -= OnAnimationTick;
        _subscriptionsInitialized = false;
    }
}
