using SkiaSharp;
using System.Runtime.InteropServices;
using Yoga;

namespace BlazorBindings.Brutalist.Elements;

public unsafe class YogaScrollableView : YogaView
{
    [Parameter]
    public float ScrollSpeed { get; set; } = 40f;

    [Parameter]
    public bool ShowScrollIndicator { get; set; } = true;

    [Parameter]
    public float ScrollIndicatorWidth { get; set; } = 4f;

    [Parameter]
    public string? ScrollIndicatorColor { get; set; }

    [Parameter]
    public string? ScrollIndicatorTrackColor { get; set; }

    private float _scrollY;
    private float _viewportHeight;
    private float _contentHeight;

    public YogaScrollableView()
    {
        Overflow ??= "hidden";
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        var task = base.SetParametersAsync(parameters);

        // Force clipping so content can scroll inside viewport.
        if (string.IsNullOrWhiteSpace(Overflow))
        {
            Overflow = "hidden";
        }

        return task;
    }

    public override void AddChild(object child, int physicalSiblingIndex)
    {
        base.AddChild(child, physicalSiblingIndex);

        if (child is Element element)
        {
            YG.NodeStyleSetFlexShrink(element.Node, 0f);
        }
    }

    public override bool DispatchClick(SKPoint point)
    {
        if (!ContainsPoint(point))
        {
            return false;
        }

        var contentPoint = ToContentPoint(point);

        for (nuint i = YG.NodeGetChildCount(Node); i > 0; i--)
        {
            var childNode = YG.NodeGetChild(Node, i - 1);
            var ptr = YG.NodeGetContext(childNode);
            var handle = GCHandle.FromIntPtr((IntPtr)ptr);

            if (handle.Target is Element childElement && childElement.DispatchClick(contentPoint))
            {
                return true;
            }
        }

        return HandleClick(contentPoint);
    }

    public override bool TryResolveCursor(SKPoint point, out bool isPointer)
    {
        isPointer = false;

        if (!ContainsPoint(point))
        {
            return false;
        }

        var contentPoint = ToContentPoint(point);

        for (nuint i = YG.NodeGetChildCount(Node); i > 0; i--)
        {
            var childNode = YG.NodeGetChild(Node, i - 1);
            var ptr = YG.NodeGetContext(childNode);
            var handle = GCHandle.FromIntPtr((IntPtr)ptr);

            if (handle.Target is not Element childElement)
            {
                continue;
            }

            if (childElement.TryResolveCursor(contentPoint, out isPointer))
            {
                return true;
            }
        }

        if (TryGetCursorPreference(out isPointer))
        {
            return true;
        }

        if (IsInteractive)
        {
            isPointer = true;
            return true;
        }

        return false;
    }

    public override Element? ResolveActiveElement(SKPoint point)
    {
        if (!ContainsPoint(point))
        {
            return null;
        }

        var contentPoint = ToContentPoint(point);

        for (nuint i = YG.NodeGetChildCount(Node); i > 0; i--)
        {
            var childNode = YG.NodeGetChild(Node, i - 1);
            var ptr = YG.NodeGetContext(childNode);
            var handle = GCHandle.FromIntPtr((IntPtr)ptr);

            if (handle.Target is not Element childElement)
            {
                continue;
            }

            var active = childElement.ResolveActiveElement(contentPoint);
            if (active is not null)
            {
                return active;
            }
        }

        return IsFocusableResolved ? this : null;
    }

    public override bool DispatchScroll(SKPoint point, float deltaY)
    {
        if (!ContainsPoint(point))
        {
            return false;
        }

        var contentPoint = ToContentPoint(point);

        for (nuint i = YG.NodeGetChildCount(Node); i > 0; i--)
        {
            var childNode = YG.NodeGetChild(Node, i - 1);
            var ptr = YG.NodeGetContext(childNode);
            var handle = GCHandle.FromIntPtr((IntPtr)ptr);

            if (handle.Target is Element childElement && childElement.DispatchScroll(contentPoint, deltaY))
            {
                return true;
            }
        }

        return HandleScroll(deltaY);
    }

    public override void RenderSkia()
    {
        base.RenderSkia();
        DrawScrollIndicator();
    }

    protected override bool HandleScroll(float deltaY)
    {
        var maxScroll = MaxScrollY;
        if (maxScroll <= 0)
        {
            return false;
        }

        var old = _scrollY;
        _scrollY = Math.Clamp(_scrollY - (deltaY * ScrollSpeed), 0f, maxScroll);

        if (Math.Abs(_scrollY - old) < 0.01f)
        {
            return false;
        }

        StateHasChanged();
        return true;
    }

    protected override void RenderChildren()
    {
        _viewportHeight = YG.NodeLayoutGetHeight(Node);
        _contentHeight = ComputeContentHeight();

        var maxScroll = MaxScrollY;
        if (_scrollY > maxScroll)
        {
            _scrollY = maxScroll;
        }

        var canvas = OpenTkService.Canvas;
        canvas.Save();
        canvas.Translate(0, -_scrollY);
        base.RenderChildren();
        canvas.Restore();
    }

    private float ComputeContentHeight()
    {
        var maxBottom = 0f;

        for (nuint i = 0; i < YG.NodeGetChildCount(Node); i++)
        {
            var child = YG.NodeGetChild(Node, i);
            var bottom = YG.NodeLayoutGetTop(child) + YG.NodeLayoutGetHeight(child);
            if (bottom > maxBottom)
            {
                maxBottom = bottom;
            }
        }

        return maxBottom;
    }

    private SKPoint ToContentPoint(SKPoint viewportPoint)
    {
        return new SKPoint(viewportPoint.X, viewportPoint.Y + _scrollY);
    }

    private float MaxScrollY => Math.Max(0f, _contentHeight - _viewportHeight);

    private void DrawScrollIndicator()
    {
        if (!ShowScrollIndicator)
        {
            return;
        }

        var maxScroll = MaxScrollY;
        if (maxScroll <= 0f)
        {
            return;
        }

        var canvas = OpenTkService.Canvas;
        var inset = 4f;
        var width = Math.Max(2f, ScrollIndicatorWidth);
        var trackTop = rect.Top + inset;
        var trackBottom = rect.Bottom - inset;
        var trackHeight = Math.Max(0f, trackBottom - trackTop);
        if (trackHeight <= 0f)
        {
            return;
        }

        var trackLeft = rect.Right - inset - width;
        var trackRect = SKRect.Create(trackLeft, trackTop, width, trackHeight);

        var thumbHeight = Math.Max(24f, (_viewportHeight / _contentHeight) * trackHeight);
        thumbHeight = Math.Min(thumbHeight, trackHeight);
        var progress = Math.Clamp(_scrollY / maxScroll, 0f, 1f);
        var thumbTravel = trackHeight - thumbHeight;
        var thumbTop = trackTop + (thumbTravel * progress);
        var thumbRect = SKRect.Create(trackLeft, thumbTop, width, thumbHeight);

        using var trackPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            Color = string.IsNullOrWhiteSpace(ScrollIndicatorTrackColor)
                ? SKColor.Parse("#22000000")
                : SKColor.Parse(ScrollIndicatorTrackColor),
        };

        using var thumbPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            Color = string.IsNullOrWhiteSpace(ScrollIndicatorColor)
                ? SKColor.Parse("#88000000")
                : SKColor.Parse(ScrollIndicatorColor),
        };

        var radius = width / 2f;
        canvas.DrawRoundRect(trackRect, radius, radius, trackPaint);
        canvas.DrawRoundRect(thumbRect, radius, radius, thumbPaint);
    }
}
