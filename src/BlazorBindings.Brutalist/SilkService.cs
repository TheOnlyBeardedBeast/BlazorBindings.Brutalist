using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;
using System.Runtime.InteropServices;
using System.Threading;

namespace BlazorBindings.Brutalist;

/// <summary>
/// An <see cref="IBrutalistRenderSurface"/> implementation backed by Silk.NET for windowing/input
/// and OpenGL 3.3 Core for accelerated texture blitting.
/// Drop-in replacement for <see cref="OpentkService"/> and <see cref="Sdl3Service"/>.
/// </summary>
public sealed class SilkService : IBrutalistRenderSurface, IDisposable
{
    // ─── Silk.NET handles ─────────────────────────────────────────────────────
    private readonly IWindow _window;
    private GL _gl = null!;
    private IInputContext _input = null!;

    // ─── Skia surface ─────────────────────────────────────────────────────────
    private readonly object _surfaceLock = new();
    private byte[]? _pixelBuffer;
    private volatile bool _surfaceDirty = true;
    private int _textureWidth;
    private int _textureHeight;

    // ─── OpenGL resources ─────────────────────────────────────────────────────
    private uint _textureId;
    private uint _vao;
    private uint _vbo;
    private uint _shaderProgram;

    // ─── Layout state ─────────────────────────────────────────────────────────
    private int _framebufferWidth;
    private int _framebufferHeight;
    private double _elapsedSeconds;
    private Thread? _renderThread;
    private bool _isPointerCursor;
    private bool _isDisposed;

    private readonly object _resizeRequestLock = new();
    private int? _pendingResizeWidth;
    private int? _pendingResizeHeight;
    private readonly object _scrollStateLock = new();
    private float _pendingScrollDeltaX;
    private float _pendingScrollDeltaY;
    private System.Numerics.Vector2 _lastMousePosition;

    // ─── IBrutalistRenderSurface events ───────────────────────────────────────
    public event Action? SurfaceResized;
    public event Action<SKPoint>? MouseClicked;
    public event Action<SKPoint>? MouseReleased;
    public event Action<SKPoint>? MouseMoved;
    public event Action<SKPoint, float, float>? MouseWheelScrolled;
    public event Action<string>? TextInputReceived;
    public event Action<BrutalistKey, bool>? KeyDownReceived;
    public event Action<float, double>? FrameTick;

    // ─── IBrutalistRenderSurface properties ───────────────────────────────────
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float DpiScaleX { get; private set; } = 1f;
    public float DpiScaleY { get; private set; } = 1f;
    public SKSurface Surface { get; private set; } = null!;
    public SKCanvas Canvas => Surface.Canvas;

    // ─────────────────────────────────────────────────────────────────────────

    public SilkService(int width = 800, int height = 600, string title = "Blazor Silk.NET App")
    {
        Console.WriteLine("[SilkService] Creating Silk.NET service...");

        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(width, height),
            Title = title,
            API = GraphicsAPI.Default, // OpenGL 3.3 Core (forward-compatible)
            VSync = true,
            FramesPerSecond = 60,
            IsEventDriven = true, // We'll drive the loop manually to control update vs render timing
        };

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Resize += OnResize;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.Closing += OnClosing;

        // Seed logical / framebuffer sizes with constructor values until OnLoad fires.
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        _framebufferWidth = Width;
        _framebufferHeight = Height;
        Surface = CreateSurface(Width, Height);

