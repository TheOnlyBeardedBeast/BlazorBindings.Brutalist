using SkiaSharp;

namespace BlazorBindings.Brutalist.Elements;

public class YogaSineWaveLoader : YogaView, IDisposable
{
    [Parameter]
    public string? Color { get; set; }

    [Parameter]
    public string? SecondaryColor { get; set; }

    [Parameter]
    public float Amplitude { get; set; } = 8f;

    [Parameter]
    public float Wavelength { get; set; } = 48f;

    [Parameter]
    public float Speed { get; set; } = 1.6f;

    [Parameter]
    public float StrokeWidth { get; set; } = 3f;

    [Parameter]
    public int Samples { get; set; } = 72;

    [Parameter]
    public bool Animated { get; set; } = true;

    [Inject]
    protected AnimationTicker AnimationTicker { get; set; } = default!;

    private bool _isSubscribed;
    private float _phase;

    public YogaSineWaveLoader()
    {
        unsafe
        {
            Yoga.YG.NodeStyleSetMinHeight(Node, 24f);
        }
    }

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        await base.SetParametersAsync(parameters);
        EnsureTickerSubscription();
    }

    public override void RenderSkia()
    {
        base.RenderSkia();

        var offset = GetOffset();
        float width;
        float height;
        unsafe
        {
            width = Yoga.YG.NodeLayoutGetWidth(Node);
            height = Yoga.YG.NodeLayoutGetHeight(Node);
        }

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var bounds = SKRect.Create(offset.left, offset.top, width, height);

        var (paddingTop, paddingRight, paddingBottom, paddingLeft) =
            string.IsNullOrWhiteSpace(Padding)
                ? (0f, 0f, 0f, 0f)
                : StyleParsers.ParseCssValue(Padding);

        var contentBounds = SKRect.Create(
            bounds.Left + paddingLeft,
            bounds.Top + paddingTop,
            Math.Max(0, bounds.Width - paddingLeft - paddingRight),
            Math.Max(0, bounds.Height - paddingTop - paddingBottom));

        if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
        {
            return;
        }

        var safeSamples = Math.Max(24, Math.Max(Samples, (int)(contentBounds.Width / 3f)));
        var safeStroke = Math.Max(1f, StrokeWidth);
        var maxAmplitude = Math.Max(0f, (contentBounds.Height - safeStroke) / 2f);
        var effectiveAmplitude = Math.Min(maxAmplitude, Math.Max(0f, Amplitude));
        if (effectiveAmplitude <= 0)
        {
            return;
        }

        var safeWavelength = Math.Max(12f, Wavelength);
        var centerY = contentBounds.MidY;

        using var primaryPath = BuildWavePath(contentBounds, safeSamples, safeWavelength, centerY, effectiveAmplitude, _phase);
        using var secondaryPath = BuildWavePath(contentBounds, safeSamples, safeWavelength, centerY, effectiveAmplitude * 0.8f, _phase + 1.4f);

        using var secondaryPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = safeStroke,
            Color = ResolveColor(SecondaryColor, SKColor.Parse("#80a5b4fc")),
        };

        using var primaryPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = safeStroke,
            Color = ResolveColor(Color, SKColor.Parse("#6366f1")),
        };

        var canvas = OpenTkService.Canvas;
        canvas.DrawPath(secondaryPath, secondaryPaint);
        canvas.DrawPath(primaryPath, primaryPaint);
    }

    private void EnsureTickerSubscription()
    {
        if (_isSubscribed)
        {
            return;
        }

        AnimationTicker.Tick += OnAnimationTick;
        _isSubscribed = true;
    }

    private void OnAnimationTick(float deltaSeconds, double elapsedSeconds)
    {
        if (!Animated)
        {
            return;
        }

        var safeSpeed = Math.Max(0f, Speed);
        _phase += deltaSeconds * safeSpeed * (2f * MathF.PI);

        if (_phase > MathF.PI * 2f)
        {
            _phase %= (2f * MathF.PI);
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private static SKPath BuildWavePath(SKRect bounds, int samples, float wavelength, float centerY, float amplitude, float phase)
    {
        var path = new SKPath();
        var dx = bounds.Width / (samples - 1);
        var points = new SKPoint[samples];

        for (var i = 0; i < samples; i++)
        {
            var x = bounds.Left + (i * dx);
            var progressX = x - bounds.Left;
            var angle = ((progressX / wavelength) * 2f * MathF.PI) + phase;
            var y = centerY + (MathF.Sin(angle) * amplitude);

            points[i] = new SKPoint(x, y);
        }

        path.MoveTo(points[0]);

        for (var i = 1; i < samples - 1; i++)
        {
            var control = points[i];
            var next = points[i + 1];
            var mid = new SKPoint((control.X + next.X) / 2f, (control.Y + next.Y) / 2f);
            path.QuadTo(control, mid);
        }

        path.LineTo(points[^1]);

        return path;
    }

    private static SKColor ResolveColor(string? value, SKColor fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return SKColor.Parse(value);
        }
        catch
        {
            return fallback;
        }
    }

    public void Dispose()
    {
        if (!_isSubscribed)
        {
            return;
        }

        AnimationTicker.Tick -= OnAnimationTick;
        _isSubscribed = false;
    }
}
