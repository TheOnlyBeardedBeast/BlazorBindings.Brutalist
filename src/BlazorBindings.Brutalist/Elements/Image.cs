using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BlazorBindings.Brutalist.Elements;

public unsafe class YogaImage : YogaView, IDisposable
{
    private enum ImageFitMode
    {
        Contain,
        Cover,
        Fill,
    }

    private static readonly HttpClient HttpClient = new();
    private static readonly MemoryCache ByteCache = new(new MemoryCacheOptions());

    public static TimeSpan CacheLifetime { get; set; } = TimeSpan.FromMinutes(10);

    [Parameter]
    public string? Source { get; set; }

    [Parameter]
    public string? Path { get; set; }

    [Parameter]
    public string? Url { get; set; }

    [Parameter]
    public string? Fit { get; set; }

    private readonly object _loadLock = new();
    private string? _resolvedSource;
    private ImageFitMode _fitMode = ImageFitMode.Contain;
    private SKBitmap? _bitmap;
    private bool _loadAttempted;

    private sealed record CacheEntry(byte[]? Bytes);

    public YogaImage()
    {
        Yoga.YG.NodeSetMeasureFunc(Node, &MeasureNode);
    }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        var setBaseParametersTask = base.SetParametersAsync(parameters);
        if (!setBaseParametersTask.IsCompletedSuccessfully)
        {
            return setBaseParametersTask.ContinueWith(
                static (task, state) =>
                {
                    task.GetAwaiter().GetResult();
                    ((YogaImage)state!).ApplySourceParameters();
                },
                this,
                System.Threading.CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        ApplySourceParameters();
        return Task.CompletedTask;
    }

    private void ApplySourceParameters()
    {
        var nextSource = ResolveSource();
        var nextFitMode = ResolveFitMode();

        var shouldMarkDirty = false;

        if (!string.Equals(_resolvedSource, nextSource, StringComparison.Ordinal))
        {
            _resolvedSource = nextSource;
            ResetLoadedBitmap();
            shouldMarkDirty = true;
        }

        if (_fitMode != nextFitMode)
        {
            _fitMode = nextFitMode;
            shouldMarkDirty = true;
        }

        if (shouldMarkDirty)
        {
            Yoga.YG.NodeMarkDirty(Node);
        }
    }

    public override void RenderSkia()
    {
        base.RenderSkia();

        EnsureBitmapLoaded();
        if (_bitmap is null)
        {
            return;
        }

        var offset = GetOffset();
        var width = Yoga.YG.NodeLayoutGetWidth(Node);
        var height = Yoga.YG.NodeLayoutGetHeight(Node);

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var containerRect = SKRect.Create(offset.left, offset.top, width, height);
        var destination = CreateContainedDestinationRect(containerRect, _bitmap.Width, _bitmap.Height);

        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        using var paint = new SKPaint
        {
            IsAntialias = true,
        };

        switch (_fitMode)
        {
            case ImageFitMode.Fill:
                OpenTkService.Canvas.DrawBitmap(_bitmap, containerRect, paint);
                break;

            case ImageFitMode.Cover:
                var sourceRect = CreateCoverSourceRect(containerRect, _bitmap.Width, _bitmap.Height);
                if (sourceRect.Width > 0 && sourceRect.Height > 0)
                {
                    OpenTkService.Canvas.DrawBitmap(_bitmap, sourceRect, containerRect, paint);
                }
                break;

            default:
                OpenTkService.Canvas.DrawBitmap(_bitmap, destination, paint);
                break;
        }
    }

    private string? ResolveSource()
    {
        if (!string.IsNullOrWhiteSpace(Source))
        {
            return Source.Trim();
        }

        if (!string.IsNullOrWhiteSpace(Url))
        {
            return Url.Trim();
        }

        if (!string.IsNullOrWhiteSpace(Path))
        {
            return Path.Trim();
        }

        return null;
    }

    private ImageFitMode ResolveFitMode()
    {
        if (string.IsNullOrWhiteSpace(Fit))
        {
            return ImageFitMode.Contain;
        }

        var value = Fit.Trim().Replace("-", string.Empty).Replace("_", string.Empty);

        return value.ToLowerInvariant() switch
        {
            "cover" => ImageFitMode.Cover,
            "fill" => ImageFitMode.Fill,
            _ => ImageFitMode.Contain,
        };
    }

    private void EnsureBitmapLoaded()
    {
        if (_loadAttempted)
        {
            return;
        }

        lock (_loadLock)
        {
            if (_loadAttempted)
            {
                return;
            }

            _loadAttempted = true;

            if (string.IsNullOrWhiteSpace(_resolvedSource))
            {
                return;
            }

            try
            {
                _bitmap = LoadBitmap(_resolvedSource);
            }
            catch
            {
                _bitmap = null;
            }
        }
    }

    private static SKBitmap? LoadBitmap(string source)
    {
        if (TryGetHttpUri(source, out var networkUri))
        {
            var bytes = GetCachedBytes(source, () => DownloadBytes(networkUri));
            return bytes is null ? null : SKBitmap.Decode(bytes);
        }

        if (TryGetFilePath(source, out var filePath))
        {
            if (!System.IO.File.Exists(filePath))
            {
                return null;
            }

            var bytes = GetCachedBytes(filePath, () => System.IO.File.ReadAllBytes(filePath));
            return bytes is null ? null : SKBitmap.Decode(bytes);
        }

        return null;
    }

    private static byte[]? GetCachedBytes(string key, Func<byte[]?> loadBytes)
    {
        if (ByteCache.TryGetValue(key, out var cachedValue) && cachedValue is CacheEntry existing)
        {
            return existing.Bytes;
        }

        var bytes = loadBytes();
        ByteCache.Set(
            key,
            new CacheEntry(bytes),
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = GetNormalizedCacheLifetime(),
            });

        return bytes;
    }

