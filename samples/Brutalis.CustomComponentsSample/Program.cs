using BlazorBindings.Brutalist;
using Brutalis.CustomComponentsSample;
using Microsoft.Extensions.DependencyInjection;

var builder = new BrutalistAppBuilder();
builder.AddCoreServicesWithSilk();

var services = builder.Build();
var renderer = services.GetRequiredService<YogaSkiaRenderer>();
var silkService = services.GetRequiredService<SilkService>();

await renderer.Dispatcher.InvokeAsync(async () =>
{
    await renderer.AddRootComponent<App>();
});

silkService.Start();

if (!OperatingSystem.IsMacOS())
{
    Console.ReadLine();
}
