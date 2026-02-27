using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;
using System.Globalization;

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

    [Inject]
    protected InteractionState InteractionState { get; set; } = default!;

    [Inject]
    protected AnimationTicker AnimationTicker { get; set; } = default!;

    private string _currentValue = string.Empty;
    private bool _subscriptionsInitialized;
    private bool _isFocused;
    private bool _caretVisible = true;
    private float _caretBlinkElapsed;
    private const float CaretBlinkIntervalSeconds = 0.5f;

    private int _caretOffset = 0;

    public YogaTextInput()
    {
        unsafe
        {
            Yoga.YG.NodeStyleSetMinHeight(Node, 36f);
        }
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
    }

    private void RenderText(SKCanvas canvas, SKRect textBounds)
    {
        var isEmpty = string.IsNullOrEmpty(_currentValue);
        var textToDraw = isEmpty ? (Placeholder ?? string.Empty) : _currentValue;

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            Color = isEmpty
                ? (string.IsNullOrWhiteSpace(PlaceholderColor) ? SKColor.Parse("#999999") : SKColor.Parse(PlaceholderColor))
                : (string.IsNullOrWhiteSpace(Color) ? SKColors.Black : SKColor.Parse(Color)),
        };

        using var font = new SKFont
        {
            Size = FontSize ?? 16f,
        };

        var lineHeight = font.Metrics.Descent - font.Metrics.Ascent;
        var baseline = textBounds.Top + ((textBounds.Height - lineHeight) / 2f) - font.Metrics.Ascent;

        canvas.DrawText(textToDraw, textBounds.Left, baseline, SKTextAlign.Left, font, paint);
    }

    private void RenderCaret(SKCanvas canvas, SKRect textBounds)
    {
        if (!_isFocused || !_caretVisible)
        {
            return;
        }

        using var font = new SKFont
        {
            Size = FontSize ?? 16f,
        };

        var lineHeight = font.Metrics.Descent - font.Metrics.Ascent;
        var baseline = textBounds.Top + ((textBounds.Height - lineHeight) / 2f) - font.Metrics.Ascent;

        var _caretText = _currentValue.Substring(0, _currentValue.Length - _caretOffset);
        var textWidth = font.MeasureText(_caretText);
        var caretX = (textBounds.Left + textWidth + 1f);
        var caretTop = baseline + font.Metrics.Ascent;
        var caretBottom = baseline + font.Metrics.Descent;

        using var caretPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = false,
            Color = string.IsNullOrWhiteSpace(Color) ? SKColors.Black : SKColor.Parse(Color),
        };

        canvas.DrawLine(caretX, caretTop, caretX, caretBottom, caretPaint);
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
        if (key == Keys.Left)
        {
            if (_caretOffset < _currentValue.Length)
            {
                _caretOffset++;
                ShowCaretNow();
                return false;
            }
        }
        else if (key == Keys.Right)
        {
            if (_caretOffset > 0)
            {
                _caretOffset--;
                ShowCaretNow();
                return false;
            }
        }

        if (key != Keys.Backspace)
        {
            return false;
        }



        if (string.IsNullOrEmpty(_currentValue))
        {
            return true;
        }

        var info = new StringInfo(_currentValue);
        var textElements = info.LengthInTextElements;
        if (textElements <= 0)
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

    private void EnsureSubscriptions()
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

    private void OnActiveElementChanged(Element? element)
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
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private void OnAnimationTick(float deltaSeconds, double elapsedSeconds)
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

    private void ShowCaretNow()
    {
        _caretVisible = true;
        _caretBlinkElapsed = 0;
    }

    private void SetValue(string value)
    {
        Console.WriteLine($"Setting value: '{value}'");
        if (_currentValue == value)
        {
            return;
        }

        _currentValue = value;

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