        Console.WriteLine("[SilkService] Window created.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void Start()
    {
        Console.WriteLine("[SilkService.Start] Starting...");

        if (OperatingSystem.IsMacOS())
        {
            // macOS requires the GL context and event loop on the main thread.
            _window.Run();
        }
        else
        {
            _renderThread = new Thread(() => _window.Run())
            {
                IsBackground = true,
                Name = "SilkRenderThread"
            };
            _renderThread.Start();
        }
    }

    public void ResizeSurface(int width, int height)
    {
        lock (_resizeRequestLock)
        {
            _pendingResizeWidth = Math.Max(1, width);
            _pendingResizeHeight = Math.Max(1, height);
        }
    }

    public void SetPointerCursor(bool enabled)
    {
        if (_isPointerCursor == enabled)
        {
            return;
        }

        _isPointerCursor = enabled;

        if (_input is null)
        {
            return;
        }

        foreach (var mouse in _input.Mice)
        {
            mouse.Cursor.StandardCursor = enabled ? StandardCursor.Hand : StandardCursor.Default;
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
                using var image = Surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 80);
                using var stream = System.IO.File.OpenWrite(filename);
                data.SaveTo(stream);
                Console.WriteLine($"[SilkService] Saved {filename}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SilkService] SaveSurfaceToFile error: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Window event handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnLoad()
    {
        Console.WriteLine("[SilkService.OnLoad] Initialising OpenGL...");

        _gl = GL.GetApi(_window);
        _input = _window.CreateInput();

        foreach (var mouse in _input.Mice)
        {
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
            mouse.Scroll += OnMouseScroll;
        }

        foreach (var keyboard in _input.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyChar += OnKeyChar;
        }

        // Sync actual sizes from the realised window / framebuffer.
        var fb = _window.FramebufferSize;
        var sz = _window.Size;
        Width = Math.Max(1, sz.X);
        Height = Math.Max(1, sz.Y);
        _framebufferWidth = Math.Max(1, fb.X);
        _framebufferHeight = Math.Max(1, fb.Y);
        DpiScaleX = (float)_framebufferWidth / Width;
        DpiScaleY = (float)_framebufferHeight / Height;

        lock (_surfaceLock)
        {
            Surface.Dispose();
            Surface = CreateSurface(_framebufferWidth, _framebufferHeight);
        }

        InitGl();

        Console.WriteLine($"[SilkService] Loaded. Logical {Width}x{Height}, Framebuffer {_framebufferWidth}x{_framebufferHeight}, Scale {DpiScaleX:0.##}x{DpiScaleY:0.##}");
    }

    private void OnResize(Vector2D<int> size)
    {
        var nextWidth = Math.Max(1, size.X);
        var nextHeight = Math.Max(1, size.Y);

        if (Width == nextWidth && Height == nextHeight)
        {
            return;
        }

        Width = nextWidth;
        Height = nextHeight;

        // Keep DPI scales coherent even when framebuffer size is unchanged.
        var fb = _window.FramebufferSize;
        _framebufferWidth = Math.Max(1, fb.X);
        _framebufferHeight = Math.Max(1, fb.Y);
        DpiScaleX = (float)_framebufferWidth / Width;
        DpiScaleY = (float)_framebufferHeight / Height;

        // Logical-size changes must notify YogaWindow even if framebuffer resize does not fire.
        _surfaceDirty = true;
        SurfaceResized?.Invoke();
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        var fbW = Math.Max(1, size.X);
        var fbH = Math.Max(1, size.Y);

        _framebufferWidth = fbW;
        _framebufferHeight = fbH;
        DpiScaleX = (float)_framebufferWidth / Width;
        DpiScaleY = (float)_framebufferHeight / Height;

        _gl.Viewport(0, 0, (uint)fbW, (uint)fbH);

        lock (_surfaceLock)
        {
            var oldSurface = Surface;
            Surface = CreateSurface(fbW, fbH);

            // Preserve previous frame to avoid black flash.
            using var oldImage = oldSurface.Snapshot();
            if (oldImage is not null)
            {
                Surface.Canvas.Clear(SKColors.White);
                Surface.Canvas.DrawImage(oldImage, new SKRect(0, 0, fbW, fbH));
                Surface.Flush();
            }

            oldSurface.Dispose();
            _surfaceDirty = true;
        }

        EnsureTextureStorage(fbW, fbH);
        SurfaceResized?.Invoke();

        Console.WriteLine($"[SilkService.OnFramebufferResize] Logical {Width}x{Height}, Framebuffer {_framebufferWidth}x{_framebufferHeight}, Scale {DpiScaleX:0.##}x{DpiScaleY:0.##}");
    }

    private void OnUpdate(double deltaTime)
    {
        ApplyPendingResizeRequest();
        EmitPendingScrollWheel();

        var delta = (float)Math.Max(0.0, deltaTime);
        _elapsedSeconds += delta;
        FrameTick?.Invoke(delta, _elapsedSeconds);
    }

    private void OnRender(double deltaTime)
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        if (_surfaceDirty)
        {
            UploadSurfaceToTexture();
        }

        _gl.UseProgram(_shaderProgram);
        _gl.BindVertexArray(_vao);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _textureId);
        _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
    }

    private void OnClosing()
    {
        _isDisposed = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Input handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button != MouseButton.Left)
        {
            return;
        }

        MouseClicked?.Invoke(NormalizeMousePosition(mouse.Position));
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button != MouseButton.Left)
        {
            return;
        }

