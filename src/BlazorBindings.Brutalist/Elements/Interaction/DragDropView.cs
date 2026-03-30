using SkiaSharp;
using System.Runtime.InteropServices;
using Yoga;

namespace BlazorBindings.Brutalist.Elements;

public class YogaDragDropView : YogaClickableView
{
    private const float DragThreshold = 4f;
    private const int DragPreviewZIndex = 1_000_000;

    private static readonly List<WeakReference<YogaDragDropView>> RegisteredDropTargets = [];
    private static YogaDragDropView? ActiveSource;
    private static YogaDragDropView? ActiveTarget;
    private static bool ActiveCanDrop;
    private static SKPoint ActivePointer;
    private static SKRect ActivePreviewRect;
    private static object? ActiveDragData;

    [Parameter]
    public bool EnableDrag { get; set; } = true;

    [Parameter]
    public bool EnableDrop { get; set; }

    [Parameter]
    public bool AllowDropOnSelf { get; set; }

    [Parameter]
    public string? DragType { get; set; }

    [Parameter]
    public string? Scope { get; set; }

    [Parameter]
    public string? AcceptDragType { get; set; }

    [Parameter]
    public bool ShowDefaultDragPreview { get; set; } = true;

    [Parameter]
    public float DragPreviewOpacity { get; set; } = 0.9f;

    [Parameter]
    public string? DragPreviewBorderRadius { get; set; }

    [Parameter]
    public object? DragData { get; set; }

    [Parameter]
    public Func<CanDropEventArgs, bool>? OnCanDrop { get; set; }

    [Parameter]
    public EventCallback<DragPreviewEventArgs> OnDragPreviewChanged { get; set; }

    [Parameter]
    public EventCallback<DropTargetStateChangedEventArgs> OnDropStateChanged { get; set; }

    [Parameter]
    public EventCallback<DropEventArgs> OnDrop { get; set; }

    [Parameter]
    public EventCallback<DropEventArgs> OnDragCompleted { get; set; }

    private bool _registeredAsDropTarget;
    private bool _pendingDrag;
    private bool _dragging;
    private SKPoint _dragStartPoint;
    private SKPoint _dragGrabOffset;
    private SKImage? _dragPreviewSnapshot;
    private int _savedZIndex;
    private bool _dropStateIsOver;
    private bool _dropStateCanDrop;

