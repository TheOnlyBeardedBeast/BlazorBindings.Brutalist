// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using BlazorBindings.Brutalist.Elements;
using BlazorBindings.Brutalist.Elements.Handlers;
using Microsoft.Extensions.Logging;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace BlazorBindings.Brutalist;

public class BrutalistBlazorBindingsRenderer : NativeComponentRenderer
{
    public BrutalistBlazorBindingsRenderer(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, SynchronizationContext syncContext)
        : base(serviceProvider, loggerFactory)
    {
        Dispatcher = Dispatcher.CreateDefault();
    }

    public override Dispatcher Dispatcher { get; }

    protected override void HandleException(Exception exception)
    {
        ExceptionDispatchInfo.Throw(exception);
    }

    // It tries to return the Element as soon as it is available, therefore Component task might still be in progress.
    internal async Task<(object Element, Task<IComponent> Component)> GetElementFromRenderedComponent(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type componentType,
        Dictionary<string, object> parameters = null)
    {
        var (elements, addComponentTask) = await GetElementsFromRenderedComponent(componentType, parameters);

        if (elements.Count != 1)
        {
            throw new InvalidOperationException("The target component must have exactly one root element.");
        }

        return (elements[0], addComponentTask);
    }

    public async Task<(T Component, int ComponentId)> AddRootComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        Dictionary<string, object> initialParameters)
        where T : IComponent
    {
        // var container = new RootContainerHandler();
        var component = (T)InstantiateComponent(typeof(T));
        // var component = (T)await AddComponent(typeof(T), container, initialParameters);
        var componentId = AssignRootComponentId(component);
        await RenderRootComponentAsync(componentId, ParameterView.FromDictionary(initialParameters));
        return (component, 0);
    }

    private async Task<(List<object> Elements, Task<IComponent> Component)> GetElementsFromRenderedComponent(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type componentType,
        Dictionary<string, object> parameters)
    {
        var container = new RootContainerHandler();

        var addComponentTask = AddComponent(componentType, container, parameters);
        var elementAddedTask = container.WaitForElementAsync();

        await Task.WhenAny(addComponentTask, elementAddedTask);

        if (addComponentTask.Exception != null)
        {
            var exception = addComponentTask.Exception.InnerException;
            ExceptionDispatchInfo.Throw(exception);
        }

        return (container.Elements, addComponentTask);
    }
}
