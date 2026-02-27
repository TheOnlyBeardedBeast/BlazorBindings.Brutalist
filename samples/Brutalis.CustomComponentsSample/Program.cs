using BlazorBindings.Brutalist;
using Brutalis.CustomComponentsSample;
using Microsoft.Extensions.DependencyInjection;

var builder = new BrutalistAppBuilder();
builder.AddCoreServices();

var services = builder.Build();
var renderer = services.GetRequiredService<YogaSkiaRenderer>();
var opentkService = services.GetRequiredService<OpentkService>();

await renderer.Dispatcher.InvokeAsync(async () =>
{
    await renderer.AddRootComponent<App>();
});

opentkService.Start();

if (!OperatingSystem.IsMacOS())
{
    Console.ReadLine();
}
