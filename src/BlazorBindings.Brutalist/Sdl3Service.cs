using OpenTK.Graphics.OpenGL4;
using SDL3;
using SkiaSharp;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;

namespace BlazorBindings.Brutalist;

/// <summary>
/// An <see cref="IBrutalistRenderSurface"/> implementation backed by SDL3 for windowing/input
/// and OpenGL 3.3 (via OpenTK bindings) for accelerated texture blitting.
/// Drop-in replacement for <see cref="OpentkService"/>.
/// </summary>
public sealed class Sdl3Service : IBrutalistRenderSurface, IDisposable
{
    // ─── SDL3 handles ────────────────────────────────────────────────────────
    private nint _window;
    private nint _glContext;
    private nint _defaultCursor;
    private nint _pointerCursor;

    // ─── Skia surface ────────────────────────────────────────────────────────
    private readonly object _surfaceLock = new();
    private byte[]? _pixelBuffer;
    private volatile bool _surfaceDirty = true;
    private int _textureWidth;
    private int _textureHeight;

    // ─── OpenGL resources ────────────────────────────────────────────────────
    private int _textureId;
    private int _vao;
    private int _vbo;
    private int _shaderProgram;

    // ─── Layout state ────────────────────────────────────────────────────────
    private int _framebufferWidth;
    private int _framebufferHeight;
    private double _elapsedSeconds;
    private volatile bool _running;
    private Thread? _renderThread;
    private bool _isPointerCursor;
    private bool _isDisposed;

    private readonly object _resizeRequestLock = new();
    private int? _pendingResizeWidth;
    private int? _pendingResizeHeight;

    // ─── IBrutalistRenderSurface events ──────────────────────────────────────
    public event Action? SurfaceResized;
    public event Action<SKPoint>? MouseClicked;
    public event Action<SKPoint>? MouseReleased;
    public event Action<SKPoint>? MouseMoved;
    public event Action<SKPoint, float, float>? MouseWheelScrolled;
    public event Action<string>? TextInputReceived;
    public event Action<BrutalistKey, bool>? KeyDownReceived;
    public event Action<float, double>? FrameTick;

    // ─── IBrutalistRenderSurface properties ──────────────────────────────────
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float DpiScaleX { get; private set; } = 1f;
    public float DpiScaleY { get; private set; } = 1f;
    public SKSurface Surface { get; private set; } = null!;
    public SKCanvas Canvas => Surface.Canvas;

    // ─────────────────────────────────────────────────────────────────────────

    public Sdl3Service(int width = 800, int height = 600, string title = "Blazor SDL3 App")
    {

        if (!SDL.Init(SDL.InitFlags.Video))
        {
            throw new InvalidOperationException($"SDL_Init failed: {SDL.GetError()}");
        }

        // Request OpenGL 3.3 Core
        SDL.GLSetAttribute(SDL.GLAttr.ContextMajorVersion, 3);
        SDL.GLSetAttribute(SDL.GLAttr.ContextMinorVersion, 3);
        SDL.GLSetAttribute(SDL.GLAttr.ContextProfileMask,
            (int)SDL.GLProfile.Core);
        SDL.GLSetAttribute(SDL.GLAttr.DoubleBuffer, 1);

        var flags = SDL.WindowFlags.OpenGL
                  | SDL.WindowFlags.Resizable
                  | SDL.WindowFlags.HighPixelDensity;

        _window = SDL.CreateWindow(title, width, height, flags);
        if (_window == 0)
        {
            SDL.Quit();
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SDL.GetError()}");
        }

        // Compute logical / framebuffer sizes
        SDL.GetWindowSize(_window, out var logW, out var logH);
        SDL.GetWindowSizeInPixels(_window, out var fbW, out var fbH);

        Width = Math.Max(1, logW);
        Height = Math.Max(1, logH);
        _framebufferWidth = Math.Max(1, fbW);
        _framebufferHeight = Math.Max(1, fbH);
        DpiScaleX = (float)_framebufferWidth / Width;
        DpiScaleY = (float)_framebufferHeight / Height;

        Surface = CreateSurface(_framebufferWidth, _framebufferHeight);

