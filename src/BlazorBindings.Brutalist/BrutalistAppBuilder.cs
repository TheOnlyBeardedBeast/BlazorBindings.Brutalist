using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.Routing;
using System.Threading;

namespace BlazorBindings.Brutalist;

public class BrutalistAppBuilder
{
    public IServiceCollection Services { get; } = new ServiceCollection();
    public IServiceProvider ServiceProvider { get; private set; }

    public BrutalistAppBuilder AddCoreServices()
    {
        Console.WriteLine("[BrutalistAppBuilder] Adding core services...");
        Console.Out.Flush();

        // Create OpentkService which will create the GPU-backed surface
        var opentkService = new OpentkService();
        Console.WriteLine("[BrutalistAppBuilder] OpentkService created");
        Console.Out.Flush();

        Services.AddSingleton(opentkService);
        Services.AddSingleton<IBrutalistRenderSurface>(opentkService);

        AddSharedCoreServices();

        Console.WriteLine("[BrutalistAppBuilder] Core services added");
        Console.Out.Flush();
        return this;
    }

    public BrutalistAppBuilder AddCoreServicesWithoutOpenTk(int width = 800, int height = 600, float dpiScale = 1f)
    {
        Console.WriteLine("[BrutalistAppBuilder] Adding headless core services...");
        Console.Out.Flush();

        var imageRenderService = new ImageRenderService(width, height, dpiScale);
        Services.AddSingleton(imageRenderService);
        Services.AddSingleton<IBrutalistRenderSurface>(imageRenderService);

        AddSharedCoreServices();

        Console.WriteLine("[BrutalistAppBuilder] Headless core services added");
        Console.Out.Flush();
        return this;
    }

    private void AddSharedCoreServices()
    {
        Services.AddSingleton<SynchronizationContext>(sp =>
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            return SynchronizationContext.Current;
        });
        Services.AddLogging(builder => builder.AddConsole());
        Services.AddSingleton<NavigationManager, BrutalistNavigationManager>();
        Services.AddSingleton<INavigationInterception, BrutalistNavigationInterception>();
        Services.AddSingleton<IScrollToLocationHash, BrutalistScrollToLocationHash>();
        Services.AddSingleton<InteractionState>();
        Services.AddSingleton<AnimationTicker>();
        Services.AddSingleton<AnimationScheduler>();
        // Services.AddSingleton<BrutalistBlazorBindingsRenderer>();
        Services.AddSingleton<YogaSkiaRenderer>();
    }

    public BrutalistAppBuilder AddComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : IComponent
    {
        Services.AddSingleton(typeof(T));
        return this;
    }

    public IServiceProvider Build()
    {
        ServiceProvider = Services.BuildServiceProvider();
        return ServiceProvider;
    }
}

