using SkiaSharp;
using System.Runtime.InteropServices;
using Yoga;

namespace BlazorBindings.Brutalist.Elements;

public enum ScrollAxes
{
    Y,
    X,
    Both,
}

public unsafe class YogaScrollableView : YogaView
{
    private const float ScrollIndicatorInset = 4f;
    private const float MinScrollbarThumbSize = 24f;
    private const float ScrollbarThumbHitPadding = 8f;

    [Inject]
    protected InteractionState InteractionState { get; set; } = default!;

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

    [Parameter]
    public ScrollAxes ScrollAxes { get; set; } = ScrollAxes.Y;

    private float _scrollX;
    private float _scrollY;
    private float _viewportWidth;
    private float _viewportHeight;
    private float _contentWidth;
    private float _contentHeight;
    private ScrollController? _subscribedScrollController;
    private bool _isDraggingVerticalScrollbar;
    private bool _isDraggingHorizontalScrollbar;
    private float _verticalThumbDragOffset;
    private float _horizontalThumbDragOffset;

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

    public override bool DispatchPointerDown(SKPoint point)
    {
        if (!ContainsPoint(point))
        {
            return false;
        }

        if (TryStartScrollbarDrag(point))
        {
            return true;
        }

        var contentPoint = ToContentPoint(point);
        var blockedByTopChild = false;

        foreach (var childElement in GetChildrenInHitTestOrder(contentPoint))
        {
            if (childElement.DispatchPointerDown(contentPoint))
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
            return HandlePointerDown(contentPoint);
        }

        if (blockedByTopChild)
        {
            return true;
        }

        return false;
    }

    public override bool DispatchPointerMove(SKPoint point)
    {
        if (!ContainsPoint(point))
        {
            return false;
        }

        if (_isDraggingVerticalScrollbar || _isDraggingHorizontalScrollbar)
        {
            return UpdateScrollbarDrag(point);
        }

        var contentPoint = ToContentPoint(point);
        var blockedByTopChild = false;

        foreach (var childElement in GetChildrenInHitTestOrder(contentPoint))
        {
            if (childElement.DispatchPointerMove(contentPoint))
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
            return HandlePointerMove(contentPoint);
        }

        if (blockedByTopChild)
        {
            return true;
        }

        return false;
    }

    public override bool DispatchPointerUp(SKPoint point)
    {
        if (!ContainsPoint(point))
        {
            return false;
        }

        if (_isDraggingVerticalScrollbar || _isDraggingHorizontalScrollbar)
        {
            UpdateScrollbarDrag(point);
            StopScrollbarDrag();
            return true;
        }

        var contentPoint = ToContentPoint(point);
        var blockedByTopChild = false;

        foreach (var childElement in GetChildrenInHitTestOrder(contentPoint))
        {
            if (childElement.DispatchPointerUp(contentPoint))
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
            return HandlePointerUp(contentPoint);
        }

        if (blockedByTopChild)
        {
            return true;
        }

        return false;
    }

    public override bool DispatchCapturedPointerMove(SKPoint point)
    {
        if (_isDraggingVerticalScrollbar || _isDraggingHorizontalScrollbar)
        {
            var adjustedPoint = ToCapturedContentPoint(point);
            return UpdateScrollbarDrag(adjustedPoint);
        }

        return base.DispatchCapturedPointerMove(point);
    }

    public override bool DispatchCapturedPointerUp(SKPoint point)
    {
        if (_isDraggingVerticalScrollbar || _isDraggingHorizontalScrollbar)
        {
            var adjustedPoint = ToCapturedContentPoint(point);
            UpdateScrollbarDrag(adjustedPoint);
            StopScrollbarDrag();
            return true;
        }

        return base.DispatchCapturedPointerUp(point);
    }

    private SKPoint ToCapturedContentPoint(SKPoint point)
    {
        var adjustedX = point.X;
        var adjustedY = point.Y;

        var parentNode = YG.NodeGetParent(Node);
        while (parentNode is not null)
        {
            var parentContext = YG.NodeGetContext(parentNode);
            if (parentContext is not null)
            {
                var handle = GCHandle.FromIntPtr((IntPtr)parentContext);
                if (handle.Target is YogaScrollableView parentScrollView)
                {
                    adjustedX += parentScrollView._scrollX;
                    adjustedY += parentScrollView._scrollY;
                }
            }

            parentNode = YG.NodeGetParent(parentNode);
        }

        return new SKPoint(adjustedX, adjustedY);
    }

