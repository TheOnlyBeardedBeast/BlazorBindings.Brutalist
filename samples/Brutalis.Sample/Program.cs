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

Console.Out.Flush();

// Add the root component and wait for it to be processed
await renderer.Dispatcher.InvokeAsync(async () =>
{
    Console.Out.Flush();
    await renderer.AddRootComponent<App>();
    Console.Out.Flush();
});

Console.Out.Flush();

// NOW start the OpenTK window after component is added
opentkService.Start();

Console.Out.Flush();

// Keep the app running - on macOS this will block, on other platforms the window thread handles it
if (OperatingSystem.IsMacOS())
{
    // On macOS, the window already runs on main thread and blocks
}
else
{
    // On other platforms, wait for user input to keep the app alive
    Console.Out.Flush();
    Console.ReadLine();
}
// // await renderer.AddRootComponent<App>(new Dictionary<string, object>());
// while (true)
// {
//     await Task.Delay(16); // ~60 FPS
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

//     for (nuint i = 0; i < YG.NodeGetChildCount(root); i++)
//     {
//         YGNode* node = YG.NodeGetChild(root, i);
//     }

//     for (nuint i = 0; i < YG.NodeGetChildCount(column); i++)
//     {
//         YGNode* node = YG.NodeGetChild(column, i);
//     }

//     YG.NodeFreeRecursive(root);
// }
