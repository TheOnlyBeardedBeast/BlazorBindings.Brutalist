using SkiaSharp;
using Yoga;

namespace BlazorBindings.Brutalist.Elements;

public unsafe class YogaScrollableView : YogaView
{
    [Parameter]
    public bool PreventParentScroll { get; set; } = false;
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
    private ScrollController? _subscribedScrollController;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        var task = base.SetParametersAsync(parameters);

        // (re)subscribe to controller events
        if (!ReferenceEquals(_subscribedScrollController, ScrollController))
        {
            if (_subscribedScrollController is not null)
            {
                _subscribedScrollController.ScrollRequested -= OnScrollRequested;
            }

            if (ScrollController is not null)
            {
                ScrollController.ScrollRequested -= OnScrollRequested;
                ScrollController.ScrollRequested += OnScrollRequested;
            }

            _subscribedScrollController = ScrollController;
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
        var blockedByTopChild = false;

        foreach (var childElement in GetChildrenInHitTestOrder(contentPoint))
        {
            if (childElement.DispatchClick(contentPoint))
            {
                return true;
            }

            if (childElement.HitTest(contentPoint) && childElement.ShouldBlockClickThrough())
            {
                blockedByTopChild = true;
                break;
            }
        }

        if (IsInteractive)
        {
            return HandleClick(contentPoint);
        }

        if (blockedByTopChild)
        {
            return true;
        }

        return false;
    }

    public override bool TryResolveCursor(SKPoint point, out bool isPointer)
    {
        isPointer = false;

        if (!ContainsPoint(point))
        {
            return false;
        }

        var contentPoint = ToContentPoint(point);

        foreach (var childElement in GetChildrenInHitTestOrder(contentPoint))
        {
            if (childElement.TryResolveCursor(contentPoint, out isPointer))
            {
                return true;
            }

            if (childElement.HitTest(contentPoint) && childElement.ShouldBlockCursorThrough())
            {
                isPointer = false;
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

        if (BlocksCursorThrough)
        {
            isPointer = false;
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

        foreach (var childElement in GetChildrenInHitTestOrder(contentPoint))
        {
            if (!childElement.HitTest(contentPoint))
            {
                continue;
            }

            var active = childElement.ResolveActiveElement(contentPoint);
            if (active is not null)
            {
                return active;
            }

            if (childElement.ShouldBlockFocusThrough())
            {
                return null;
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

        foreach (var childElement in GetChildrenInHitTestOrder(contentPoint))
        {
            if (childElement.DispatchScroll(contentPoint, deltaY))
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
            return PreventParentScroll;
        }

        var old = _scrollY;
        _scrollY = Math.Clamp(_scrollY - (deltaY * ScrollSpeed), 0f, maxScroll);

        if (Math.Abs(_scrollY - old) < 0.01f)
        {
            return PreventParentScroll;
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

        // Render only children intersecting the current scroll viewport.
        // This keeps culling local to scroll containers and avoids global layout side effects.
        const float overscan = 64f;
        var visibleTop = _scrollY - overscan;
        var visibleBottom = _scrollY + _viewportHeight + overscan;

        foreach (var childElement in GetChildrenInRenderOrder())
        {
            var childTop = YG.NodeLayoutGetTop(childElement.Node);
            var childBottom = childTop + YG.NodeLayoutGetHeight(childElement.Node);

            if (childBottom < visibleTop || childTop > visibleBottom)
            {
                continue;
            }

            childElement.RenderSkia();
        }

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

    [Parameter]
    public ScrollController? ScrollController { get; set; }

    private void OnScrollRequested(ScrollRequest req)
    {
        if (req == null) return;

        // Run on renderer thread
        _ = InvokeAsync(() =>
        {
            var maxScroll = MaxScrollY;
            var newScroll = _scrollY;

            if (req.Element is null)
            {
                newScroll = req.LocalTop;
            }
            else
            {
                var childTop = YG.NodeLayoutGetTop(req.Element.Node);
                var absoluteTop = childTop + req.LocalTop;
                var absoluteBottom = childTop + req.LocalBottom;

                if (absoluteTop < _scrollY)
                {
                    newScroll = absoluteTop;
                }
                else if (absoluteBottom > _scrollY + _viewportHeight)
                {
                    newScroll = absoluteBottom - _viewportHeight;
                }
            }

            newScroll = Math.Clamp(newScroll, 0f, maxScroll);
            if (Math.Abs(newScroll - _scrollY) > 0.01f)
            {
                _scrollY = newScroll;
                StateHasChanged();
            }
        });
    }

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

    public void Dispose()
    {
        if (_subscribedScrollController is not null)
        {
            _subscribedScrollController.ScrollRequested -= OnScrollRequested;
            _subscribedScrollController = null;
        }
    }
}
