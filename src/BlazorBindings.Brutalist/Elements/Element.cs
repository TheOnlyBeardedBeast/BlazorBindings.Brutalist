using SkiaSharp;
using System.Runtime.InteropServices;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Yoga;

namespace BlazorBindings.Brutalist.Elements;

public unsafe class Element : NativeControlComponentBase
{
    [Parameter]
    public string? Background { get; set; }

    [Parameter]
    public YGFlexDirection? Direction { get; set; }

    [Parameter]
    public string? Wrap { get; set; }

    [Parameter]
    public string? Grow { get; set; }
    [Parameter]
    public string? Shrink { get; set; }
    [Parameter]
    public string? Basis { get; set; }
    [Parameter]
    public string? JustifyContent { get; set; }
    [Parameter]
    public string? AlignItems { get; set; }
    [Parameter]
    public string? AlignContent { get; set; }
    [Parameter]
    public string? AlignSelf { get; set; }
    [Parameter]
    public float? Width { get; set; }
    [Parameter]
    public float? Height { get; set; }
    [Parameter]
    public string? MinWidth { get; set; }
    [Parameter]
    public string? MinHeight { get; set; }
    [Parameter]
    public string? MaxWidth { get; set; }
    [Parameter]
    public string? MaxHeight { get; set; }
    [Parameter]
    public float? Gap { get; set; }
    [Parameter]
    public string? BorderRadius { get; set; }
    [Parameter]
    public string? BorderWidth { get; set; }
    [Parameter]
    public string? BorderColor { get; set; }
    [Parameter]
    public string? OutlineWidth { get; set; }
    [Parameter]
    public string? OutlineColor { get; set; }
    [Parameter]
    public string? OutlineOffset { get; set; }
    [Parameter]
    public string? Padding { get; set; }
    [Parameter]
    public string? Margin { get; set; }
    [Parameter]
    public string? Position { get; set; }
    [Parameter]
    public string? Top { get; set; }
    [Parameter]
    public string? Right { get; set; }
    [Parameter]
    public string? Bottom { get; set; }
    [Parameter]
    public string? Left { get; set; }
    [Parameter]
    public string? Overflow { get; set; }
    [Parameter]
    public string? Display { get; set; }
    [Parameter]
    public string? AspectRatio { get; set; }
    [Parameter]
    public string? Cursor { get; set; }
    [Parameter]
    public bool? Focusable { get; set; }
    public YGNode* Node { get; } = YG.NodeNew();

    [Parameter]
    public string? Id { get; set; }

    [Inject] protected IBrutalistRenderSurface OpenTkService { get; set; } = default!;

    public SKRect rect { get; set; }
    private SKPath? _hitPath;

