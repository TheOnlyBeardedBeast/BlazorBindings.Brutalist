using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Brutalis.CustomComponentsSample;

public abstract class NavigationAwareComponentBase : ComponentBase, IDisposable
{
    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += HandleLocationChanged;
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        OnLocationChanged(args);
        _ = InvokeAsync(StateHasChanged);
    }

    protected virtual void OnLocationChanged(LocationChangedEventArgs args)
    {
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= HandleLocationChanged;
    }
}