    protected override bool IsInteractive => base.IsInteractive || (!Disabled && EnableDrag);

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters);
        UpdateDropTargetRegistration();
    }

    protected override bool HandlePointerDown(SKPoint point)
    {
        var handledByBase = base.HandlePointerDown(point);

        if (Disabled || !EnableDrag)
        {
            return handledByBase;
        }

        _pendingDrag = true;
        _dragStartPoint = point;
        _dragGrabOffset = new SKPoint(point.X - rect.Left, point.Y - rect.Top);
        return true;
    }

    protected override bool HandlePointerMove(SKPoint point)
    {
        var handledByBase = base.HandlePointerMove(point);

        if (_pendingDrag && !_dragging && Distance(_dragStartPoint, point) >= DragThreshold)
        {
            StartDrag(point);
        }

        if (_dragging && ReferenceEquals(ActiveSource, this))
        {
            UpdateDrag(point);
            return true;
        }

        return handledByBase;
    }

    protected override bool HandlePointerUp(SKPoint point)
    {
        var handledByBase = base.HandlePointerUp(point);

        if (_dragging && ReferenceEquals(ActiveSource, this))
        {
            CompleteDrag(point);
            return true;
        }

        _pendingDrag = false;
        return handledByBase;
    }

    public override bool DispatchCapturedPointerMove(SKPoint point)
    {
        if (_dragging && ReferenceEquals(ActiveSource, this))
        {
            var adjustedPoint = ToCapturedContentPoint(point);
            return HandlePointerMove(adjustedPoint);
        }

        return base.DispatchCapturedPointerMove(point);
    }

    public override bool DispatchCapturedPointerUp(SKPoint point)
    {
        if (_dragging && ReferenceEquals(ActiveSource, this))
        {
            var adjustedPoint = ToCapturedContentPoint(point);
            return HandlePointerUp(adjustedPoint);
        }

        return base.DispatchCapturedPointerUp(point);
    }

    protected override void RenderPostMain(SKCanvas canvas, SKRect bounds)
    {
        base.RenderPostMain(canvas, bounds);
    }

    private void StartDrag(SKPoint pointer)
    {
        _pendingDrag = false;
        _dragging = true;
        ActiveSource = this;
        ActiveTarget = null;
        ActiveCanDrop = false;
        ActiveDragData = DragData;
        _savedZIndex = ZIndex;
        ZIndex = DragPreviewZIndex;

        CapturePreviewSnapshot();
        UpdateDrag(pointer);
    }

    private void UpdateDrag(SKPoint pointer)
    {
        ActivePointer = pointer;
        ActivePreviewRect = SKRect.Create(
            pointer.X - _dragGrabOffset.X,
            pointer.Y - _dragGrabOffset.Y,
            rect.Width,
            rect.Height);

        EvaluateTarget();
        NotifyDragPreviewState(true);
        StateHasChanged();
    }

    private void CompleteDrag(SKPoint pointer)
    {
        ActivePointer = pointer;
        var dropEvent = new DropEventArgs
        {
            Source = this,
            Target = ActiveTarget ?? this,
            Pointer = ActivePointer,
            PreviewRect = ActivePreviewRect,
            DragType = DragType,
            Scope = Scope,
            DragData = DragData,
        };

        if (ActiveTarget is not null && ActiveCanDrop)
        {
            ActiveTarget.InvokeDropEvent(dropEvent);
            InvokeDragCompleted(dropEvent);
        }
        else
        {
            InvokeDragCompleted(dropEvent);
        }

        ResetDragSession();
    }

    private void ResetDragSession()
    {
        _pendingDrag = false;
        _dragging = false;
        ZIndex = _savedZIndex;

        _dragPreviewSnapshot?.Dispose();
        _dragPreviewSnapshot = null;

        NotifyDragPreviewState(false);

        if (ActiveTarget is not null)
        {
            ActiveTarget.UpdateDropState(false, false, ActiveSource, ActiveDragData);
        }

        ActiveSource = null;
        ActiveTarget = null;
        ActiveCanDrop = false;
        ActiveDragData = null;

        StateHasChanged();
    }

    private void EvaluateTarget()
    {
        var previousTarget = ActiveTarget;
        var previousCanDrop = ActiveCanDrop;

        var newTarget = FindBestDropTarget();
        var canDrop = newTarget?.CanAcceptDropFrom(this) == true;

        if (!ReferenceEquals(previousTarget, newTarget) || previousCanDrop != canDrop)
        {
            if (previousTarget is not null)
            {
                previousTarget.UpdateDropState(false, false, this, ActiveDragData);
            }

            if (newTarget is not null)
            {
                newTarget.UpdateDropState(true, canDrop, this, ActiveDragData);
            }
        }

        ActiveTarget = newTarget;
        ActiveCanDrop = canDrop;
    }

    private YogaDragDropView? FindBestDropTarget()
    {
        CleanupRegisteredTargets();

        YogaDragDropView? best = null;
        var previewRectInViewport = GetActivePreviewRectInViewport();

        for (var i = 0; i < RegisteredDropTargets.Count; i++)
        {
            if (!RegisteredDropTargets[i].TryGetTarget(out var candidate) || candidate is null)
            {
                continue;
            }

            if (candidate.Disabled || !candidate.EnableDrop)
            {
                continue;
            }

            if (ReferenceEquals(candidate, this) && !AllowDropOnSelf)
            {
                continue;
            }

            if (!ScopeMatches(candidate.Scope, Scope))
            {
                continue;
            }

            if (!candidate.GetRectInViewport().IntersectsWith(previewRectInViewport))
            {
                continue;
            }

            if (best is null || candidate.ZIndex > best.ZIndex)
            {
                best = candidate;
            }
        }

        return best;
    }

    private bool CanAcceptDropFrom(YogaDragDropView source)
    {
        if (!EnableDrop || Disabled)
        {
            return false;
        }

        if (!ScopeMatches(Scope, source.Scope))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(AcceptDragType) &&
            !string.Equals(AcceptDragType, source.DragType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (OnCanDrop is not null)
        {
            return OnCanDrop(new CanDropEventArgs
            {
                Source = source,
                Target = this,
                DragType = source.DragType,
                Scope = source.Scope,
                DragData = source.DragData,
            });
        }

        return true;
    }

    private void CapturePreviewSnapshot()
    {
        _dragPreviewSnapshot?.Dispose();
        _dragPreviewSnapshot = null;

        using var surfaceSnapshot = OpenTkService.Surface.Snapshot();

        var surfaceWidth = surfaceSnapshot.Width;
        var surfaceHeight = surfaceSnapshot.Height;
        if (surfaceWidth <= 0 || surfaceHeight <= 0)
        {
            return;
        }

        var sourceRectDip = GetRectInViewport();
        var width = Math.Max(1, (int)Math.Ceiling(sourceRectDip.Width * OpenTkService.DpiScaleX));
        var height = Math.Max(1, (int)Math.Ceiling(sourceRectDip.Height * OpenTkService.DpiScaleY));
        var left = (int)Math.Floor(sourceRectDip.Left * OpenTkService.DpiScaleX);
        var top = (int)Math.Floor(sourceRectDip.Top * OpenTkService.DpiScaleY);

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var clampedLeft = Math.Clamp(left, 0, Math.Max(0, surfaceWidth - 1));
        var clampedTop = Math.Clamp(top, 0, Math.Max(0, surfaceHeight - 1));
        var clampedRight = Math.Clamp(left + width, clampedLeft + 1, surfaceWidth);
        var clampedBottom = Math.Clamp(top + height, clampedTop + 1, surfaceHeight);

        if (clampedRight <= clampedLeft || clampedBottom <= clampedTop)
        {
            return;
        }

        var sourceRect = new SKRectI(clampedLeft, clampedTop, clampedRight, clampedBottom);
        _dragPreviewSnapshot = surfaceSnapshot.Subset(sourceRect);
    }

    internal static void RenderDragPreviewOverlay(SKCanvas canvas)
    {
        if (ActiveSource is null)
        {
            return;
        }

        if (!ActiveSource.ShowDefaultDragPreview)
        {
            return;
        }

        if (!ActiveSource._dragging)
        {
            return;
        }

        if (ActiveSource._dragPreviewSnapshot is null)
        {
            return;
        }

        var previewRectInViewport = GetActivePreviewRectInViewport();

        canvas.Save();

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White.WithAlpha((byte)Math.Clamp((int)Math.Round(ActiveSource.DragPreviewOpacity * 255f), 0, 255)),
        };

        var radiusValue = ActiveSource.DragPreviewBorderRadius ?? ActiveSource.BorderRadius;
        if (!string.IsNullOrWhiteSpace(radiusValue))
        {
            var (topLeft, topRight, bottomRight, bottomLeft) = StyleParsers.ParseBorderRadius(radiusValue);
            var rrect = new SKRoundRect();
            rrect.SetRectRadii(previewRectInViewport, new[]
            {
                new SKPoint(topLeft, topLeft),
                new SKPoint(topRight, topRight),
                new SKPoint(bottomRight, bottomRight),
                new SKPoint(bottomLeft, bottomLeft),
            });

            using var clipPath = new SKPath();
            clipPath.AddRoundRect(rrect);
            canvas.ClipPath(clipPath, SKClipOperation.Intersect, true);
        }

        canvas.DrawImage(ActiveSource._dragPreviewSnapshot, previewRectInViewport, paint);
        canvas.Restore();
    }

    private static SKRect GetActivePreviewRectInViewport()
    {
        if (ActiveSource is null)
        {
            return SKRect.Empty;
        }

        var preview = ActivePreviewRect;
        var scrollOffset = ActiveSource.GetAncestorScrollOffset();
        preview.Offset(-scrollOffset.X, -scrollOffset.Y);
        return preview;
    }

    private SKRect GetRectInViewport()
    {
        var viewportRect = rect;
        var scrollOffset = GetAncestorScrollOffset();
        viewportRect.Offset(-scrollOffset.X, -scrollOffset.Y);
        return viewportRect;
    }

    private void UpdateDropState(bool isOver, bool canDrop, YogaDragDropView? source, object? dragData = null)
    {
        if (_dropStateIsOver == isOver && _dropStateCanDrop == canDrop)
        {
            return;
        }

        _dropStateIsOver = isOver;
        _dropStateCanDrop = canDrop;

        InvokeEventCallback(OnDropStateChanged, new DropTargetStateChangedEventArgs
        {
            Source = source,
            Target = this,
            IsDragOver = isOver,
            CanDrop = canDrop,
            DragType = source?.DragType,
            Scope = source?.Scope,
            DragData = dragData,
        });

        StateHasChanged();
    }

    private void NotifyDragPreviewState(bool isDragging)
    {
        InvokeEventCallback(OnDragPreviewChanged, new DragPreviewEventArgs
        {
            Source = this,
            HoverTarget = ActiveTarget,
            Pointer = ActivePointer,
            PreviewRect = ActivePreviewRect,
            IsDragging = isDragging,
            CanDrop = ActiveCanDrop,
            DragType = DragType,
            Scope = Scope,
            DragData = DragData,
        });
    }

    private void InvokeDropEvent(DropEventArgs args)
    {
        InvokeEventCallback(OnDrop, args);
    }

    private void InvokeDragCompleted(DropEventArgs args)
    {
        InvokeEventCallback(OnDragCompleted, args);
    }

    private void UpdateDropTargetRegistration()
    {
        if (EnableDrop && !_registeredAsDropTarget)
        {
            RegisteredDropTargets.Add(new WeakReference<YogaDragDropView>(this));
            _registeredAsDropTarget = true;
            CleanupRegisteredTargets();
            return;
        }

        if (!EnableDrop && _registeredAsDropTarget)
        {
            RemoveTarget(this);
            _registeredAsDropTarget = false;
        }
    }

    private static void RemoveTarget(YogaDragDropView target)
    {
        for (var i = RegisteredDropTargets.Count - 1; i >= 0; i--)
        {
            if (!RegisteredDropTargets[i].TryGetTarget(out var existing) || existing is null || ReferenceEquals(existing, target))
            {
                RegisteredDropTargets.RemoveAt(i);
            }
        }
    }

    private static void CleanupRegisteredTargets()
    {
        for (var i = RegisteredDropTargets.Count - 1; i >= 0; i--)
        {
            if (!RegisteredDropTargets[i].TryGetTarget(out var target) || target is null || !target._registeredAsDropTarget)
            {
                RegisteredDropTargets.RemoveAt(i);
            }
        }
    }

    private static bool ScopeMatches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return true;
        }

        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static float Distance(SKPoint a, SKPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private unsafe SKPoint ToCapturedContentPoint(SKPoint point)
    {
        var adjustedX = point.X;
        var adjustedY = point.Y;

        var ancestorScroll = GetAncestorScrollOffset();
        adjustedX += ancestorScroll.X;
        adjustedY += ancestorScroll.Y;

        return new SKPoint(adjustedX, adjustedY);
    }

    private unsafe SKPoint GetAncestorScrollOffset()
    {
        var scrollX = 0f;
        var scrollY = 0f;

        var parentNode = YG.NodeGetParent(Node);
        while (parentNode is not null)
        {
            var parentContext = YG.NodeGetContext(parentNode);
            if (parentContext is not null)
            {
                var handle = GCHandle.FromIntPtr((IntPtr)parentContext);
                if (handle.Target is YogaScrollableView parentScrollView)
                {
                    scrollX += parentScrollView.CurrentScrollX;
                    scrollY += parentScrollView.CurrentScrollY;
                }
            }

            parentNode = YG.NodeGetParent(parentNode);
        }

        return new SKPoint(scrollX, scrollY);
    }
}