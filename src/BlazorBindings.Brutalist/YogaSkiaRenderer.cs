using BlazorBindings.Brutalist.Elements;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;
using System.Runtime.ExceptionServices;

namespace BlazorBindings.Brutalist;

public class YogaSkiaRenderer : Renderer
{
    public readonly YogaElementManager _elementManager = new YogaElementManager();
    private readonly List<int> _rootComponentIds = new();
    private readonly Dictionary<int, YogaComponentAdapter> _componentIdToAdapter = [];

    private readonly IBrutalistRenderSurface _renderSurface;
    private readonly InteractionState _interactionState;

    public YogaSkiaRenderer(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, IBrutalistRenderSurface renderSurface, InteractionState interactionState)
        : base(serviceProvider, loggerFactory)
    {
        _renderSurface = renderSurface;
        _interactionState = interactionState;
        _renderSurface.SurfaceResized += OnSurfaceResized;
        _renderSurface.MouseClicked += OnMouseClicked;
        _renderSurface.MouseMoved += OnMouseMoved;
        _renderSurface.MouseWheelScrolled += OnMouseWheelScrolled;
        _renderSurface.TextInputReceived += OnTextInputReceived;
        _renderSurface.KeyDownReceived += OnKeyDownReceived;
    }

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        HashSet<YogaComponentAdapter> adaptersWithPendingEdits = [];
        var numUpdatedComponents = renderBatch.UpdatedComponents.Count;
        for (var componentIndex = 0; componentIndex < numUpdatedComponents; componentIndex++)
        {
            var updatedComponent = renderBatch.UpdatedComponents.Array[componentIndex];

            if (updatedComponent.Edits.Count > 0)
            {
                var adapter = _componentIdToAdapter[updatedComponent.ComponentId];
                adapter.ApplyEdits(updatedComponent.ComponentId, updatedComponent.Edits, renderBatch, adaptersWithPendingEdits);
            }
        }
        // Console.WriteLine(adaptersWithPendingEdits.Count);
        foreach (var adapter in adaptersWithPendingEdits.OrderByDescending(a => a.DeepLevel))
            adapter.ApplyPendingEdits();

        RenderCurrentFrame();

        var numDisposedComponents = renderBatch.DisposedComponentIDs.Count;
        for (var i = 0; i < numDisposedComponents; i++)
        {
            var disposedComponentId = renderBatch.DisposedComponentIDs.Array[i];
            if (_componentIdToAdapter.Remove(disposedComponentId, out var adapter))
            {
                (adapter as IDisposable)?.Dispose();
            }
        }

        return Task.CompletedTask;
    }

    public async Task<TComponent> AddRootComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(Dictionary<string, object> parameters = null)
        where TComponent : IComponent
    {
        var component = CreateComponent<TComponent>(parameters ?? new());
        await RenderRootComponentAsync(_rootComponentIds.Last(), ParameterView.Empty);
        return component;
    }

    private TComponent CreateComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TComponent>(Dictionary<string, object> parameters)
        where TComponent : IComponent
    {
        var component = InstantiateComponent(typeof(TComponent));
        var componentId = AssignRootComponentId(component);
        _rootComponentIds.Add(componentId);

        var rootAdapter = new YogaComponentAdapter(this, null, knownTargetElement: null);
        _componentIdToAdapter.Add(componentId, rootAdapter);

        // Setup a root container element to host your visual tree
        // e.g. Element root = new YogaView(); _elementManager.RegisterRoot(root);
        return (TComponent)component;
    }

    internal void RegisterComponentAdapter(YogaComponentAdapter adapter, int componentId)
    {
        _componentIdToAdapter[componentId] = adapter;
    }

    public void SaveSurfaceToFile(string filePath, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 80)
    {
        _renderSurface.SaveSurfaceToFile(filePath);
    }

    public byte[] RenderToImage(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 90)
    {
        RenderCurrentFrame();
        using var image = _renderSurface.Surface.Snapshot();
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    private void OnSurfaceResized()
    {
        _ = Dispatcher.InvokeAsync(RenderCurrentFrame);
    }

    private void OnMouseClicked(SKPoint point)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            var root = GetRootElement();
            var activeElement = root?.ResolveActiveElement(point);
            _interactionState.SetActiveElement(activeElement);
            root?.DispatchClick(point);
        });
    }

    private void OnMouseMoved(SKPoint point)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            var root = GetRootElement();
            var shouldUsePointer = false;
            if (root is not null && root.TryResolveCursor(point, out var isPointer))
            {
                shouldUsePointer = isPointer;
            }

            _renderSurface.SetPointerCursor(shouldUsePointer);
        });
    }

    private void OnTextInputReceived(string text)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            _interactionState.ActiveElement?.DispatchTextInput(text);
            RenderCurrentFrame();
        });
    }

    private void OnMouseWheelScrolled(SKPoint point, float deltaY)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            var root = GetRootElement();
            if (root?.DispatchScroll(point, deltaY) == true)
            {
                RenderCurrentFrame();
            }
        });
    }

    private void OnKeyDownReceived(Keys key)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            _interactionState.ActiveElement?.DispatchKeyDown(key);
            RenderCurrentFrame();
        });
    }

    private void RenderCurrentFrame()
    {
        if (_rootComponentIds.Count == 0)
        {
            return;
        }

        var rootElement = GetRootElement();
        if (rootElement is null)
        {
            return;
        }

        _renderSurface.LockSurface(canvas =>
        {
            canvas.Clear(SKColors.White);
            rootElement.Render();
        });
    }

    private Element? GetRootElement()
    {
        if (_rootComponentIds.Count == 0)
        {
            return null;
        }

        var rootComponentId = _rootComponentIds[0];
        if (!_componentIdToAdapter.TryGetValue(rootComponentId, out var rootAdapter))
        {
            return null;
        }

        return FindFirstElement(rootAdapter);
    }

    private static Element? FindFirstElement(YogaComponentAdapter adapter)
    {
        if (adapter._targetElement is Element element)
        {
            return element;
        }

        foreach (var child in adapter.Children)
        {
            if (child is null)
            {
                continue;
            }

            var candidate = FindFirstElement(child);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    protected override void HandleException(Exception exception)
    {
        ExceptionDispatchInfo.Capture(exception).Throw();
    }
}

