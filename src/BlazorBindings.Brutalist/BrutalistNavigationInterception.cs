using Microsoft.AspNetCore.Components.Routing;

namespace BlazorBindings.Brutalist;

internal class BrutalistNavigationInterception : INavigationInterception
{
    public Task EnableNavigationInterceptionAsync() => Task.CompletedTask;
}