        MouseReleased?.Invoke(NormalizeMousePosition(mouse.Position));
    }

    private void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
        lock (_scrollStateLock)
        {
            _lastMousePosition = position;
        }

        MouseMoved?.Invoke(NormalizeMousePosition(position));
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel scroll)
    {
        lock (_scrollStateLock)
        {
            _lastMousePosition = mouse.Position;
            _pendingScrollDeltaX += scroll.X;
            _pendingScrollDeltaY += scroll.Y;
        }
    }

    private void EmitPendingScrollWheel()
    {
        float deltaX;
        float deltaY;
        System.Numerics.Vector2 mousePosition;

        lock (_scrollStateLock)
        {
            deltaX = _pendingScrollDeltaX;
            deltaY = _pendingScrollDeltaY;
            mousePosition = _lastMousePosition;
            _pendingScrollDeltaX = 0f;
            _pendingScrollDeltaY = 0f;
        }

        if (Math.Abs(deltaX) < float.Epsilon && Math.Abs(deltaY) < float.Epsilon)
        {
            return;
        }

        MouseWheelScrolled?.Invoke(NormalizeMousePosition(mousePosition), deltaX, deltaY);
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        var isShift = keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight);
        KeyDownReceived?.Invoke(MapKey(key), isShift);
    }

    private void OnKeyChar(IKeyboard keyboard, char c)
    {
        if (c != '\0')
        {
            TextInputReceived?.Invoke(c.ToString());
        }
    }

    private SKPoint NormalizeMousePosition(System.Numerics.Vector2 pos)
    {
        var x = pos.X;
        var y = pos.Y;

        // If coordinates appear to be in framebuffer space, convert to logical.
        if (x > Width || y > Height)
        {
            x /= DpiScaleX;
            y /= DpiScaleY;
        }

        return new SKPoint(x, y);
    }

    private static BrutalistKey MapKey(Key key) => key switch
    {
        Key.Enter => BrutalistKey.Enter,
        Key.KeypadEnter => BrutalistKey.KeyPadEnter,
        Key.Backspace => BrutalistKey.Backspace,
        Key.Delete => BrutalistKey.Delete,
        Key.Left => BrutalistKey.Left,
        Key.Right => BrutalistKey.Right,
        Key.Up => BrutalistKey.Up,
        Key.Down => BrutalistKey.Down,
        Key.Tab => BrutalistKey.Tab,
        Key.ShiftLeft => BrutalistKey.LeftShift,
        Key.ShiftRight => BrutalistKey.RightShift,
        _ => BrutalistKey.Unknown,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Pending resize
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyPendingResizeRequest()
    {
        int? rW, rH;
        lock (_resizeRequestLock)
        {
            rW = _pendingResizeWidth;
            rH = _pendingResizeHeight;
            _pendingResizeWidth = null;
            _pendingResizeHeight = null;
        }

        if (rW is null || rH is null)
        {
            return;
        }

        if (_window.Size.X != rW.Value || _window.Size.Y != rH.Value)
        {
            _window.Size = new Vector2D<int>(rW.Value, rH.Value);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OpenGL helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void InitGl()
    {
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Disable(EnableCap.DepthTest);

        CreateShaders();
        CreateQuad();
        CreateTexture();
        EnsureTextureStorage(_framebufferWidth, _framebufferHeight);

        Console.WriteLine("[SilkService] OpenGL initialised.");
    }

    private unsafe void UploadSurfaceToTexture()
    {
        lock (_surfaceLock)
        {
            if (_framebufferWidth <= 0 || _framebufferHeight <= 0)
            {
                return;
            }

            var requiredSize = _framebufferWidth * _framebufferHeight * 4;
            if (_pixelBuffer is null || _pixelBuffer.Length < requiredSize)
            {
                _pixelBuffer = new byte[requiredSize];
            }

            var handle = GCHandle.Alloc(_pixelBuffer, GCHandleType.Pinned);
            try
            {
                var imageInfo = new SKImageInfo(
                    _framebufferWidth, _framebufferHeight,
                    SKColorType.Rgba8888, SKAlphaType.Premul);
                Surface.ReadPixels(imageInfo, handle.AddrOfPinnedObject(), _framebufferWidth * 4, 0, 0);

                _gl.BindTexture(TextureTarget.Texture2D, _textureId);
                _gl.TexSubImage2D(
                    TextureTarget.Texture2D,
                    0, 0, 0,
                    (uint)_framebufferWidth, (uint)_framebufferHeight,
                    PixelFormat.Rgba, PixelType.UnsignedByte,
                    (void*)handle.AddrOfPinnedObject());

                _surfaceDirty = false;
            }
            finally
            {
                handle.Free();
            }
        }
    }

    private unsafe void EnsureTextureStorage(int width, int height)
    {
        var w = Math.Max(1, width);
        var h = Math.Max(1, height);

        if (_textureWidth == w && _textureHeight == h)
        {
            return;
        }

        _gl.BindTexture(TextureTarget.Texture2D, _textureId);
        _gl.TexImage2D(
            TextureTarget.Texture2D, 0,
            InternalFormat.Rgba, (uint)w, (uint)h, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte,
            (void*)0);

        _textureWidth = w;
        _textureHeight = h;
    }

    private void CreateTexture()
    {
        _textureId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _textureId);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    private unsafe void CreateQuad()
    {
        float[] vertices =
        {
            -1f, -1f, 0f,  0f, 1f,
             1f, -1f, 0f,  1f, 1f,
            -1f,  1f, 0f,  0f, 0f,
             1f,  1f, 0f,  1f, 0f,
        };

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        fixed (float* ptr = vertices)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)),
                ptr,
                BufferUsageARB.StaticDraw);
        }

        var stride = (uint)(5 * sizeof(float));
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
    }

    private void CreateShaders()
    {
        const string vertSrc = """
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec2 aTexCoord;
            out vec2 vTexCoord;
            void main() {
                vTexCoord = aTexCoord;
                gl_Position = vec4(aPosition, 1.0);
            }
            """;

        const string fragSrc = """
            #version 330 core
            in vec2 vTexCoord;
            out vec4 FragColor;
            uniform sampler2D uTexture;
            void main() {
                FragColor = texture(uTexture, vTexCoord);
            }
            """;

        var vert = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vert, vertSrc);
        _gl.CompileShader(vert);

        var frag = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(frag, fragSrc);
        _gl.CompileShader(frag);

        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vert);
        _gl.AttachShader(_shaderProgram, frag);
        _gl.LinkProgram(_shaderProgram);

        _gl.DeleteShader(vert);
        _gl.DeleteShader(frag);

        _gl.UseProgram(_shaderProgram);
        _gl.Uniform1(_gl.GetUniformLocation(_shaderProgram, "uTexture"), 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static SKSurface CreateSurface(int width, int height)
    {
        var imageInfo = new SKImageInfo(
            Math.Max(1, width), Math.Max(1, height),
            SKColorType.Rgba8888, SKAlphaType.Premul);
        return SKSurface.Create(imageInfo)
            ?? throw new InvalidOperationException("Failed to create SKSurface.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IDisposable
    // ─────────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Console.WriteLine("[SilkService] Disposing...");

        lock (_surfaceLock)
        {
            Surface.Dispose();
        }

        if (_textureId != 0) _gl.DeleteTexture(_textureId);
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_shaderProgram != 0) _gl.DeleteProgram(_shaderProgram);

        _input?.Dispose();
        _gl?.Dispose();
        _window?.Dispose();

        Console.WriteLine("[SilkService] Disposed.");
    }
}
