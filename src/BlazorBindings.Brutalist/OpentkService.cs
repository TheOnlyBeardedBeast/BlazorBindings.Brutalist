using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SkiaSharp;
using System.Threading;

namespace BlazorBindings.Brutalist;

public sealed class OpentkService : IBrutalistRenderSurface, IDisposable
{
    private readonly GameWindow _window;
    private readonly object _surfaceLock = new();
    private Thread _renderThread;
    private byte[]? _pixelBuffer;
    private volatile bool _surfaceDirty = true;
    private int _textureWidth;
    private int _textureHeight;
    private int _framebufferWidth;
    private int _framebufferHeight;
    private double _elapsedSeconds;

    public event Action? SurfaceResized;
    public event Action<SKPoint>? MouseClicked;
    public event Action<SKPoint>? MouseMoved;
    public event Action<SKPoint, float>? MouseWheelScrolled;
    public event Action<string>? TextInputReceived;
    public event Action<Keys>? KeyDownReceived;
    public event Action<float, double>? FrameTick;

    // Logical (DIP-like) size used by layout and drawing coordinates.
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float DpiScaleX { get; private set; } = 1f;
    public float DpiScaleY { get; private set; } = 1f;

    public SKSurface Surface { get; private set; }
    public SKCanvas Canvas => Surface.Canvas;
    private bool _isPointerCursor;

    public OpentkService(int width = 800, int height = 600, string title = "Blazor OpenTK App")
    {
        Console.WriteLine("[OpentkService] Creating OpenTK service...");
        var gameSettings = new GameWindowSettings
        {
            UpdateFrequency = 60,
        };

        var nativeSettings = new NativeWindowSettings
        {
            ClientSize = new Vector2i(width, height),
            Title = title,
            Flags = ContextFlags.ForwardCompatible,
            WindowBorder = WindowBorder.Resizable
        };

        _window = new GameWindow(gameSettings, nativeSettings);
        Console.WriteLine("[OpentkService] Window created");

        // Use client size for logical coordinates and framebuffer size for pixel backing.
        Width = Math.Max(1, _window.ClientSize.X);
        Height = Math.Max(1, _window.ClientSize.Y);
        _framebufferWidth = Math.Max(1, _window.FramebufferSize.X);
        _framebufferHeight = Math.Max(1, _window.FramebufferSize.Y);
        DpiScaleX = (float)_framebufferWidth / Width;
        DpiScaleY = (float)_framebufferHeight / Height;

        Console.WriteLine($"[OpentkService] ClientSize: {_window.ClientSize.X}x{_window.ClientSize.Y}, FramebufferSize: {_framebufferWidth}x{_framebufferHeight}, Scale: {DpiScaleX:0.##}x{DpiScaleY:0.##}");

        // Backing surface is framebuffer-sized for crisp rendering.
        Surface = CreateSurface(_framebufferWidth, _framebufferHeight);
        Console.WriteLine($"[OpentkService] Software surface created: {_framebufferWidth}x{_framebufferHeight}");

        _window.Load += OnLoad;
        _window.Resize += OnResize;
        _window.RenderFrame += OnRenderFrame;
        _window.MouseDown += OnMouseDown;
        _window.MouseMove += OnMouseMove;
        _window.TextInput += OnTextInput;
        _window.KeyDown += OnKeyDown;

        Console.WriteLine("[OpentkService] Event handlers attached");
    }

    private void OnMouseDown(MouseButtonEventArgs args)
    {
        if (args.Button != MouseButton.Left)
        {
            return;
        }

        var x = _window.MousePosition.X;
        var y = _window.MousePosition.Y;

        if (x > Width || y > Height)
        {
            x /= DpiScaleX;
            y /= DpiScaleY;
        }

        MouseClicked?.Invoke(new SKPoint(x, y));
    }

    private void OnMouseMove(MouseMoveEventArgs args)
    {
        var x = _window.MousePosition.X;
        var y = _window.MousePosition.Y;

        if (x > Width || y > Height)
        {
            x /= DpiScaleX;
            y /= DpiScaleY;
        }

        MouseMoved?.Invoke(new SKPoint(x, y));
    }

    private void OnTextInput(TextInputEventArgs args)
    {
        if (string.IsNullOrEmpty(args.AsString))
        {
            return;
        }

        TextInputReceived?.Invoke(args.AsString);
    }

    private void OnKeyDown(KeyboardKeyEventArgs args)
    {
        KeyDownReceived?.Invoke(args.Key);
    }

