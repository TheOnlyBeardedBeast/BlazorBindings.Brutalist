using Microsoft.AspNetCore.Components.Rendering;
using SkiaSharp;
// using Yoga;
using Yoga;

namespace BlazorBindings.Brutalist.Elements;

public class YogaView : Element, IContainerElementHandler, IHandleChildContentText
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    public object? TargetElement => null;

    private TaskCompletionSource<object>? _taskCompletionSource;

    private Dictionary<nuint, object> _pendingChildren = [];


    public unsafe Task WaitForElementAsync()
    {
        if (YG.NodeGetChildCount(Node) > 0)
        {
            return Task.CompletedTask;
        }

        _taskCompletionSource ??= new();
        return _taskCompletionSource.Task;
    }


    public unsafe override void AddChild(object child, int physicalSiblingIndex)
    {

        // Console.WriteLine((child as Element).Node != null);

        if (child != null && (child as Element).Node != null)
        {

            // Console.WriteLine("Insert {0} to {1}", (child as Element).Id, this.Id);
            // Console.WriteLine(physicalSiblingIndex);
            nuint placeToInsert = (nuint)physicalSiblingIndex <= YG.NodeGetChildCount(Node) ? (nuint)physicalSiblingIndex : YG.NodeGetChildCount(Node);
            YG.NodeInsertChild(this.Node, (child as Element).Node, placeToInsert);
            // Node.Insert(physicalSiblingIndex, (child as Element).Node);
            _taskCompletionSource?.TrySetResult(child);
        }
    }

    public override void RenderSkia()
    {
        // TODO: custom skia rendering

        base.RenderSkia();
    }

    protected override RenderFragment GetChildContent() => ChildContent;
    // protected unsafe override RenderFragment GetChildContent() => builder =>
    // {
    //     Console.WriteLine("GetChildContent");
    //     Console.WriteLine(ChildContent);
    //     int seq = 0;
    //     for (nuint i = 0; i < YG.NodeGetChildCount(Node); i++)
    //     {
    //         builder.AddContent(seq++, this);
    //     }
    // };

    public unsafe void RemoveChild(object child, int physicalSiblingIndex)
    {
        YG.NodeRemoveChild(Node, (child as Element).Node);
    }

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters);
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        base.BuildRenderTree(builder);
    }

    public void HandleText(int index, string text)
    {
        // Console.WriteLine("HandleText");
        // Console.Write(text);
    }

}