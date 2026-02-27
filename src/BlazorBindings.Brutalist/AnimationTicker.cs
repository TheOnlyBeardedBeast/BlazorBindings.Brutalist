namespace BlazorBindings.Brutalist;

public sealed class AnimationTicker
{
    public float DeltaSeconds { get; private set; }
    public double ElapsedSeconds { get; private set; }

    public event Action<float, double>? Tick;

    public AnimationTicker(IBrutalistRenderSurface renderSurface)
    {
        renderSurface.FrameTick += OnFrameTick;
    }

    private void OnFrameTick(float deltaSeconds, double elapsedSeconds)
    {
        DeltaSeconds = deltaSeconds;
        ElapsedSeconds = elapsedSeconds;
        Tick?.Invoke(deltaSeconds, elapsedSeconds);
    }
}
