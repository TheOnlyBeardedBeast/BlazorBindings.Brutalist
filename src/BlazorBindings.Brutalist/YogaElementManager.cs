using BlazorBindings.Brutalist.Elements;
using System;

namespace BlazorBindings.Brutalist;

/// <summary>
/// Utilities needed by the system to manage native controls. Implementations
/// of native rendering systems have their own quirks in terms of dealing with
/// parent/child relationships, so each must implement this given the constraints
/// and requirements of their systems.
/// </summary>
public class YogaElementManager
{
    public virtual void AddChildElement(
        IComponent parentHandler,
        IComponent childHandler,
        int physicalSiblingIndex)
    {
        if (childHandler is not Element || parentHandler is not Element)
        {
            return;
        }

        // if (parentHandler is not IContainerElementHandler parent)
        // {
        //     throw new NotSupportedException($"Handler of type '{parentHandler.GetType().FullName}' representing element type " +
        //         $"'{parentHandler?.GetType().FullName ?? "<null>"}' doesn't support adding a child " +
        //         $"(child type is '{childHandler?.GetType().FullName}').");
        // }

        (parentHandler as Element).AddChild(childHandler, physicalSiblingIndex);
    }

    public virtual void RemoveChildElement(IComponent parentHandler, IComponent childHandler, int physicalSiblingIndex)
    {
        if (childHandler is INonPhysicalChild nonPhysicalChild)
        {
            nonPhysicalChild.RemoveFromParent(parentHandler);
        }
        else if (parentHandler is IContainerElementHandler parent)
        {
            parent.RemoveChild(childHandler, physicalSiblingIndex);
        }
        else
        {
            throw new NotSupportedException($"Handler of type '{parentHandler.GetType().FullName}' representing element type " +
                $"'{parentHandler?.GetType().FullName ?? "<null>"}' doesn't support removing a child " +
                $"(child type is '{childHandler?.GetType().FullName}').");
        }
    }

    public virtual void ReplaceChildElement(IComponent parentHandler, IComponent oldChild, IComponent newChild, int physicalSiblingIndex)
    {
        if (oldChild is INonPhysicalChild || newChild is INonPhysicalChild)
            throw new NotSupportedException("NonPhysicalChild elements cannot be replaced.");

        if (parentHandler is not IContainerElementHandler container)
            throw new InvalidOperationException($"Handler of type '{parentHandler.GetType().FullName}' does not support replacing child elements.");

        container.ReplaceChild(physicalSiblingIndex, oldChild, newChild);
    }
}

