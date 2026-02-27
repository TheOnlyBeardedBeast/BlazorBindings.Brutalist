namespace BlazorBindings.Brutalist;

public sealed class AnimationScheduler
{
    private readonly object _gate = new();
    private readonly List<ActiveAnimation> _animations = [];

    public AnimationScheduler(AnimationTicker ticker)
    {
        ticker.Tick += OnTick;
    }

    public IDisposable Animate(
        float durationSeconds,
        Action<float> update,
        Func<float, float>? easing = null,
        Action? completed = null)
    {
        if (update is null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        var safeDuration = Math.Max(0.0001f, durationSeconds);
        var safeEasing = easing ?? AnimationEasings.Linear;

        var animation = new ActiveAnimation(safeDuration, update, safeEasing, completed);

        // Immediately publish initial value.
        update(0f);

        lock (_gate)
        {
            _animations.Add(animation);
        }

        return new CancelToken(this, animation.Id);
    }

    private void OnTick(float deltaSeconds, double elapsedSeconds)
    {
        List<ActiveAnimation>? finished = null;

        lock (_gate)
        {
            if (_animations.Count == 0)
            {
                return;
            }

            for (var i = _animations.Count - 1; i >= 0; i--)
            {
                var animation = _animations[i];
                animation.Elapsed += deltaSeconds;

                var t = Math.Clamp(animation.Elapsed / animation.DurationSeconds, 0f, 1f);
                var eased = Math.Clamp(animation.Easing(t), 0f, 1f);
                animation.Update(eased);

                if (t < 1f)
                {
                    continue;
                }

                _animations.RemoveAt(i);
                finished ??= [];
                finished.Add(animation);
            }
        }

        if (finished is null)
        {
            return;
        }

        foreach (var animation in finished)
        {
            animation.Completed?.Invoke();
        }
    }

    private void Cancel(Guid id)
    {
        lock (_gate)
        {
            for (var i = _animations.Count - 1; i >= 0; i--)
            {
                if (_animations[i].Id != id)
                {
                    continue;
                }

                _animations.RemoveAt(i);
                break;
            }
        }
    }

    private sealed class ActiveAnimation(
        float durationSeconds,
        Action<float> update,
        Func<float, float> easing,
        Action? completed)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public float DurationSeconds { get; } = durationSeconds;
        public float Elapsed { get; set; }
        public Action<float> Update { get; } = update;
        public Func<float, float> Easing { get; } = easing;
        public Action? Completed { get; } = completed;
    }

    private sealed class CancelToken(AnimationScheduler scheduler, Guid id) : IDisposable
    {
        private bool _isDisposed;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            scheduler.Cancel(id);
        }
    }
}