    private string? ResolvedBackground
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Background))
            {
                return null;
            }

            // If background has 9 characters (#RRGGBBAA), extract first 7 (#RRGGBB)
            if (Background.Length == 9 && Background.StartsWith("#"))
            {
                return Background[..7];
            }

            return Background;
        }
    }

    private byte? ResolvedTransparency
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Background))
            {
                return null;
            }

            // If background has 9 characters (#RRGGBBAA), extract last 2 (AA)
            if (Background.Length == 9 && Background.StartsWith("#"))
            {
                var alphaHex = Background[7..9];
                if (byte.TryParse(alphaHex, System.Globalization.NumberStyles.HexNumber, null, out var alpha))
                {
                    return alpha;
                }
            }

            return null;
        }
    }

    protected Element() : base()
    {
        // Console.WriteLine("View created");
        var handle = GCHandle.Alloc(this);
        YG.NodeSetContext(Node, (void*)GCHandle.ToIntPtr(handle));

        // Use web-like default behavior so flex items can shrink when space is constrained.
        // This avoids children in row layouts taking full window width unless explicitly prevented.
        YG.NodeStyleSetFlexShrink(Node, 1f);
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        static void ApplyLength(StyleParsers.CssLength? length, Action<float> setPoint, Action<float> setPercent)
        {
            if (!length.HasValue)
            {
                return;
            }

            if (length.Value.Kind == StyleParsers.CssLengthKind.Percent)
            {
                setPercent(length.Value.Value);
            }
            else if (length.Value.Kind == StyleParsers.CssLengthKind.Point)
            {
                setPoint(length.Value.Value);
            }
        }

        parameters.SetParameterProperties(this);
        StateHasChanged();

        if (Direction.HasValue)
        {
            YG.NodeStyleSetFlexDirection(Node, Direction.Value);
        }

        var flexGrow = StyleParsers.ParseFloat(Grow);
        if (flexGrow.HasValue)
        {
            YG.NodeStyleSetFlexGrow(Node, flexGrow.Value);
        }

        var flexShrink = StyleParsers.ParseFloat(Shrink);
        if (flexShrink.HasValue)
        {
            YG.NodeStyleSetFlexShrink(Node, flexShrink.Value);
        }

        if (!string.IsNullOrWhiteSpace(Basis))
        {
            var basisValue = Basis.Trim();
            if (basisValue.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                YG.NodeStyleSetFlexBasisAuto(Node);
            }
            else if (basisValue.EndsWith("%", StringComparison.Ordinal))
            {
                var percent = StyleParsers.ParseFloat(basisValue[..^1]);
                if (percent.HasValue)
                {
                    YG.NodeStyleSetFlexBasisPercent(Node, percent.Value);
                }
            }
            else
            {
                var basis = StyleParsers.ParseFloat(basisValue);
                if (basis.HasValue)
                {
                    YG.NodeStyleSetFlexBasis(Node, basis.Value);
                }
            }
        }

        if (Width.HasValue)
        {
            YG.NodeStyleSetWidth(Node, Width.Value);
        }

        if (Height.HasValue)
        {
            YG.NodeStyleSetHeight(Node, Height.Value);
        }

        ApplyLength(
            StyleParsers.ParseCssLength(MinWidth),
            v => YG.NodeStyleSetMinWidth(Node, v),
            v => YG.NodeStyleSetMinWidthPercent(Node, v));

        ApplyLength(
            StyleParsers.ParseCssLength(MinHeight),
            v => YG.NodeStyleSetMinHeight(Node, v),
            v => YG.NodeStyleSetMinHeightPercent(Node, v));

        ApplyLength(
            StyleParsers.ParseCssLength(MaxWidth),
            v => YG.NodeStyleSetMaxWidth(Node, v),
            v => YG.NodeStyleSetMaxWidthPercent(Node, v));

        ApplyLength(
            StyleParsers.ParseCssLength(MaxHeight),
            v => YG.NodeStyleSetMaxHeight(Node, v),
            v => YG.NodeStyleSetMaxHeightPercent(Node, v));

        if (Gap.HasValue)
        {
            YG.NodeStyleSetGap(Node, YGGutter.YGGutterAll, Gap.Value);
        }

        if (!string.IsNullOrWhiteSpace(Padding))
        {
            var (top, right, bottom, left) = StyleParsers.ParseCssValue(Padding);
            YG.NodeStyleSetPadding(Node, YGEdge.YGEdgeTop, top);
            YG.NodeStyleSetPadding(Node, YGEdge.YGEdgeRight, right);
            YG.NodeStyleSetPadding(Node, YGEdge.YGEdgeBottom, bottom);
            YG.NodeStyleSetPadding(Node, YGEdge.YGEdgeLeft, left);
        }

        if (!string.IsNullOrWhiteSpace(Margin))
        {
            var (top, right, bottom, left) = StyleParsers.ParseCssValue(Margin);
            YG.NodeStyleSetMargin(Node, YGEdge.YGEdgeTop, top);
            YG.NodeStyleSetMargin(Node, YGEdge.YGEdgeRight, right);
            YG.NodeStyleSetMargin(Node, YGEdge.YGEdgeBottom, bottom);
            YG.NodeStyleSetMargin(Node, YGEdge.YGEdgeLeft, left);
        }

        if (!string.IsNullOrWhiteSpace(BorderWidth))
        {
            var (top, right, bottom, left) = StyleParsers.ParseCssValue(BorderWidth);
            YG.NodeStyleSetBorder(Node, YGEdge.YGEdgeTop, top);
            YG.NodeStyleSetBorder(Node, YGEdge.YGEdgeRight, right);
            YG.NodeStyleSetBorder(Node, YGEdge.YGEdgeBottom, bottom);
            YG.NodeStyleSetBorder(Node, YGEdge.YGEdgeLeft, left);
        }

        var positionType = StyleParsers.ParsePositionType(Position);
        if (positionType.HasValue)
        {
            YG.NodeStyleSetPositionType(Node, positionType.Value);
        }

        ApplyLength(
            StyleParsers.ParseCssLength(Top),
            v => YG.NodeStyleSetPosition(Node, YGEdge.YGEdgeTop, v),
            v => YG.NodeStyleSetPositionPercent(Node, YGEdge.YGEdgeTop, v));

        ApplyLength(
            StyleParsers.ParseCssLength(Right),
            v => YG.NodeStyleSetPosition(Node, YGEdge.YGEdgeRight, v),
            v => YG.NodeStyleSetPositionPercent(Node, YGEdge.YGEdgeRight, v));

        ApplyLength(
            StyleParsers.ParseCssLength(Bottom),
            v => YG.NodeStyleSetPosition(Node, YGEdge.YGEdgeBottom, v),
            v => YG.NodeStyleSetPositionPercent(Node, YGEdge.YGEdgeBottom, v));

        ApplyLength(
            StyleParsers.ParseCssLength(Left),
            v => YG.NodeStyleSetPosition(Node, YGEdge.YGEdgeLeft, v),
            v => YG.NodeStyleSetPositionPercent(Node, YGEdge.YGEdgeLeft, v));

        var wrap = StyleParsers.ParseFlexWrap(Wrap);
        if (wrap.HasValue)
        {
            YG.NodeStyleSetFlexWrap(Node, wrap.Value);
        }

        var justify = StyleParsers.ParseJustifyContent(JustifyContent);
        if (justify.HasValue)
        {
            YG.NodeStyleSetJustifyContent(Node, justify.Value);
        }

        var alignItems = StyleParsers.ParseAlign(AlignItems);
        if (alignItems.HasValue)
        {
            YG.NodeStyleSetAlignItems(Node, alignItems.Value);
        }

        var alignContent = StyleParsers.ParseAlign(AlignContent);
        if (alignContent.HasValue)
        {
            YG.NodeStyleSetAlignContent(Node, alignContent.Value);
        }

        var alignSelf = StyleParsers.ParseAlign(AlignSelf);
        if (alignSelf.HasValue)
        {
            YG.NodeStyleSetAlignSelf(Node, alignSelf.Value);
        }

        var overflow = StyleParsers.ParseOverflow(Overflow);
        if (overflow.HasValue)
        {
            YG.NodeStyleSetOverflow(Node, overflow.Value);
        }

        var display = StyleParsers.ParseDisplay(Display);
        if (display.HasValue)
        {
            YG.NodeStyleSetDisplay(Node, display.Value);
        }

        var aspectRatio = StyleParsers.ParseFloat(AspectRatio);
        if (aspectRatio.HasValue)
        {
            YG.NodeStyleSetAspectRatio(Node, aspectRatio.Value);
        }

        return Task.CompletedTask;
    }

    public void Test()
    {
        Console.WriteLine("TEST");
    }


    public virtual void Render()
    {
        // Debug.WriteLine("View Rendered");
        Console.WriteLine($"[Element.Render] {Id ?? "UnknownId"} - Rendering Yoga layout");
        YG.NodeCalculateLayout(Node, YG.YGUndefined, YG.YGUndefined, YGDirection.YGDirectionLTR);

        // Console.WriteLine($"Left={YG.NodeLayoutGetLeft(Node)},Top={YG.NodeLayoutGetTop(Node)},Width={YG.NodeLayoutGetWidth(Node)},Height={YG.NodeLayoutGetHeight(Node)}");

        // var childCount = YG.NodeGetChildCount(Node);
        // Console.WriteLine(childCount);
        // for (nuint i = 0; i < childCount; i++)
        // {
        //     var child = YG.NodeGetChild(Node, i);

        //     var ptr = YG.NodeGetContext(child);
        //     var handle = GCHandle.FromIntPtr((IntPtr)ptr);
        //     var element = (Element)handle.Target!;
        // }

        RenderSkia();
    }

    public virtual void AddChild(object child, int physicalSiblingIndex) { }

    public virtual void RenderSkia()
    {
        var canvas = OpenTkService.Canvas;
        var offset = GetOffset();

        rect = SKRect.Create(offset.left, offset.top, YG.NodeLayoutGetWidth(Node), YG.NodeLayoutGetHeight(Node));
        UpdateHitShape();

        if (Id == "RouteHost")
        {
            Console.WriteLine($"[RouteHost] x={rect.Left}, y={rect.Top}, w={rect.Width}, h={rect.Height}, children={YG.NodeGetChildCount(Node)}");
        }

        var overflow = StyleParsers.ParseOverflow(Overflow);
        var shouldClip = overflow == YGOverflow.YGOverflowHidden;

        if (shouldClip)
        {
            canvas.Save();

            if (!string.IsNullOrWhiteSpace(BorderRadius))
            {
                var (topLeft, topRight, bottomRight, bottomLeft) = StyleParsers.ParseBorderRadius(BorderRadius);
                var rrect = new SKRoundRect();
                rrect.SetRectRadii(rect, new[]
                {
                    new SKPoint(topLeft, topLeft),
                    new SKPoint(topRight, topRight),
                    new SKPoint(bottomRight, bottomRight),
                    new SKPoint(bottomLeft, bottomLeft)
                });

                using var clipPath = new SKPath();
                clipPath.AddRoundRect(rrect);
                canvas.ClipPath(clipPath, SKClipOperation.Intersect, true);
            }
            else
            {
                canvas.ClipRect(rect);
            }
        }

        // Console.WriteLine($"Left={YG.NodeLayoutGetLeft(Node)},Top={YG.NodeLayoutGetTop(Node)},Width={YG.NodeLayoutGetWidth(Node)},Height={YG.NodeLayoutGetHeight(Node)}");

        if (Background is not null)
        {
            var color = SKColor.Parse(ResolvedBackground ?? Background);

            // Apply transparency/alpha if specified
            if (ResolvedTransparency.HasValue)
            {
                color = color.WithAlpha(ResolvedTransparency.Value);
            }

            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = color,
                IsAntialias = true,
            };

            if (!string.IsNullOrWhiteSpace(BorderRadius))
            {
                var (topLeft, topRight, bottomRight, bottomLeft) = StyleParsers.ParseBorderRadius(BorderRadius);

                // Use SKRoundRect for individual corner radii
                var rrect = new SKRoundRect();
                rrect.SetRectRadii(rect, new[]
                {
                    new SKPoint(topLeft, topLeft),     // Top-left
                    new SKPoint(topRight, topRight),   // Top-right
                    new SKPoint(bottomRight, bottomRight), // Bottom-right
                    new SKPoint(bottomLeft, bottomLeft)    // Bottom-left
                });

                canvas.DrawRoundRect(rrect, paint);
            }
            else
            {
                canvas.DrawRect(rect, paint);
            }
        }

        RenderChildren();

        if (shouldClip)
        {
            canvas.Restore();
        }

        if (!string.IsNullOrWhiteSpace(BorderWidth))
        {
            var (top, right, bottom, left) = StyleParsers.ParseCssValue(BorderWidth);
            var color = string.IsNullOrWhiteSpace(BorderColor) ? SKColors.Black : SKColor.Parse(BorderColor);

            if (top > 0 && Math.Abs(top - right) < float.Epsilon && Math.Abs(top - bottom) < float.Epsilon && Math.Abs(top - left) < float.Epsilon)
            {
                using var borderPaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = top,
                    Color = color,
                    IsAntialias = true,
                };

                if (!string.IsNullOrWhiteSpace(BorderRadius))
                {
                    var (topLeft, topRight, bottomRight, bottomLeft) = StyleParsers.ParseBorderRadius(BorderRadius);
                    var rrect = new SKRoundRect();
                    rrect.SetRectRadii(rect, new[]
                    {
                        new SKPoint(topLeft, topLeft),
                        new SKPoint(topRight, topRight),
                        new SKPoint(bottomRight, bottomRight),
                        new SKPoint(bottomLeft, bottomLeft)
                    });
                    canvas.DrawRoundRect(rrect, borderPaint);
                }
                else
                {
                    canvas.DrawRect(rect, borderPaint);
                }
            }
            else
            {
                using var borderPaint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    Color = color,
                    IsAntialias = true,
                };

                if (top > 0)
                    canvas.DrawRect(SKRect.Create(rect.Left, rect.Top, rect.Width, top), borderPaint);
                if (right > 0)
                    canvas.DrawRect(SKRect.Create(rect.Right - right, rect.Top, right, rect.Height), borderPaint);
                if (bottom > 0)
                    canvas.DrawRect(SKRect.Create(rect.Left, rect.Bottom - bottom, rect.Width, bottom), borderPaint);
                if (left > 0)
                    canvas.DrawRect(SKRect.Create(rect.Left, rect.Top, left, rect.Height), borderPaint);
            }
        }

        if (!string.IsNullOrWhiteSpace(OutlineWidth))
        {
            var (top, right, bottom, left) = StyleParsers.ParseCssValue(OutlineWidth);
            var color = string.IsNullOrWhiteSpace(OutlineColor) ? SKColors.Black : SKColor.Parse(OutlineColor);
            var outlineOffset = StyleParsers.ParseFloat(OutlineOffset) ?? 0f;

            var borderOutTop = 0f;
            var borderOutRight = 0f;
            var borderOutBottom = 0f;
            var borderOutLeft = 0f;

            if (!string.IsNullOrWhiteSpace(BorderWidth))
            {
                var (bTop, bRight, bBottom, bLeft) = StyleParsers.ParseCssValue(BorderWidth);
                var isUniformBorder = bTop > 0
                    && Math.Abs(bTop - bRight) < float.Epsilon
                    && Math.Abs(bTop - bBottom) < float.Epsilon
                    && Math.Abs(bTop - bLeft) < float.Epsilon;

                // Uniform borders are currently rendered with stroke centered on element bounds,
                // so half of the width extends outside and must be respected by outline.
                if (isUniformBorder)
                {
                    var half = bTop / 2f;
                    borderOutTop = half;
                    borderOutRight = half;
                    borderOutBottom = half;
                    borderOutLeft = half;
                }
            }

            if (top > 0 && Math.Abs(top - right) < float.Epsilon && Math.Abs(top - bottom) < float.Epsilon && Math.Abs(top - left) < float.Epsilon)
            {
                var outlineWidth = top;
                var halfWidth = outlineWidth / 2f;
                var expand = outlineOffset + halfWidth + Math.Max(Math.Max(borderOutTop, borderOutRight), Math.Max(borderOutBottom, borderOutLeft));
                var outlineRect = SKRect.Create(
                    rect.Left - expand,
                    rect.Top - expand,
                    rect.Width + (expand * 2f),
                    rect.Height + (expand * 2f));

                using var outlinePaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = outlineWidth,
                    Color = color,
                    IsAntialias = true,
                };

                if (!string.IsNullOrWhiteSpace(BorderRadius))
                {
                    var (topLeft, topRight, bottomRight, bottomLeft) = StyleParsers.ParseBorderRadius(BorderRadius);
                    var radiusExpand = outlineOffset + halfWidth + Math.Max(Math.Max(borderOutTop, borderOutRight), Math.Max(borderOutBottom, borderOutLeft));
                    var rrect = new SKRoundRect();
                    rrect.SetRectRadii(outlineRect, new[]
                    {
                        new SKPoint(topLeft + radiusExpand, topLeft + radiusExpand),
                        new SKPoint(topRight + radiusExpand, topRight + radiusExpand),
                        new SKPoint(bottomRight + radiusExpand, bottomRight + radiusExpand),
                        new SKPoint(bottomLeft + radiusExpand, bottomLeft + radiusExpand)
                    });
                    canvas.DrawRoundRect(rrect, outlinePaint);
                }
                else
                {
                    canvas.DrawRect(outlineRect, outlinePaint);
                }
            }
            else
            {
                using var outlinePaint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    Color = color,
                    IsAntialias = true,
                };

                var outerLeft = rect.Left - outlineOffset - left - borderOutLeft;
                var outerTop = rect.Top - outlineOffset - top - borderOutTop;
                var outerRight = rect.Right + outlineOffset + right + borderOutRight;

                if (top > 0)
                    canvas.DrawRect(SKRect.Create(outerLeft, outerTop, outerRight - outerLeft, top), outlinePaint);
                if (right > 0)
                    canvas.DrawRect(SKRect.Create(rect.Right + outlineOffset + borderOutRight, rect.Top - outlineOffset - borderOutTop, right, rect.Height + (outlineOffset * 2f) + borderOutTop + borderOutBottom), outlinePaint);
                if (bottom > 0)
                    canvas.DrawRect(SKRect.Create(outerLeft, rect.Bottom + outlineOffset + borderOutBottom, outerRight - outerLeft, bottom), outlinePaint);
                if (left > 0)
                    canvas.DrawRect(SKRect.Create(rect.Left - outlineOffset - left - borderOutLeft, rect.Top - outlineOffset - borderOutTop, left, rect.Height + (outlineOffset * 2f) + borderOutTop + borderOutBottom), outlinePaint);
            }
        }
    }

    private void UpdateHitShape()
    {
        _hitPath?.Dispose();
        _hitPath = null;

        if (string.IsNullOrWhiteSpace(BorderRadius))
        {
            return;
        }

        var (topLeft, topRight, bottomRight, bottomLeft) = StyleParsers.ParseBorderRadius(BorderRadius);
        var rrect = new SKRoundRect();
        rrect.SetRectRadii(rect, new[]
        {
            new SKPoint(topLeft, topLeft),
            new SKPoint(topRight, topRight),
            new SKPoint(bottomRight, bottomRight),
            new SKPoint(bottomLeft, bottomLeft)
        });

        _hitPath = new SKPath();
        _hitPath.AddRoundRect(rrect);
    }

    public virtual bool DispatchClick(SKPoint point)
    {
        if (!ContainsPoint(point))
        {
            return false;
        }

        for (nuint i = YG.NodeGetChildCount(Node); i > 0; i--)
        {
            var childNode = YG.NodeGetChild(Node, i - 1);
            var ptr = YG.NodeGetContext(childNode);
            var handle = GCHandle.FromIntPtr((IntPtr)ptr);

            if (handle.Target is Element childElement && childElement.DispatchClick(point))
            {
                return true;
            }
        }

        if (!IsInteractive)
        {
            return false;
        }

        return HandleClick(point);
    }

    public virtual bool DispatchTextInput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return HandleTextInput(text);
    }

    public virtual bool DispatchKeyDown(Keys key)
    {
        return HandleKeyDown(key);
    }

    public virtual bool DispatchScroll(SKPoint point, float deltaY)
    {
        for (nuint i = YG.NodeGetChildCount(Node); i > 0; i--)
        {
            var childNode = YG.NodeGetChild(Node, i - 1);
            var ptr = YG.NodeGetContext(childNode);
            var handle = GCHandle.FromIntPtr((IntPtr)ptr);

            if (handle.Target is Element childElement && childElement.DispatchScroll(point, deltaY))
            {
                return true;
            }
        }

        if (!ContainsPoint(point))
        {
            return false;
        }

        return HandleScroll(deltaY);
    }

    public virtual bool TryResolveCursor(SKPoint point, out bool isPointer)
    {
        isPointer = false;
        return TryResolveCursorCore(point, out isPointer);
    }

    public virtual Element? ResolveActiveElement(SKPoint point)
    {
        if (!ContainsPoint(point))
        {
            return null;
        }

        for (nuint i = YG.NodeGetChildCount(Node); i > 0; i--)
        {
            var childNode = YG.NodeGetChild(Node, i - 1);
            var ptr = YG.NodeGetContext(childNode);
            var handle = GCHandle.FromIntPtr((IntPtr)ptr);

            if (handle.Target is not Element childElement)
            {
                continue;
            }

            var activeElement = childElement.ResolveActiveElement(point);
            if (activeElement is not null)
            {
                return activeElement;
            }
        }

        return IsFocusableResolved ? this : null;
    }

    private bool TryResolveCursorCore(SKPoint point, out bool isPointer)
    {
        isPointer = false;

        if (!ContainsPoint(point))
        {
            return false;
        }

        for (nuint i = YG.NodeGetChildCount(Node); i > 0; i--)
        {
            var childNode = YG.NodeGetChild(Node, i - 1);
            var ptr = YG.NodeGetContext(childNode);
            var handle = GCHandle.FromIntPtr((IntPtr)ptr);

            if (handle.Target is not Element childElement)
            {
                continue;
            }

            if (childElement.TryResolveCursorCore(point, out isPointer))
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

    private Element? FindInteractiveElement(SKPoint point)
    {
        if (!ContainsPoint(point))
        {
            return null;
        }

        for (nuint i = YG.NodeGetChildCount(Node); i > 0; i--)
        {
            var childNode = YG.NodeGetChild(Node, i - 1);
            var ptr = YG.NodeGetContext(childNode);
            var handle = GCHandle.FromIntPtr((IntPtr)ptr);

            if (handle.Target is not Element childElement)
            {
                continue;
            }

            var interactiveElement = childElement.FindInteractiveElement(point);
            if (interactiveElement is not null)
            {
                return interactiveElement;
            }
        }

        return IsInteractive ? this : null;
    }

    protected bool TryGetCursorPreference(out bool isPointer)
    {
        isPointer = false;

        if (string.IsNullOrWhiteSpace(Cursor))
        {
            return false;
        }

        var cursor = Cursor.Trim();
        if (cursor.Equals("pointer", StringComparison.OrdinalIgnoreCase)
            || cursor.Equals("hand", StringComparison.OrdinalIgnoreCase))
        {
            isPointer = true;
            return true;
        }

        if (cursor.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            isPointer = false;
            return true;
        }

        return false;
    }

    protected bool IsFocusableResolved => Focusable ?? IsFocusable;

    protected virtual bool IsInteractive => false;
    protected virtual bool IsFocusable => IsInteractive;

    protected virtual bool HandleClick(SKPoint point)
    {
        return false;
    }

    protected virtual bool HandleTextInput(string text)
    {
        return false;
    }

    protected virtual bool HandleKeyDown(Keys key)
    {
        return false;
    }

    protected virtual bool HandleScroll(float deltaY)
    {
        return false;
    }

    protected bool ContainsPoint(SKPoint point)
    {
        if (_hitPath is not null)
        {
            return _hitPath.Contains(point.X, point.Y);
        }

        return rect.Contains(point);
    }

    public (float top, float right, float bottom, float left) GetOffset()
    {
        var parent = YG.NodeGetParent(Node);

        if (parent is not null)
        {
            var ptr = YG.NodeGetContext(parent);
            var handle = GCHandle.FromIntPtr((IntPtr)ptr);
            var element = (Element)handle.Target!;

            var (top, right, bottom, left) = element.GetOffset();

            return (top + YG.NodeLayoutGetTop(Node), right + YG.NodeLayoutGetRight(Node), bottom + YG.NodeLayoutGetBottom(Node), left + YG.NodeLayoutGetLeft(Node));
        }

        return (YG.NodeLayoutGetTop(Node), YG.NodeLayoutGetRight(Node), YG.NodeLayoutGetBottom(Node), YG.NodeLayoutGetLeft(Node));
    }

    protected virtual void RenderChildren()
    {

        for (nuint i = 0; i < YG.NodeGetChildCount(Node); i++)
        {
            YGNode* node = YG.NodeGetChild(Node, i);
            var ptr = YG.NodeGetContext(node);
            var handle = GCHandle.FromIntPtr((IntPtr)ptr);
            var element = (Element)handle.Target!;
            element.RenderSkia();
        }
    }
}