    public void SetPointerCursor(bool enabled)
    {
        if (_isPointerCursor == enabled)
        {
            return;
        }

        _isPointerCursor = enabled;
        _window.Cursor = enabled ? MouseCursor.PointingHand : MouseCursor.Default;
    }

    public void Start()
    {
        Console.WriteLine("[OpentkService.Start] Starting window...");
        if (OperatingSystem.IsMacOS())
        {
            Console.WriteLine("[OpentkService.Start] macOS detected - running on main thread");
            Run();
        }
        else
        {
            Console.WriteLine("[OpentkService.Start] Non-macOS - creating render thread");
            _renderThread = new Thread(() => _window.Run()) { IsBackground = true };
            _renderThread.Start();
        }
    }

    private void Run()
    {
        _window.Run();
    }

    private static SKSurface CreateSurface(int width, int height)
    {
        var safeWidth = Math.Max(1, width);
        var safeHeight = Math.Max(1, height);
        var imageInfo = new SKImageInfo(safeWidth, safeHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        return SKSurface.Create(imageInfo) ?? throw new InvalidOperationException("Failed to create SKSurface.");
    }

    private void OnLoad()
    {
        Console.WriteLine("[OpentkService.OnLoad] Initializing OpenGL...");
        Console.Out.Flush();

        GL.ClearColor(0f, 0f, 0f, 1f);
        GL.Disable(EnableCap.DepthTest);
        _window.VSync = VSyncMode.On;

        // Set up shader and quad for rendering the software surface as a texture
        CreateShaders();
        CreateQuad();
        CreateTexture();
        EnsureTextureStorage(_framebufferWidth, _framebufferHeight);

        Console.WriteLine("[OpentkService.OnLoad] OpenGL initialized");
        Console.Out.Flush();
    }

    private void OnResize(ResizeEventArgs args)
    {
        Width = Math.Max(1, _window.ClientSize.X);
        Height = Math.Max(1, _window.ClientSize.Y);
        _framebufferWidth = Math.Max(1, _window.FramebufferSize.X);
        _framebufferHeight = Math.Max(1, _window.FramebufferSize.Y);
        DpiScaleX = (float)_framebufferWidth / Width;
        DpiScaleY = (float)_framebufferHeight / Height;

        if (_framebufferWidth <= 0 || _framebufferHeight <= 0)
        {
            return;
        }

        GL.Viewport(0, 0, _framebufferWidth, _framebufferHeight);

        lock (_surfaceLock)
        {
            var oldSurface = Surface;
            Surface = CreateSurface(_framebufferWidth, _framebufferHeight);

            // Preserve previous frame to avoid black flash while app redraw catches up.
            using (var oldImage = oldSurface.Snapshot())
            {
                if (oldImage is not null)
                {
                    Surface.Canvas.Clear(SKColors.White);
                    Surface.Canvas.DrawImage(oldImage, new SKRect(0, 0, _framebufferWidth, _framebufferHeight));
                    Surface.Flush();
                }
            }

            oldSurface.Dispose();
            _surfaceDirty = true;
        }

        EnsureTextureStorage(_framebufferWidth, _framebufferHeight);

        SurfaceResized?.Invoke();

        Console.WriteLine($"[OnResize] ClientSize: {Width}x{Height}, FramebufferSize: {_framebufferWidth}x{_framebufferHeight}, Scale: {DpiScaleX:0.##}x{DpiScaleY:0.##}, Surface recreated");
    }

    private void OnRenderFrame(FrameEventArgs args)
    {
        var deltaSeconds = (float)Math.Max(0d, args.Time);
        _elapsedSeconds += deltaSeconds;
        FrameTick?.Invoke(deltaSeconds, _elapsedSeconds);

        EmitScrollDeltaFromMouseState();

        // No visual changes pending, so skip GPU work/present.
        if (!_surfaceDirty)
        {
            return;
        }

        GL.Clear(ClearBufferMask.ColorBufferBit);

        if (_surfaceDirty)
        {
            UploadSurfaceToTexture();
        }

        GL.UseProgram(_shaderProgram);
        GL.BindVertexArray(_vao);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _textureId);
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

        _window.SwapBuffers();

        // SaveSurfaceToFile("frame.png");
    }

