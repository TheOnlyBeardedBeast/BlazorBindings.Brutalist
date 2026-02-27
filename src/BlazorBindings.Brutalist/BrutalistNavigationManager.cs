using System;

namespace BlazorBindings.Brutalist;

public class BrutalistNavigationManager : NavigationManager
{
    public BrutalistNavigationManager()
    {
        Initialize("app:///", "app:///");
    }

    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
        Uri = ToAbsoluteUri(uri).AbsoluteUri;
        NotifyLocationChanged(false);
    }
}
