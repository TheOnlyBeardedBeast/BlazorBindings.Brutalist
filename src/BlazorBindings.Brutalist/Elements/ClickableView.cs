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

    [Inject]
    protected InteractionState InteractionState { get; set; } = default!;

    private bool _subscriptionsInitialized;
    private bool _isFocused;

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters);
        EnsureSubscriptions();
    }

    protected override bool IsInteractive => OnClickAction is not null || OnClick.HasDelegate;

    protected override bool HandleClick(SKPoint point)
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
        _subscriptionsInitialized = false;
    }
}
