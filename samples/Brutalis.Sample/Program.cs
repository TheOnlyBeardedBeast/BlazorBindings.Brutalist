using BlazorBindings.Brutalist;
using BlazorBindings.Brutalist.Elements;
using BlazorBindings.Brutalist.Elements.Handlers;
using BlazorBindings.Core;
using BrutalisSample;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using Yoga;



var builder = new BrutalistAppBuilder();

builder.AddCoreServices();

var services = builder.Build();
var renderer = services.GetRequiredService<YogaSkiaRenderer>();
var opentkService = services.GetRequiredService<OpentkService>();

Console.WriteLine("[Program] Adding root component...");
Console.Out.Flush();

// Add the root component and wait for it to be processed
await renderer.Dispatcher.InvokeAsync(async () =>
{
    Console.WriteLine("[Program.Dispatcher] Inside dispatcher");
    Console.Out.Flush();
    await renderer.AddRootComponent<App>();
    Console.WriteLine("[Program.Dispatcher] Component added");
    Console.Out.Flush();
});

Console.WriteLine("[Program] Component added, starting renderer...");
Console.Out.Flush();

// NOW start the OpenTK window after component is added
opentkService.Start();

Console.WriteLine("[Program] OpentkService started, keeping app alive...");
Console.Out.Flush();

// Keep the app running - on macOS this will block, on other platforms the window thread handles it
if (OperatingSystem.IsMacOS())
{
    // On macOS, the window already runs on main thread and blocks
    Console.WriteLine("[Program] Running on macOS - window is blocking");
}
else
{
    // On other platforms, wait for user input to keep the app alive
    Console.WriteLine("[Program] Waiting for input to keep alive...");
    Console.Out.Flush();
    Console.ReadLine();
}
// // await renderer.AddRootComponent<App>(new Dictionary<string, object>());
// while (true)
// {
//     await Task.Delay(16); // ~60 FPS
//     Console.WriteLine("#######Render");
//     (handler.Elements[0] as Element).Render();
//     // Manually trigger any rendering or UI logic here
//     // e.g. renderer.RenderFrame(); or trigger layout updates
// }


// unsafe
// {
//     YGNode* root = YG.NodeNew();
//     YG.NodeStyleSetWidth(root, 800);
//     YG.NodeStyleSetHeight(root, 600);
//     // YG.NodeStyleSetPadding(root, YGEdge.YGEdgeAll, 10);
//     YG.NodeStyleSetFlexDirection(root, YGFlexDirection.YGFlexDirectionColumn);
//     // YG.NodeStyleSetGap(root, YGGutter.YGGutterAll, 10);

//     YGNode* row = YG.NodeNew();
//     YG.NodeInsertChild(root, row, 0);
//     YG.NodeStyleSetWidth(row, 800);
//     YG.NodeStyleSetHeight(row, 100);
//     YG.NodeStyleSetFlexDirection(row, YGFlexDirection.YGFlexDirectionRow);

//     YGNode* column = YG.NodeNew();
//     YG.NodeInsertChild(root, column, 1);
//     YG.NodeStyleSetWidth(column, 800);
//     YG.NodeStyleSetHeight(column, 500);
//     YG.NodeStyleSetFlexDirection(row, YGFlexDirection.YGFlexDirectionColumn);

//     YGNode* columnChild1 = YG.NodeNew();
//     YG.NodeStyleSetWidth(columnChild1, 100);
//     YG.NodeStyleSetHeight(columnChild1, 100);
//     YG.NodeInsertChild(column, columnChild1, 0);

//     YG.NodeCalculateLayout(root, YG.YGUndefined, YG.YGUndefined, YGDirection.YGDirectionLTR);

//     Console.WriteLine($"Left={YG.NodeLayoutGetLeft(root)},Top={YG.NodeLayoutGetTop(root)},Width={YG.NodeLayoutGetWidth(root)},Height={YG.NodeLayoutGetHeight(root)}");
//     for (nuint i = 0; i < YG.NodeGetChildCount(root); i++)
//     {
//         YGNode* node = YG.NodeGetChild(root, i);
//         Console.WriteLine($"Left={YG.NodeLayoutGetLeft(node)},Top={YG.NodeLayoutGetTop(node)},Width={YG.NodeLayoutGetWidth(node)},Height={YG.NodeLayoutGetHeight(node)}");
//     }

//     for (nuint i = 0; i < YG.NodeGetChildCount(column); i++)
//     {
//         YGNode* node = YG.NodeGetChild(column, i);
//         Console.WriteLine($"Left={YG.NodeLayoutGetLeft(node)},Top={YG.NodeLayoutGetTop(node)},Width={YG.NodeLayoutGetWidth(node)},Height={YG.NodeLayoutGetHeight(node)}");
//     }

//     YG.NodeFreeRecursive(root);
// }
