using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;

namespace BlazorBindings.Brutalist;

public sealed class ImageRenderService : IBrutalistRenderSurface, IDisposable
{
    private readonly object _surfaceLock = new();
    private double _elapsedSeconds;

    public event Action? SurfaceResized;
    public event Action<SKPoint>? MouseClicked;
    public event Action<SKPoint>? MouseMoved;
    public event Action<SKPoint, float>? MouseWheelScrolled;
    public event Action<string>? TextInputReceived;
    public event Action<Keys>? KeyDownReceived;
    public event Action<float, double>? FrameTick;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public float DpiScaleX { get; private set; }
    public float DpiScaleY { get; private set; }
    public SKSurface Surface { get; private set; }
    public SKCanvas Canvas => Surface.Canvas;

    public ImageRenderService(int width = 800, int height = 600, float dpiScale = 1f)
    {
        var safeWidth = Math.Max(1, width);
        var safeHeight = Math.Max(1, height);
        var safeScale = Math.Max(0.1f, dpiScale);

        Width = safeWidth;
        Height = safeHeight;
        DpiScaleX = safeScale;
        DpiScaleY = safeScale;

        Surface = CreateSurface(WidthInPixels, HeightInPixels);
    }

    private int WidthInPixels => Math.Max(1, (int)Math.Round(Width * DpiScaleX));
    private int HeightInPixels => Math.Max(1, (int)Math.Round(Height * DpiScaleY));

    public void Start()
    {
        // Intentionally no-op in headless mode.
    }

    public void SetPointerCursor(bool enabled)
    {
        // No cursor in headless mode.
    }

    public void Resize(int width, int height, float? dpiScale = null)
    {
        var safeWidth = Math.Max(1, width);
        var safeHeight = Math.Max(1, height);
        var safeScale = Math.Max(0.1f, dpiScale ?? DpiScaleX);

        lock (_surfaceLock)
        {
            Width = safeWidth;
            Height = safeHeight;
            DpiScaleX = safeScale;
            DpiScaleY = safeScale;

            var oldSurface = Surface;
            Surface = CreateSurface(WidthInPixels, HeightInPixels);

            using var oldImage = oldSurface.Snapshot();
            Surface.Canvas.Clear(SKColors.White);
            Surface.Canvas.DrawImage(oldImage, new SKRect(0, 0, WidthInPixels, HeightInPixels));
            Surface.Flush();
            oldSurface.Dispose();
        }

        SurfaceResized?.Invoke();
    }

    public void LockSurface(Action<SKCanvas> draw)
    {
        lock (_surfaceLock)
        {
            var canvas = Surface.Canvas;
            canvas.Save();
            canvas.Scale(DpiScaleX, DpiScaleY);
            draw(canvas);
            canvas.Restore();
            Surface.Flush();
        }
    }

    public void SaveSurfaceToFile(string filename)
    {
        lock (_surfaceLock)
        {
            using var image = Surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            using var stream = System.IO.File.OpenWrite(filename);
            data.SaveTo(stream);
        }
    }

    public byte[] RenderToImage(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 90)
    {
        lock (_surfaceLock)
        {
            using var image = Surface.Snapshot();
            using var data = image.Encode(format, quality);
            return data.ToArray();
        }
    }

    public void AdvanceFrame(float deltaSeconds)
    {
        var safeDelta = Math.Max(0f, deltaSeconds);
        _elapsedSeconds += safeDelta;
        FrameTick?.Invoke(safeDelta, _elapsedSeconds);
    }

    public void DispatchMouseClick(SKPoint point)
    {
        MouseClicked?.Invoke(point);
    }

    public void DispatchMouseMove(SKPoint point)
    {
        MouseMoved?.Invoke(point);
    }

    public void DispatchMouseWheel(SKPoint point, float deltaY)
    {
        MouseWheelScrolled?.Invoke(point, deltaY);
    }

    public void DispatchTextInput(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            TextInputReceived?.Invoke(text);
        }
    }

    public void DispatchKeyDown(Keys key)
    {
        KeyDownReceived?.Invoke(key);
    }

    public void Dispose()
    {
        lock (_surfaceLock)
        {
            Surface.Dispose();
        }
    }

    private static SKSurface CreateSurface(int width, int height)
    {
        var imageInfo = new SKImageInfo(Math.Max(1, width), Math.Max(1, height), SKColorType.Rgba8888, SKAlphaType.Premul);
        return SKSurface.Create(imageInfo) ?? throw new InvalidOperationException("Failed to create SKSurface.");
    }
}
