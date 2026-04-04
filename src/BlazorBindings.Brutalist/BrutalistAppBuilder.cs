using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.Routing;
using System.Threading;

namespace BlazorBindings.Brutalist;

public class BrutalistAppBuilder
{
    public IServiceCollection Services { get; } = new ServiceCollection();
    public IServiceProvider ServiceProvider { get; private set; } = default!;

    public BrutalistAppBuilder AddCoreServices()
    {
        Console.Out.Flush();

        // Create Sdl3Service which will create the GPU-backed surface
        var sdl3Service = new Sdl3Service();
        Console.Out.Flush();

        Services.AddSingleton(sdl3Service);
        Services.AddSingleton<IBrutalistRenderSurface>(sdl3Service);

        AddSharedCoreServices();

        Console.Out.Flush();
        return this;
    }

    public BrutalistAppBuilder AddCoreServicesWithoutOpenTk(int width = 800, int height = 600, float dpiScale = 1f)
    {
        Console.Out.Flush();

        var imageRenderService = new ImageRenderService(width, height, dpiScale);
        Services.AddSingleton(imageRenderService);
        Services.AddSingleton<IBrutalistRenderSurface>(imageRenderService);

        AddSharedCoreServices();

        Console.Out.Flush();
        return this;
    }

    public BrutalistAppBuilder AddCoreServicesWithSilk()
    {
        Console.Out.Flush();

        var silkService = new SilkService();
        Console.Out.Flush();

        Services.AddSingleton(silkService);
        Services.AddSingleton<IBrutalistRenderSurface>(silkService);

        AddSharedCoreServices();

        Console.Out.Flush();
        return this;
    }

    private void AddSharedCoreServices()
    {
        Services.AddSingleton<SynchronizationContext>(sp =>
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            return SynchronizationContext.Current ?? new SynchronizationContext();
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

