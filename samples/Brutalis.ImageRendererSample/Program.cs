using BlazorBindings.Brutalist;
using Brutalis.ImageRendererSample;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

var outputPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(Environment.CurrentDirectory, "render-output.png");

var builder = new BrutalistAppBuilder();
builder.AddCoreServicesWithoutOpenTk(width: 1200, height: 630, dpiScale: 1f);

var services = builder.Build();
var renderer = services.GetRequiredService<YogaSkiaRenderer>();

await renderer.Dispatcher.InvokeAsync(async () =>
{
    await renderer.AddRootComponent<App>();

    var imageBytes = renderer.RenderToImage(SKEncodedImageFormat.Png, quality: 90);
    var outputDirectory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    await File.WriteAllBytesAsync(outputPath, imageBytes);
});

Console.WriteLine($"Image rendered to: {outputPath}");
