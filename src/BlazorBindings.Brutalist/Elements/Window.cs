using Yoga;

namespace BlazorBindings.Brutalist.Elements;

public class YogaWindow : YogaView, IDisposable
{
    private bool _subscribed;

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters);

        EnsureSubscribed();
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
