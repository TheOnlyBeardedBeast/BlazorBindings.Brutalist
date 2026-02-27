using Microsoft.AspNetCore.Components.Routing;

namespace BlazorBindings.Brutalist;

internal class BrutalistScrollToLocationHash : IScrollToLocationHash
{
    public Task RefreshScrollPositionForHash(string locationAbsolute) => Task.CompletedTask;
}
