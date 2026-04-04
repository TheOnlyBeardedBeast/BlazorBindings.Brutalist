# Brutalist

Brutalist is a Blazor-style native UI runtime focused on flexible component composition, high-performance rendering, and modern cross-platform app hosting.

It keeps the Razor authoring model you already know, while targeting a custom rendering and layout stack instead of HTML.

## Core technologies

- C# and Razor components for declarative UI
- Blazor component model for composition and state updates
- Yoga for layout engine and flex behavior
- Skia for drawing and rendering
- Slint for runtime hosting and window integration
- Material-inspired component layer built in Brutalist packages

## Quick start tutorial

### 1. Prerequisites

- .NET SDK 9.0+
- macOS, Windows, or Linux development environment

### 2. Clone and open

```bash
git clone https://github.com/dreamescaper/BlazorBindings.Maui.git
cd BlazorBindings.Maui
```

### 3. Run the custom components sample

```bash
cd samples/Brutalist.CustomComponentsSample
dotnet run
```

### 4. Explore the sample pages

Open and edit pages under:

- samples/Brutalist.CustomComponentsSample/Content

For example:

- Home.razor
- Buttons.razor
- Sliders.razor
- DragDrop.razor

## Usage examples

### Example: simple page with Material text and button

```razor
@page "/hello"
@using MaterialColorUtilities.Schemes

<YogaView Direction="Yoga.YGFlexDirection.YGFlexDirectionColumn" Gap="12" Padding="24">
        <MaterialText FontSize="MaterialFontSize.HeadlineMedium" TextAlign="left">
                Hello brutalist
        </MaterialText>

        <MaterialText FontSize="MaterialFontSize.BodyMedium" TextAlign="left" WrapText>
                This UI is authored with Razor and rendered through Yoga + Skia + Slint.
        </MaterialText>

        <Button Label="Click me" OnClick="HandleClick" />
        <MaterialText FontSize="MaterialFontSize.BodySmall" TextAlign="left">Count: @count</MaterialText>
</YogaView>

@code {
        private int count;

        private void HandleClick()
        {
                count++;
        }
}
```

### Example: app bootstrap

```csharp
using BlazorBindings.Brutalist;
using Microsoft.Extensions.DependencyInjection;

var builder = new BrutalistAppBuilder();
builder.AddCoreServicesWithSilk();

var services = builder.Build();
var renderer = services.GetRequiredService<YogaSkiaRenderer>();

await renderer.Dispatcher.InvokeAsync(async () =>
{
        await renderer.AddRootComponent<App>();
});
```

Note: the current repository API uses AddCoreServicesWithSilk in sample startup code.

## Project structure

```text
BlazorBindings.Maui.sln
src/
    BlazorBindings.Brutalist/
        Core rendering runtime, Yoga integration, app builder, hosting services
    BlazorBindings.Brutalist.Material/
        Material-inspired components, theme utilities, typography
    BlazorBindings.Brutalist.Phosphor/
        Icon pack integration
    BlazorBindings.Core/
        Shared infrastructure and abstractions
samples/
    Brutalist.CustomComponentsSample/
        Main showcase app for controls, theming, routing, interactions
    Brutalist.Sample/
        Additional sample app
    Brutalist.ImageRendererSample/
        Rendering to image examples
    Brutalist.ImageRendererWebSample/
        Web-hosted rendering example
```

## Typical workflow

1. Build or update components in src/BlazorBindings.Brutalist.Material
2. Validate behavior quickly in samples/Brutalist.CustomComponentsSample
3. Add focused examples in sample pages for new capabilities
4. Keep runtime and component packages aligned through the solution build

## Community

- NuGet package: https://www.nuget.org/packages/BlazorBindings.Maui/
- Discord: https://discord.com/channels/732297728826277939/734865521400610856

## Code of Conduct

This project follows the Contributor Covenant and .NET Foundation code of conduct:

https://dotnetfoundation.org/code-of-conduct
