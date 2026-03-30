using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;

namespace BlazorBindings.Brutalist.Elements;

public class YogaClickableView : YogaView, IDisposable
{
    [Parameter]
    public EventCallback OnClick { get; set; }

    [Parameter]
    public Action? OnClickAction { get; set; }

    [Parameter]
    public EventCallback OnFocus { get; set; }

    [Parameter]
    public EventCallback OnBlur { get; set; }

    [Parameter]
    public EventCallback<PointerEventArgs> OnPointerDown { get; set; }

    [Parameter]
    public EventCallback<PointerEventArgs> OnPointerMove { get; set; }

    [Parameter]
    public EventCallback<PointerEventArgs> OnPointerUp { get; set; }

    [Parameter]
    public bool Disabled { get; set; } = false;

    [Inject]
    protected InteractionState InteractionState { get; set; } = default!;

    private bool _subscriptionsInitialized;
    private bool _isFocused;

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters);

        // Prevent style cascading when disabled
        if (Disabled)
        {
            StateHasChanged();
        }

        EnsureSubscriptions();
    }

    protected override bool IsInteractive => !Disabled
        && (OnClickAction is not null
            || OnClick.HasDelegate
            || OnPointerDown.HasDelegate
            || OnPointerMove.HasDelegate
            || OnPointerUp.HasDelegate);

    protected override bool IsFocusable => !Disabled && IsInteractive;

    protected override bool HandleClick(SKPoint point)
    {
        if (Disabled)
        {
            return false;
        }

        return InvokeClickHandlers();
    }

    protected override bool HandleKeyDown(Keys key)
    {
        if (Disabled)
        {
            return false;
        }

        if (key != Keys.Enter && key != Keys.KeyPadEnter)
        {
            return false;
        }

        return InvokeClickHandlers();
    }

    private bool InvokeClickHandlers()
    {
        var handled = false;

        if (OnClickAction is not null)
        {
            OnClickAction();
            handled = true;
        }

        if (OnClick.HasDelegate)
        {
            InvokeEventCallback(OnClick);
            handled = true;
        }

        return handled;
    }

    protected override bool HandlePointerDown(SKPoint point)
    {
        if (Disabled)
        {
            return false;
        }

        InteractionState.SetPointerCapture(this);

        if (!OnPointerDown.HasDelegate)
        {
            return false;
        }

        InvokeEventCallback(OnPointerDown, BuildPointerEventArgs(point));
        return true;
    }

    protected override bool HandlePointerMove(SKPoint point)
    {
        if (Disabled || !OnPointerMove.HasDelegate)
        {
            return false;
        }

        InvokeEventCallback(OnPointerMove, BuildPointerEventArgs(point));
        return true;
    }

    protected override bool HandlePointerUp(SKPoint point)
    {
        if (Disabled)
        {
            return false;
        }

        if (ReferenceEquals(InteractionState.PointerCaptureElement, this))
        {
            InteractionState.SetPointerCapture(null);
        }

        if (!OnPointerUp.HasDelegate)
        {
            return false;
        }

        InvokeEventCallback(OnPointerUp, BuildPointerEventArgs(point));
        return true;
    }

    private PointerEventArgs BuildPointerEventArgs(SKPoint point)
    {
        var elementLeft = rect.Left;
        var elementTop = rect.Top;
        var elementWidth = rect.Width;
        var elementHeight = rect.Height;

        return new PointerEventArgs
        {
            Element = this,
            ElementLeft = elementLeft,
            ElementTop = elementTop,
            ElementWidth = elementWidth,
            ElementHeight = elementHeight,
            PointerX = point.X,
            PointerY = point.Y,
            PointerLocalX = point.X - elementLeft,
            PointerLocalY = point.Y - elementTop,
        };
    }

    private void EnsureSubscriptions()
    {
        if (_subscriptionsInitialized)
        {
            return;
        }

        InteractionState.ActiveElementChanged += OnActiveElementChanged;
        _subscriptionsInitialized = true;
        _isFocused = ReferenceEquals(InteractionState.ActiveElement, this);
    }

    private void OnActiveElementChanged(Element? element)
    {
        if (Disabled)
        {
            return;
        }

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
                InvokeEventCallback(OnFocus);
            }
        }
        else
        {
            if (OnBlur.HasDelegate)
            {
                InvokeEventCallback(OnBlur);
            }
        }
    }

    public void Dispose()
    {
        if (!_subscriptionsInitialized)
        {
            return;
        }

        InteractionState.ActiveElementChanged -= OnActiveElementChanged;

        if (ReferenceEquals(InteractionState.PointerCaptureElement, this))
        {
            InteractionState.SetPointerCapture(null);
        }

        _subscriptionsInitialized = false;

    }
}
