using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;

namespace BlazorBindings.Brutalist;

public interface IBrutalistRenderSurface
{
    event Action? SurfaceResized;
    event Action<SKPoint>? MouseClicked;
    event Action<SKPoint>? MouseMoved;
    event Action<SKPoint, float>? MouseWheelScrolled;
    event Action<string>? TextInputReceived;
    event Action<Keys>? KeyDownReceived;
    event Action<float, double>? FrameTick;

    int Width { get; }
    int Height { get; }
    float DpiScaleX { get; }
    float DpiScaleY { get; }
    SKSurface Surface { get; }
    SKCanvas Canvas { get; }

    void Start();
    void SetPointerCursor(bool enabled);
    void LockSurface(Action<SKCanvas> draw);
    void SaveSurfaceToFile(string filename);
}