    public override bool TryResolveCursor(SKPoint point, out bool isPointer)
    {
        isPointer = false;

        if (!ContainsPoint(point))
        {
            return false;
        }

        if (IsScrollbarThumbHit(point))
        {
            isPointer = true;
            return true;
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

    public override bool DispatchScroll(SKPoint point, float deltaX, float deltaY)
    {
        if (!ContainsPoint(point))
        {
            return false;
        }

        var contentPoint = ToContentPoint(point);

        foreach (var childElement in GetChildrenInHitTestOrder(contentPoint))
        {
            if (childElement.DispatchScroll(contentPoint, deltaX, deltaY))
            {
                return true;
            }
        }

        return HandleScroll(deltaX, deltaY);
    }

    public override void RenderSkia()
    {
        base.RenderSkia();
        DrawScrollIndicator();
    }

    protected override bool HandleScroll(float deltaX, float deltaY)
    {
        var canScrollX = ScrollAxes is ScrollAxes.X or ScrollAxes.Both;
        var canScrollY = ScrollAxes is ScrollAxes.Y or ScrollAxes.Both;

        var didScrollX = false;
        var didScrollY = false;

        if (canScrollY)
        {
            didScrollY = TryScrollY(deltaY * ScrollSpeed, triggerRender: false);
        }

        if (canScrollX)
        {
            didScrollX = TryScrollX(deltaX * ScrollSpeed, triggerRender: false);
        }

        if (didScrollX || didScrollY)
        {
            StateHasChanged();
            return true;
        }

        return PreventParentScroll;
    }

    protected override void RenderChildren()
    {
        _viewportWidth = YG.NodeLayoutGetWidth(Node);
        _viewportHeight = YG.NodeLayoutGetHeight(Node);
        _contentWidth = ComputeContentWidth();
        _contentHeight = ComputeContentHeight();

        var maxScrollX = MaxScrollX;
        var maxScroll = MaxScrollY;
        if (_scrollX > maxScrollX)
        {
            _scrollX = maxScrollX;
        }

        if (_scrollY > maxScroll)
        {
            _scrollY = maxScroll;
        }

        var canvas = OpenTkService.Canvas;
        canvas.Save();
        canvas.Translate(-_scrollX, -_scrollY);

        // Render only children intersecting the current scroll viewport.
        // This keeps culling local to scroll containers and avoids global layout side effects.
        const float overscan = 64f;
        var visibleLeft = _scrollX - overscan;
        var visibleRight = _scrollX + _viewportWidth + overscan;
        var visibleTop = _scrollY - overscan;
        var visibleBottom = _scrollY + _viewportHeight + overscan;

        foreach (var childElement in GetChildrenInRenderOrder())
        {
            var childLeft = YG.NodeLayoutGetLeft(childElement.Node);
            var childRight = childLeft + YG.NodeLayoutGetWidth(childElement.Node);
            var childTop = YG.NodeLayoutGetTop(childElement.Node);
            var childBottom = childTop + YG.NodeLayoutGetHeight(childElement.Node);

            if (childRight < visibleLeft || childLeft > visibleRight || childBottom < visibleTop || childTop > visibleBottom)
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

    private float ComputeContentWidth()
    {
        var maxRight = 0f;

        for (nuint i = 0; i < YG.NodeGetChildCount(Node); i++)
        {
            var child = YG.NodeGetChild(Node, i);
            var right = YG.NodeLayoutGetLeft(child) + YG.NodeLayoutGetWidth(child);
            if (right > maxRight)
            {
                maxRight = right;
            }
        }

        return maxRight;
    }

    private SKPoint ToContentPoint(SKPoint viewportPoint)
    {
        return new SKPoint(viewportPoint.X + _scrollX, viewportPoint.Y + _scrollY);
    }

    private float MaxScrollX => Math.Max(0f, _contentWidth - _viewportWidth);

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

    private bool TryScrollX(float delta, bool triggerRender = true)
    {
        var maxScrollX = MaxScrollX;
        if (maxScrollX <= 0f)
        {
            return false;
        }

        var oldX = _scrollX;
        _scrollX = Math.Clamp(_scrollX - delta, 0f, maxScrollX);
        if (Math.Abs(_scrollX - oldX) < 0.01f)
        {
            return false;
        }

        if (triggerRender)
        {
            StateHasChanged();
        }

        return true;
    }

    private bool TryScrollY(float delta, bool triggerRender = true)
    {
        var maxScrollY = MaxScrollY;
        if (maxScrollY <= 0f)
        {
            return false;
        }

        var oldY = _scrollY;
        _scrollY = Math.Clamp(_scrollY - delta, 0f, maxScrollY);
        if (Math.Abs(_scrollY - oldY) < 0.01f)
        {
            return false;
        }

        if (triggerRender)
        {
            StateHasChanged();
        }

        return true;
    }

    private bool TryStartScrollbarDrag(SKPoint point)
    {
        if (!ShowScrollIndicator)
        {
            return false;
        }

        if (TryGetVerticalIndicatorRects(out _, out var verticalThumbRect) && ExpandHitRect(verticalThumbRect).Contains(point))
        {
            _isDraggingVerticalScrollbar = true;
            _isDraggingHorizontalScrollbar = false;
            _verticalThumbDragOffset = Math.Clamp(point.Y - verticalThumbRect.Top, 0f, verticalThumbRect.Height);
            InteractionState.SetPointerCapture(this);
            return true;
        }

        if (TryGetHorizontalIndicatorRects(out _, out var horizontalThumbRect) && ExpandHitRect(horizontalThumbRect).Contains(point))
        {
            _isDraggingHorizontalScrollbar = true;
            _isDraggingVerticalScrollbar = false;
            _horizontalThumbDragOffset = Math.Clamp(point.X - horizontalThumbRect.Left, 0f, horizontalThumbRect.Width);
            InteractionState.SetPointerCapture(this);
            return true;
        }

        return false;
    }

    private bool UpdateScrollbarDrag(SKPoint point)
    {
        var handled = false;
        var changed = false;

        if (_isDraggingVerticalScrollbar)
        {
            handled = true;

            if (TryGetVerticalIndicatorRects(out var verticalTrackRect, out var verticalThumbRect))
            {
                var thumbTravel = Math.Max(0f, verticalTrackRect.Height - verticalThumbRect.Height);
                var targetTop = point.Y - _verticalThumbDragOffset;
                var clampedTop = Math.Clamp(targetTop, verticalTrackRect.Top, verticalTrackRect.Top + thumbTravel);
                var progress = thumbTravel <= 0f ? 0f : (clampedTop - verticalTrackRect.Top) / thumbTravel;
                var newScrollY = progress * MaxScrollY;

                if (Math.Abs(newScrollY - _scrollY) > 0.01f)
                {
                    _scrollY = newScrollY;
                    changed = true;
                }
            }
        }

        if (_isDraggingHorizontalScrollbar)
        {
            handled = true;

            if (TryGetHorizontalIndicatorRects(out var horizontalTrackRect, out var horizontalThumbRect))
            {
                var thumbTravel = Math.Max(0f, horizontalTrackRect.Width - horizontalThumbRect.Width);
                var targetLeft = point.X - _horizontalThumbDragOffset;
                var clampedLeft = Math.Clamp(targetLeft, horizontalTrackRect.Left, horizontalTrackRect.Left + thumbTravel);
                var progress = thumbTravel <= 0f ? 0f : (clampedLeft - horizontalTrackRect.Left) / thumbTravel;
                var newScrollX = progress * MaxScrollX;

                if (Math.Abs(newScrollX - _scrollX) > 0.01f)
                {
                    _scrollX = newScrollX;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            StateHasChanged();
        }

        return handled;
    }

    private void StopScrollbarDrag()
    {
        _isDraggingVerticalScrollbar = false;
        _isDraggingHorizontalScrollbar = false;
    }

    private bool IsScrollbarThumbHit(SKPoint point)
    {
        if (!ShowScrollIndicator)
        {
            return false;
        }

        if (TryGetVerticalIndicatorRects(out _, out var verticalThumbRect) && ExpandHitRect(verticalThumbRect).Contains(point))
        {
            return true;
        }

        if (TryGetHorizontalIndicatorRects(out _, out var horizontalThumbRect) && ExpandHitRect(horizontalThumbRect).Contains(point))
        {
            return true;
        }

        return false;
    }

    private bool TryGetVerticalIndicatorRects(out SKRect trackRect, out SKRect thumbRect)
    {
        trackRect = SKRect.Empty;
        thumbRect = SKRect.Empty;

        if (!ShowScrollIndicator || ScrollAxes is ScrollAxes.X)
        {
            return false;
        }

        var maxScrollY = MaxScrollY;
        if (maxScrollY <= 0f)
        {
            return false;
        }

        var width = Math.Max(2f, ScrollIndicatorWidth);
        var trackTop = rect.Top + ScrollIndicatorInset;
        var trackBottom = rect.Bottom - ScrollIndicatorInset;
        var trackHeight = Math.Max(0f, trackBottom - trackTop);
        if (trackHeight <= 0f)
        {
            return false;
        }

        var trackLeft = rect.Right - ScrollIndicatorInset - width;
        trackRect = SKRect.Create(trackLeft, trackTop, width, trackHeight);

        var thumbHeight = Math.Max(MinScrollbarThumbSize, (_viewportHeight / _contentHeight) * trackHeight);
        thumbHeight = Math.Min(thumbHeight, trackHeight);
        var progress = Math.Clamp(_scrollY / maxScrollY, 0f, 1f);
        var thumbTravel = trackHeight - thumbHeight;
        var thumbTop = trackTop + (thumbTravel * progress);
        thumbRect = SKRect.Create(trackLeft, thumbTop, width, thumbHeight);

        return true;
    }

    private bool TryGetHorizontalIndicatorRects(out SKRect trackRect, out SKRect thumbRect)
    {
        trackRect = SKRect.Empty;
        thumbRect = SKRect.Empty;

        if (!ShowScrollIndicator || ScrollAxes is ScrollAxes.Y)
        {
            return false;
        }

        var maxScrollX = MaxScrollX;
        if (maxScrollX <= 0f)
        {
            return false;
        }

        var width = Math.Max(2f, ScrollIndicatorWidth);
        var trackLeft = rect.Left + ScrollIndicatorInset;
        var trackRight = rect.Right - ScrollIndicatorInset;
        var trackWidth = Math.Max(0f, trackRight - trackLeft);
        if (trackWidth <= 0f)
        {
            return false;
        }

        var trackTop = rect.Bottom - ScrollIndicatorInset - width;
        trackRect = SKRect.Create(trackLeft, trackTop, trackWidth, width);

        var thumbWidth = Math.Max(MinScrollbarThumbSize, (_viewportWidth / _contentWidth) * trackWidth);
        thumbWidth = Math.Min(thumbWidth, trackWidth);
        var progress = Math.Clamp(_scrollX / maxScrollX, 0f, 1f);
        var thumbTravel = trackWidth - thumbWidth;
        var thumbLeft = trackLeft + (thumbTravel * progress);
        thumbRect = SKRect.Create(thumbLeft, trackTop, thumbWidth, width);

        return true;
    }

    private static SKRect ExpandHitRect(SKRect rect)
    {
        var hitRect = rect;
        hitRect.Inflate(ScrollbarThumbHitPadding, ScrollbarThumbHitPadding);
        return hitRect;
    }

    private void DrawScrollIndicator()
    {
        if (!ShowScrollIndicator)
        {
            return;
        }

        var drawVertical = TryGetVerticalIndicatorRects(out var verticalTrackRect, out var verticalThumbRect);
        var drawHorizontal = TryGetHorizontalIndicatorRects(out var horizontalTrackRect, out var horizontalThumbRect);
        if (!drawVertical && !drawHorizontal)
        {
            return;
        }

        var canvas = OpenTkService.Canvas;
        var width = Math.Max(2f, ScrollIndicatorWidth);

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

        if (drawVertical)
        {
            canvas.DrawRoundRect(verticalTrackRect, radius, radius, trackPaint);
            canvas.DrawRoundRect(verticalThumbRect, radius, radius, thumbPaint);
        }

        if (drawHorizontal)
        {
            canvas.DrawRoundRect(horizontalTrackRect, radius, radius, trackPaint);
            canvas.DrawRoundRect(horizontalThumbRect, radius, radius, thumbPaint);
        }
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
