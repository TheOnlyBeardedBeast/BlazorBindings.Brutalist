using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System.Threading;

namespace BlazorBindings.Brutalist;

public class WindowRenderer : IDisposable
{
    private GameWindow _window;
    private Thread _renderThread;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public event Action? OnResize;
    public event Action? OnRenderFrame;

    private bool _running = true;

    public WindowRenderer()
    {
        var nativeSettings = new NativeWindowSettings()
        {
            Size = new OpenTK.Mathematics.Vector2i(800, 600),
            Title = "Blazor OpenTK App",
            Flags = ContextFlags.ForwardCompatible
        };

        _window = new GameWindow(GameWindowSettings.Default, nativeSettings);

        Width = _window.Size.X;
        Height = _window.Size.Y;

        _window.Resize += OnResized;
        _window.RenderFrame += OnRendered;

        if (OperatingSystem.IsMacOS())
        {
            Run(); // on main thread
        }
        else
        {
            _renderThread = new Thread(() => _window.Run()) { IsBackground = true };
            _renderThread.Start();
        }
    }

    protected void Run()
    {
        _window.Run();
    }

    private void OnResized(ResizeEventArgs args)
    {
        Width = args.Width;
        Height = args.Height;
        OnResize?.Invoke();
    }

    private void OnRendered(FrameEventArgs args)
    {
        OnRenderFrame?.Invoke();
        _window.SwapBuffers();
    }

    public void Dispose()
    {
        _running = false;
        _window.Close();
        _window.Dispose();
    }
}

