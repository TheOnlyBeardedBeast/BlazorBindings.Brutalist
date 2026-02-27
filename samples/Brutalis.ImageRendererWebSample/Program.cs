using BlazorBindings.Brutalist;
using Brutalis.ImageRendererWebSample;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<PageImageRenderer>();

var app = builder.Build();

app.MapGet("/health", () => Results.Text("ok"));

app.MapGet("/{**path}", async (HttpContext context, PageImageRenderer renderer, CancellationToken cancellationToken) =>
{
    var routePath = "/" + (context.Request.RouteValues["path"] as string ?? string.Empty);
    if (routePath == "/")
    {
        routePath = "/home";
    }

    if (context.Request.QueryString.HasValue)
    {
        routePath += context.Request.QueryString.Value;
    }

    var width = ParseSize(context.Request.Query["w"], 1200);
    var height = ParseSize(context.Request.Query["h"], 630);

    var png = await renderer.RenderAsync(routePath, width, height, cancellationToken);
    return Results.File(png, "image/png");
});

app.Run();

static int ParseSize(string? raw, int fallback)
{
    if (!int.TryParse(raw, out var value))
    {
        return fallback;
    }

    return Math.Clamp(value, 100, 4000);
}

sealed class PageImageRenderer
{
    public async Task<byte[]> RenderAsync(string routePath, int width, int height, CancellationToken cancellationToken)
    {
        var appBuilder = new BrutalistAppBuilder();
        appBuilder.AddCoreServicesWithoutOpenTk(width: width, height: height, dpiScale: 1f);

        var serviceProvider = appBuilder.Build();
        using var scopeLifetime = serviceProvider as IDisposable;

        var renderer = serviceProvider.GetRequiredService<YogaSkiaRenderer>();
        var navigation = serviceProvider.GetRequiredService<NavigationManager>();

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            navigation.NavigateTo(NormalizeRoute(routePath));
            await renderer.AddRootComponent<App>();
            await Task.Yield();
        });

        cancellationToken.ThrowIfCancellationRequested();

        return await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RenderToImage(SKEncodedImageFormat.Png, quality: 90));
    }

    private static string NormalizeRoute(string routePath)
    {
        if (string.IsNullOrWhiteSpace(routePath))
        {
            return "/home";
        }

        return routePath.StartsWith('/') ? routePath : "/" + routePath;
    }
}
