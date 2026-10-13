using System;

namespace MonoFlixel;

/// <summary>
/// Easing functions: they take a progress from 0 to 1 and return the eased progress (also 0 to 1, or a little
/// beyond for Back / Elastic). Pass them to tweens: <c>new TweenOptions { Ease = FlxEase.BounceOut }</c>.
/// </summary>
public static class FlxEase
{
    const float PiHalf = MathF.PI / 2;
    const float Elastic = 1f, ElasticPeriod = 0.4f;

    public static float Linear(float t) => t;

    public static float QuadIn(float t) => t * t;
    public static float QuadOut(float t) => -t * (t - 2);
    public static float QuadInOut(float t) => t <= 0.5f ? t * t * 2 : 1 - (--t) * t * 2;

    public static float CubeIn(float t) => t * t * t;
    public static float CubeOut(float t) => 1 + (--t) * t * t;
    public static float CubeInOut(float t) => t <= 0.5f ? t * t * t * 4 : 1 + (--t) * t * t * 4;

    public static float QuartIn(float t) => t * t * t * t;
    public static float QuartOut(float t) => 1 - (t -= 1) * t * t * t;
    public static float QuartInOut(float t) => t <= 0.5f ? t * t * t * t * 8 : (1 - (t = t * 2 - 2) * t * t * t) / 2 + 0.5f;

    public static float QuintIn(float t) => t * t * t * t * t;
    public static float QuintOut(float t) => (t = t - 1) * t * t * t * t + 1;
    public static float QuintInOut(float t) => t <= 0.5f ? t * t * t * t * t * 16 : ((t = t * 2 - 2) * t * t * t * t + 2) / 2;

    public static float SmoothStepIn(float t) => 2 * SmoothStepInOut(t / 2);
    public static float SmoothStepOut(float t) => 2 * SmoothStepInOut(0.5f + t / 2) - 1;
    public static float SmoothStepInOut(float t) => t * t * (t * -2 + 3);

    public static float SmootherStepIn(float t) => 2 * SmootherStepInOut(t / 2);
    public static float SmootherStepOut(float t) => 2 * SmootherStepInOut(0.5f + t / 2) - 1;
    public static float SmootherStepInOut(float t) => t * t * t * (t * (t * 6 - 15) + 10);

    public static float SineIn(float t) => -MathF.Cos(PiHalf * t) + 1;
    public static float SineOut(float t) => MathF.Sin(PiHalf * t);
    public static float SineInOut(float t) => -MathF.Cos(MathF.PI * t) / 2 + 0.5f;

    public static float BounceIn(float t) => 1 - BounceOut(1 - t);
    public static float BounceOut(float t)
    {
        if (t < 1 / 2.75f) return 7.5625f * t * t;
        if (t < 2 / 2.75f) return 7.5625f * (t -= 1.5f / 2.75f) * t + 0.75f;
        if (t < 2.5f / 2.75f) return 7.5625f * (t -= 2.25f / 2.75f) * t + 0.9375f;
        return 7.5625f * (t -= 2.625f / 2.75f) * t + 0.984375f;
    }
    public static float BounceInOut(float t) => t < 0.5f ? (1 - BounceOut(1 - t * 2)) / 2 : BounceOut(t * 2 - 1) / 2 + 0.5f;

    public static float CircIn(float t) => -(MathF.Sqrt(1 - t * t) - 1);
    public static float CircOut(float t) => MathF.Sqrt(1 - (t - 1) * (t - 1));
    public static float CircInOut(float t) =>
        t <= 0.5f ? (MathF.Sqrt(1 - t * t * 4) - 1) / -2 : (MathF.Sqrt(1 - (t * 2 - 2) * (t * 2 - 2)) + 1) / 2;

    public static float ExpoIn(float t) => MathF.Pow(2, 10 * (t - 1));
    public static float ExpoOut(float t) => -MathF.Pow(2, -10 * t) + 1;
    public static float ExpoInOut(float t) =>
        t < 0.5f ? MathF.Pow(2, 10 * (t * 2 - 1)) / 2 : (-MathF.Pow(2, -10 * (t * 2 - 1)) + 2) / 2;

    public static float BackIn(float t) => t * t * (2.70158f * t - 1.70158f);
    public static float BackOut(float t) => 1 - (t -= 1) * t * (-2.70158f * t - 1.70158f);
    public static float BackInOut(float t)
    {
        t *= 2;
        if (t < 1) return t * t * (2.70158f * t - 1.70158f) / 2;
        t -= 2;
        return (1 - t * t * (-2.70158f * t - 1.70158f)) / 2 + 0.5f;
    }

    public static float ElasticIn(float t) =>
        -(Elastic * MathF.Pow(2, 10 * (t -= 1)) * MathF.Sin((t - ElasticPeriod / (2 * MathF.PI) * MathF.Asin(1 / Elastic)) * (2 * MathF.PI) / ElasticPeriod));
    public static float ElasticOut(float t) =>
        Elastic * MathF.Pow(2, -10 * t) * MathF.Sin((t - ElasticPeriod / (2 * MathF.PI) * MathF.Asin(1 / Elastic)) * (2 * MathF.PI) / ElasticPeriod) + 1;
    public static float ElasticInOut(float t)
    {
        if (t < 0.5f)
            return -0.5f * (Elastic * MathF.Pow(2, 10 * (t -= 0.5f)) * MathF.Sin((t - ElasticPeriod / 4) * (2 * MathF.PI) / ElasticPeriod));
        return Elastic * MathF.Pow(2, -10 * (t -= 0.5f)) * MathF.Sin((t - ElasticPeriod / 4) * (2 * MathF.PI) / ElasticPeriod) * 0.5f + 1;
    }
}