    private void EmitScrollDeltaFromMouseState()
    {
        var deltaY = _window.MouseState.ScrollDelta.Y;
        if (Math.Abs(deltaY) < float.Epsilon)
        {
            return;
        }

        var x = _window.MousePosition.X;
        var y = _window.MousePosition.Y;

        if (x > Width || y > Height)
        {
            x /= DpiScaleX;
            y /= DpiScaleY;
        }

        MouseWheelScrolled?.Invoke(new SKPoint(x, y), deltaY);
    }

    private void UploadSurfaceToTexture()
    {
        lock (_surfaceLock)
        {
            if (_framebufferWidth <= 0 || _framebufferHeight <= 0)
            {
                return;
            }

            // Get the pixels from the Skia surface
            var requiredSize = _framebufferWidth * _framebufferHeight * 4;
            if (_pixelBuffer is null || _pixelBuffer.Length < requiredSize)
            {
                _pixelBuffer = new byte[requiredSize];
            }

            var handle = System.Runtime.InteropServices.GCHandle.Alloc(_pixelBuffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                var imageInfo = new SKImageInfo(_framebufferWidth, _framebufferHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
                Surface.ReadPixels(imageInfo, handle.AddrOfPinnedObject(), _framebufferWidth * 4, 0, 0);

                GL.BindTexture(TextureTarget.Texture2D, _textureId);
                GL.TexSubImage2D(
                    TextureTarget.Texture2D,
                    0,
                    0,
                    0,
                    _framebufferWidth,
                    _framebufferHeight,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    _pixelBuffer);

                _surfaceDirty = false;
            }
            finally
            {
                handle.Free();
            }
        }
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
            _surfaceDirty = true;
        }
    }

    public void SaveSurfaceToFile(string filename)
    {
        try
        {
            lock (_surfaceLock)
            {
                using (var image = Surface.Snapshot())
                using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
                using (var stream = System.IO.File.OpenWrite(filename))
                {
                    data.SaveTo(stream);
                }
                Console.WriteLine($"[SaveSurfaceToFile] Saved {filename}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveSurfaceToFile] Error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Console.WriteLine("[OpentkService.Dispose] Cleaning up resources...");

        lock (_surfaceLock)
        {
            Surface.Dispose();
        }

        if (_textureId != 0)
        {
            GL.DeleteTexture(_textureId);
        }

        if (_vbo != 0)
        {
            GL.DeleteBuffer(_vbo);
        }

        if (_vao != 0)
        {
            GL.DeleteVertexArray(_vao);
        }

        if (_shaderProgram != 0)
        {
            GL.DeleteProgram(_shaderProgram);
        }

        _window?.Close();
        _window?.Dispose();

        Console.WriteLine("[OpentkService.Dispose] Cleanup complete");
    }

    private int _textureId;
    private int _vao;
    private int _vbo;
    private int _shaderProgram;

    private void EnsureTextureStorage(int width, int height)
    {
        var safeWidth = Math.Max(1, width);
        var safeHeight = Math.Max(1, height);

        if (_textureWidth == safeWidth && _textureHeight == safeHeight)
        {
            return;
        }

        GL.BindTexture(TextureTarget.Texture2D, _textureId);
        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba,
            safeWidth,
            safeHeight,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            IntPtr.Zero);

        _textureWidth = safeWidth;
        _textureHeight = safeHeight;
    }

    private void CreateTexture()
    {
        _textureId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _textureId);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    private void CreateQuad()
    {
        var vertices = new float[]
        {
            -1f, -1f, 0f, 0f, 1f,
             1f, -1f, 0f, 1f, 1f,
            -1f,  1f, 0f, 0f, 0f,
             1f,  1f, 0f, 1f, 0f
        };

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        var stride = 5 * sizeof(float);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
    }

    private void CreateShaders()
    {
        var vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, @"#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;
out vec2 vTexCoord;
void main()
{
    vTexCoord = aTexCoord;
    gl_Position = vec4(aPosition, 1.0);
}");
        GL.CompileShader(vertexShader);

        var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, @"#version 330 core
in vec2 vTexCoord;
out vec4 FragColor;
uniform sampler2D uTexture;
void main()
{
    FragColor = texture(uTexture, vTexCoord);
}");
        GL.CompileShader(fragmentShader);

        _shaderProgram = GL.CreateProgram();
        GL.AttachShader(_shaderProgram, vertexShader);
        GL.AttachShader(_shaderProgram, fragmentShader);
        GL.LinkProgram(_shaderProgram);

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        GL.UseProgram(_shaderProgram);
        var textureLocation = GL.GetUniformLocation(_shaderProgram, "uTexture");
        GL.Uniform1(textureLocation, 0);
    }
}
