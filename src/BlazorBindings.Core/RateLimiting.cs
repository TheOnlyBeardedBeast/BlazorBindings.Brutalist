namespace BlazorBindings.Core;

public static class RateLimit
{
    public static Debouncer Debounce(TimeSpan delay) => new(delay);

    public static Throttler Throttle(TimeSpan interval, bool invokeTrailingCall = true)
        => new(interval, invokeTrailingCall);
}

public sealed class Debouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly object _gate = new();
    private CancellationTokenSource _cts;
    private bool _disposed;

    public Debouncer(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        _delay = delay;
    }

    public void Execute(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _ = ExecuteAsync(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    public Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfDisposed();

        CancellationTokenSource localCts;
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            localCts = _cts;
        }

        return RunAsync(localCts, action);
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task RunAsync(CancellationTokenSource localCts, Func<Task> action)
    {
        try
        {
            await Task.Delay(_delay, localCts.Token).ConfigureAwait(false);
            if (localCts.IsCancellationRequested)
            {
                return;
            }

            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_cts, localCts))
                {
                    _cts?.Dispose();
                    _cts = null;
                }
                else
                {
                    localCts.Dispose();
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cancel();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Debouncer));
        }
    }
}

public sealed class Throttler : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly bool _invokeTrailingCall;
    private readonly object _gate = new();

    private DateTimeOffset _lastExecutionUtc = DateTimeOffset.MinValue;
    private CancellationTokenSource _trailingCts;
    private Func<Task> _trailingAction;
    private bool _trailingScheduled;
    private bool _disposed;

    public Throttler(TimeSpan interval, bool invokeTrailingCall = true)
    {
        if (interval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        _interval = interval;
        _invokeTrailingCall = invokeTrailingCall;
    }

    public void Execute(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _ = ExecuteAsync(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    public Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfDisposed();

        var now = DateTimeOffset.UtcNow;
        bool runNow;
        TimeSpan trailingDelay = TimeSpan.Zero;
        CancellationTokenSource trailingCtsToStart = null;

        lock (_gate)
        {
            var elapsed = now - _lastExecutionUtc;
            if (elapsed >= _interval)
            {
                _lastExecutionUtc = now;
                CancelTrailing_NoLock();
                runNow = true;
            }
            else
            {
                runNow = false;

                if (_invokeTrailingCall)
                {
                    _trailingAction = action;

                    if (!_trailingScheduled)
                    {
                        _trailingScheduled = true;
                        trailingDelay = _interval - elapsed;
                        _trailingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        trailingCtsToStart = _trailingCts;
                    }
                }
            }
        }

        if (runNow)
        {
            return action();
        }

        if (trailingCtsToStart != null)
        {
            _ = RunTrailingAsync(trailingDelay, trailingCtsToStart);
        }

        return Task.CompletedTask;
    }

    public void Cancel()
    {
        lock (_gate)
        {
            CancelTrailing_NoLock();
        }
    }

    private async Task RunTrailingAsync(TimeSpan delay, CancellationTokenSource localCts)
    {
        try
        {
            await Task.Delay(delay, localCts.Token).ConfigureAwait(false);

            Func<Task> action;
            lock (_gate)
            {
                _trailingScheduled = false;
                action = _trailingAction;
                _trailingAction = null;

                if (ReferenceEquals(_trailingCts, localCts))
                {
                    _trailingCts = null;
                }

                _lastExecutionUtc = DateTimeOffset.UtcNow;
            }

            if (!localCts.IsCancellationRequested && action != null)
            {
                await action().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            localCts.Dispose();
        }
    }

    private void CancelTrailing_NoLock()
    {
        _trailingScheduled = false;
        _trailingAction = null;
        _trailingCts?.Cancel();
        _trailingCts?.Dispose();
        _trailingCts = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cancel();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Throttler));
        }
    }
}
