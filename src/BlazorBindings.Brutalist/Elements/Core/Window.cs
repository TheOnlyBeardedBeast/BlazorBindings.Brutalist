using Yoga;

namespace BlazorBindings.Brutalist.Elements;

public class YogaWindow : YogaView, IDisposable
{
    private bool _subscribed;
    private int? _lastRequestedWidth;
    private int? _lastRequestedHeight;

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters);

        EnsureSubscribed();
        ApplyRequestedWindowSize();
        ApplyWindowSize();
    }

    private void EnsureSubscribed()
    {
        if (_subscribed)
        {
            return;
        }

        OpenTkService.SurfaceResized += OnSurfaceResized;
        _subscribed = true;
    }

    private void OnSurfaceResized()
    {
        _ = InvokeAsync(() =>
        {
            ApplyWindowSize();
            StateHasChanged();
        });
    }

    private void ApplyRequestedWindowSize()
    {
        // YogaWindow uses inherited Width/Height parameters as desired app window size.
        // If provided and changed, request a surface resize.
        if (!Width.HasValue && !Height.HasValue)
        {
            return;
        }

        var requestedWidth = Math.Max(1, (int)MathF.Round(Width ?? OpenTkService.Width));
        var requestedHeight = Math.Max(1, (int)MathF.Round(Height ?? OpenTkService.Height));

        if (_lastRequestedWidth == requestedWidth && _lastRequestedHeight == requestedHeight)
        {
            return;
        }

        _lastRequestedWidth = requestedWidth;
        _lastRequestedHeight = requestedHeight;
        OpenTkService.ResizeSurface(requestedWidth, requestedHeight);
    }

    private unsafe void ApplyWindowSize()
    {
        YG.NodeStyleSetWidth(Node, OpenTkService.Width);
        YG.NodeStyleSetHeight(Node, OpenTkService.Height);
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            OpenTkService.SurfaceResized -= OnSurfaceResized;
            _subscribed = false;
        }
    }
}