    private static TimeSpan GetNormalizedCacheLifetime()
    {
        return CacheLifetime <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(1)
            : CacheLifetime;
    }

    private static bool TryGetHttpUri(string source, out Uri uri)
    {
        uri = null!;
        var isAbsolute = Uri.TryCreate(source, UriKind.Absolute, out var parsedUri);
        if (!isAbsolute || parsedUri is null)
        {
            return false;
        }

        uri = parsedUri;

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetFilePath(string source, out string filePath)
    {
        filePath = source;

        if (Uri.TryCreate(source, UriKind.Absolute, out var absoluteUri) && absoluteUri is not null)
        {
            if (absoluteUri.IsFile)
            {
                filePath = absoluteUri.LocalPath;
                return true;
            }

            return false;
        }

        if (System.IO.Path.IsPathRooted(source))
        {
            filePath = source;
            return true;
        }

        var fullPath = System.IO.Path.GetFullPath(source);
        filePath = fullPath;
        return true;
    }

    private static byte[]? DownloadBytes(Uri uri)
    {
        try
        {
            return HttpClient.GetByteArrayAsync(uri).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    private void ResetLoadedBitmap()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        _loadAttempted = false;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static Yoga.YGSize MeasureNode(
        Yoga.YGNode* node,
        float width,
        Yoga.YGMeasureMode widthMode,
        float height,
        Yoga.YGMeasureMode heightMode)
    {
        var size = new Yoga.YGSize { width = 0, height = 0 };

        var ptr = Yoga.YG.NodeGetContext(node);
        if (ptr is null)
        {
            return size;
        }

        var handle = GCHandle.FromIntPtr((IntPtr)ptr);
        if (handle.Target is not YogaImage element)
        {
            return size;
        }

        element.EnsureBitmapLoaded();
        if (element._bitmap is null)
        {
            return size;
        }

        var measuredWidth = (float)element._bitmap.Width;
        var measuredHeight = (float)element._bitmap.Height;

        var fittedSize = FitSizeWithinConstraints(
            measuredWidth,
            measuredHeight,
            width,
            widthMode,
            height,
            heightMode);

        size.width = fittedSize.width;
        size.height = fittedSize.height;

        return size;
    }

    private static Yoga.YGSize FitSizeWithinConstraints(
        float sourceWidth,
        float sourceHeight,
        float availableWidth,
        Yoga.YGMeasureMode widthMode,
        float availableHeight,
        Yoga.YGMeasureMode heightMode)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return new Yoga.YGSize { width = 0, height = 0 };
        }

        var widthLimit = widthMode == Yoga.YGMeasureMode.YGMeasureModeUndefined
            ? float.PositiveInfinity
            : Math.Max(0f, availableWidth);

        var heightLimit = heightMode == Yoga.YGMeasureMode.YGMeasureModeUndefined
            ? float.PositiveInfinity
            : Math.Max(0f, availableHeight);

        var widthScale = float.IsPositiveInfinity(widthLimit) ? float.PositiveInfinity : widthLimit / sourceWidth;
        var heightScale = float.IsPositiveInfinity(heightLimit) ? float.PositiveInfinity : heightLimit / sourceHeight;
        var scale = Math.Min(widthScale, heightScale);

        if (float.IsPositiveInfinity(scale) || float.IsNaN(scale))
        {
            scale = 1f;
        }

        if (scale < 0f)
        {
            scale = 0f;
        }

        return new Yoga.YGSize
        {
            width = sourceWidth * scale,
            height = sourceHeight * scale,
        };
    }

    private static SKRect CreateContainedDestinationRect(SKRect container, int sourceWidth, int sourceHeight)
    {
        if (container.Width <= 0 || container.Height <= 0 || sourceWidth <= 0 || sourceHeight <= 0)
        {
            return SKRect.Empty;
        }

        var sourceAspect = (float)sourceWidth / sourceHeight;
        var containerAspect = container.Width / container.Height;

        float drawWidth;
        float drawHeight;

        if (sourceAspect > containerAspect)
        {
            drawWidth = container.Width;
            drawHeight = drawWidth / sourceAspect;
        }
        else
        {
            drawHeight = container.Height;
            drawWidth = drawHeight * sourceAspect;
        }

        var left = container.Left + ((container.Width - drawWidth) / 2f);
        var top = container.Top + ((container.Height - drawHeight) / 2f);
        return SKRect.Create(left, top, drawWidth, drawHeight);
    }

    private static SKRect CreateCoverSourceRect(SKRect container, int sourceWidth, int sourceHeight)
    {
        if (container.Width <= 0 || container.Height <= 0 || sourceWidth <= 0 || sourceHeight <= 0)
        {
            return SKRect.Empty;
        }

        var sourceW = (float)sourceWidth;
        var sourceH = (float)sourceHeight;
        var scale = Math.Max(container.Width / sourceW, container.Height / sourceH);

        if (scale <= 0 || float.IsNaN(scale) || float.IsInfinity(scale))
        {
            return SKRect.Empty;
        }

        var cropWidth = container.Width / scale;
        var cropHeight = container.Height / scale;
        var cropLeft = (sourceW - cropWidth) / 2f;
        var cropTop = (sourceH - cropHeight) / 2f;

        return SKRect.Create(cropLeft, cropTop, cropWidth, cropHeight);
    }

    public void Dispose()
    {
        ResetLoadedBitmap();
    }
}