        // Pre-create system cursors (fast, no allocation at event time)
        _defaultCursor = SDL.CreateSystemCursor(SDL.SystemCursor.Default);
        _pointerCursor = SDL.CreateSystemCursor(SDL.SystemCursor.Pointer);

    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void Start()
    {
        if (OperatingSystem.IsMacOS())
        {
            // macOS requires OpenGL and SDL to run on the main thread.
            Run();
        }
        else
        {
            _renderThread = new Thread(Run) { IsBackground = true, Name = "SDL3RenderThread" };
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
        SDL.SetCursor(enabled ? _pointerCursor : _defaultCursor);
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
            }
        }
        catch (Exception)
        {
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Main run loop
    // ─────────────────────────────────────────────────────────────────────────

    private void Run()
    {
        // Create GL context on the thread that will drive it (this thread).
        _glContext = SDL.GLCreateContext(_window);
        if (_glContext == 0)
        {
            throw new InvalidOperationException($"SDL_GL_CreateContext failed: {SDL.GetError()}");
        }

        SDL.GLMakeCurrent(_window, _glContext);
        SDL.GLSetSwapInterval(1); // vsync on

        // Load OpenTK GL function pointers via SDL3's resolver.
        GL.LoadBindings(new Sdl3GlBindingsContext());

        InitGl();

        // Enable SDL text input for the window (SDL3 requires per-window opt-in)
        SDL.StartTextInput(_window);

        _running = true;
        var lastTimestamp = (double)SDL.GetTicks() / 1000.0;

        while (_running)
        {
            // Process all pending SDL events
            while (SDL.PollEvent(out var sdlEvent))
            {
                HandleSdlEvent(ref sdlEvent);
            }

            // Apply any programmatic resize requests
            ApplyPendingResizeRequest();

            // Frame tick
            var now = (double)SDL.GetTicks() / 1000.0;
            var delta = (float)Math.Max(0.0, now - lastTimestamp);
            lastTimestamp = now;
            _elapsedSeconds += delta;
            FrameTick?.Invoke(delta, _elapsedSeconds);

            // Render
            if (_surfaceDirty)
            {
                GL.Clear(ClearBufferMask.ColorBufferBit);
                UploadSurfaceToTexture();

                GL.UseProgram(_shaderProgram);
                GL.BindVertexArray(_vao);
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, _textureId);
                GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

                SDL.GLSwapWindow(_window);
            }
            else
            {
                // Nothing to draw — yield a timeslice so we don't busy-spin.
                Thread.Sleep(1);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SDL event handling
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleSdlEvent(ref SDL.Event e)
    {
        switch ((SDL.EventType)e.Type)
        {
            case SDL.EventType.Quit:
                _running = false;
                break;

            // ── Window events ──────────────────────────────────────────
            case SDL.EventType.WindowPixelSizeChanged:
                OnFramebufferResized(e.Window.Data1, e.Window.Data2);
                break;

            case SDL.EventType.WindowResized:
                SDL.GetWindowSizeInPixels(_window, out var fbW, out var fbH);
                OnFramebufferResized(fbW, fbH);
                break;

            // ── Mouse ──────────────────────────────────────────────────
            case SDL.EventType.MouseButtonDown:
                if (e.Button.Button == 1)
                {
                    MouseClicked?.Invoke(new SKPoint(e.Button.X, e.Button.Y));
                }
                break;

            case SDL.EventType.MouseButtonUp:
                if (e.Button.Button == 1)
                {
                    MouseReleased?.Invoke(new SKPoint(e.Button.X, e.Button.Y));
                }
                break;

            case SDL.EventType.MouseMotion:
                MouseMoved?.Invoke(new SKPoint(e.Motion.X, e.Motion.Y));
                break;

            case SDL.EventType.MouseWheel:
                // SDL3 wheel values are already in floating-point line units.
                // Negate Y so scrolling down produces a negative delta (matching OpenTK convention).
                SDL.GetMouseState(out var mx, out var my);
                MouseWheelScrolled?.Invoke(new SKPoint(mx, my), e.Wheel.X, -e.Wheel.Y);
                break;

            // ── Keyboard ───────────────────────────────────────────────
            case SDL.EventType.KeyDown:
                var brutalistKey = MapSdlKey((int)e.Key.Key);
                var isShift = (e.Key.Mod & SDL.Keymod.Shift) != 0;
                KeyDownReceived?.Invoke(brutalistKey, isShift);
                break;

            // ── Text input ─────────────────────────────────────────────
            case SDL.EventType.TextInput:
                var text = ReadTextInputEvent(ref e);
                if (!string.IsNullOrEmpty(text))
                {
                    TextInputReceived?.Invoke(text);
                }
                break;
        }
    }

    private static unsafe string? ReadTextInputEvent(ref SDL.Event e)
    {
        // SDL.TextInputEvent.Text is `char *` (UTF-8) in SDL3.
        // SDL3-CS exposes it as a byte* in the union; marshal via pointer arithmetic.
        fixed (SDL.Event* ep = &e)
        {
            var textEvent = (SDL.TextInputEvent*)ep;
            if (textEvent->Text == 0)
            {
                return null;
            }

            return Marshal.PtrToStringUTF8((nint)textEvent->Text);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Resize handling
    // ─────────────────────────────────────────────────────────────────────────

    private void OnFramebufferResized(int fbWidth, int fbHeight)
    {
        var safeW = Math.Max(1, fbWidth);
        var safeH = Math.Max(1, fbHeight);

        SDL.GetWindowSize(_window, out var logW, out var logH);
        Width = Math.Max(1, logW);
        Height = Math.Max(1, logH);
        _framebufferWidth = safeW;
        _framebufferHeight = safeH;
        DpiScaleX = (float)_framebufferWidth / Width;
        DpiScaleY = (float)_framebufferHeight / Height;

        GL.Viewport(0, 0, _framebufferWidth, _framebufferHeight);

        lock (_surfaceLock)
        {
            var oldSurface = Surface;
            Surface = CreateSurface(_framebufferWidth, _framebufferHeight);

            // Preserve previous frame to avoid black flash.
            using var oldImage = oldSurface.Snapshot();
            if (oldImage is not null)
            {
                Surface.Canvas.Clear(SKColors.White);
                Surface.Canvas.DrawImage(oldImage,
                    new SKRect(0, 0, _framebufferWidth, _framebufferHeight));
                Surface.Flush();
            }

            oldSurface.Dispose();
            _surfaceDirty = true;
        }

        EnsureTextureStorage(_framebufferWidth, _framebufferHeight);
        SurfaceResized?.Invoke();

    }

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

        SDL.GetWindowSize(_window, out var curW, out var curH);
        if (curW != rW.Value || curH != rH.Value)
        {
            SDL.SetWindowSize(_window, rW.Value, rH.Value);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OpenGL helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void InitGl()
    {
        GL.ClearColor(0f, 0f, 0f, 1f);
        GL.Disable(EnableCap.DepthTest);

        CreateShaders();
        CreateQuad();
        CreateTexture();
        EnsureTextureStorage(_framebufferWidth, _framebufferHeight);

    }

    private void UploadSurfaceToTexture()
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

                GL.BindTexture(TextureTarget.Texture2D, _textureId);
                GL.TexSubImage2D(
                    TextureTarget.Texture2D,
                    0, 0, 0,
                    _framebufferWidth, _framebufferHeight,
                    PixelFormat.Rgba, PixelType.UnsignedByte,
                    _pixelBuffer);

                _surfaceDirty = false;
            }
            finally
            {
                handle.Free();
            }
        }
    }

    private void EnsureTextureStorage(int width, int height)
    {
        var w = Math.Max(1, width);
        var h = Math.Max(1, height);

        if (_textureWidth == w && _textureHeight == h)
        {
            return;
        }

        GL.BindTexture(TextureTarget.Texture2D, _textureId);
        GL.TexImage2D(
            TextureTarget.Texture2D, 0,
            PixelInternalFormat.Rgba, w, h, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte,
            IntPtr.Zero);

        _textureWidth = w;
        _textureHeight = h;
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
        float[] vertices =
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

        var vert = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vert, vertSrc);
        GL.CompileShader(vert);

        var frag = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(frag, fragSrc);
        GL.CompileShader(frag);

        _shaderProgram = GL.CreateProgram();
        GL.AttachShader(_shaderProgram, vert);
        GL.AttachShader(_shaderProgram, frag);
        GL.LinkProgram(_shaderProgram);

        GL.DeleteShader(vert);
        GL.DeleteShader(frag);

        GL.UseProgram(_shaderProgram);
        GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "uTexture"), 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Key mapping
    // ─────────────────────────────────────────────────────────────────────────

    private static BrutalistKey MapSdlKey(int sdlKeycode) => sdlKeycode switch
    {
        0x0000000D => BrutalistKey.Enter,         // SDLK_RETURN
        0x40000058 => BrutalistKey.KeyPadEnter,   // SDLK_KP_ENTER
        0x00000008 => BrutalistKey.Backspace,     // SDLK_BACKSPACE
        0x00000009 => BrutalistKey.Tab,           // SDLK_TAB
        0x4000004C => BrutalistKey.Delete,        // SDLK_DELETE
        0x40000050 => BrutalistKey.Left,          // SDLK_LEFT
        0x4000004F => BrutalistKey.Right,         // SDLK_RIGHT
        0x40000052 => BrutalistKey.Up,            // SDLK_UP
        0x40000051 => BrutalistKey.Down,          // SDLK_DOWN
        0x400000E1 => BrutalistKey.LeftShift,     // SDLK_LSHIFT
        0x400000E5 => BrutalistKey.RightShift,    // SDLK_RSHIFT
        _ => BrutalistKey.Unknown,
    };

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
        _running = false;


        lock (_surfaceLock)
        {
            Surface.Dispose();
        }

        if (_textureId != 0) GL.DeleteTexture(_textureId);
        if (_vbo != 0) GL.DeleteBuffer(_vbo);
        if (_vao != 0) GL.DeleteVertexArray(_vao);
        if (_shaderProgram != 0) GL.DeleteProgram(_shaderProgram);

        if (_pointerCursor != 0) SDL.DestroyCursor(_pointerCursor);
        if (_defaultCursor != 0) SDL.DestroyCursor(_defaultCursor);

        if (_glContext != 0) SDL.GLDestroyContext(_glContext);
        if (_window != 0) SDL.DestroyWindow(_window);

        SDL.Quit();

    }

    // ─────────────────────────────────────────────────────────────────────────
    // OpenTK GL bindings context backed by SDL_GL_GetProcAddress
    // ─────────────────────────────────────────────────────────────────────────

    private sealed unsafe class Sdl3GlBindingsContext : OpenTK.IBindingsContext
    {
        public nint GetProcAddress(string procName)
        {
            return SDL_GL_GetProcAddress(procName);
        }

        [DllImport("SDL3", EntryPoint = "SDL_GL_GetProcAddress", ExactSpelling = true)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        private static extern nint SDL_GL_GetProcAddress([MarshalAs(UnmanagedType.LPUTF8Str)] string procName);
    }
}
