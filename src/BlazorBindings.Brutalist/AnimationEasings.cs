namespace BlazorBindings.Brutalist;

public static class AnimationEasings
{
    public static float Linear(float t) => t;

    public static float EaseInOutQuad(float t)
    {
        if (t < 0.5f)
        {
            return 2f * t * t;
        }

        var inv = -2f * t + 2f;
        return 1f - ((inv * inv) / 2f);
    }

    public static float EaseOutCubic(float t)
    {
        var inv = 1f - t;
        return 1f - (inv * inv * inv);
    }
}
