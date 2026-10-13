// NOTE: "= value" defaults are documentation only (not compiled in); set them from C#.
// VHS tape: colors smear sideways and split, the picture wobbles, a band of tracking noise rolls up the screen, the bottom edge
// tears (head switching), noise and stray white streaks, soft scanlines and a washed, warm color.
sampler TextureSampler : register(s0);

float fTimer;
float2 Resolution;
float Amount;          // 0 = clean, 1 = full tape
float Noise;           // grain (0.12 normal)
float ChromaOffset;    // how far red and blue are pushed apart, in pixels (2 normal)
float Jitter;          // wobble and tracking tear (1 normal)

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(TextureSampler, uv);
    float t = fTimer;
    float row = floor(uv.y * Resolution.y);
    float frame = floor(t * 30.0);

    // slow wobble of the whole picture
    float shift = sin(uv.y * 5.0 + t * 1.7) * 0.0006 + sin(uv.y * 41.0 - t * 5.0) * 0.00025;
    shift *= Jitter;

    // the tracking band: a stripe that rolls up the screen and tears the lines inside it
    float bandTop = frac(t * 0.11);
    float bd = frac(uv.y - bandTop);
    float band = smoothstep(0.0, 0.012, bd) * (1.0 - smoothstep(0.035, 0.07, bd));
    float rowRand = Hash(float2(row, frame));
    shift += band * (rowRand - 0.5) * 0.03 * Jitter;

    // the bottom of the picture tears (where the video heads switch)
    float bottom = smoothstep(0.925, 1.0, uv.y);
    shift += bottom * ((Hash(float2(row, frame + 7.0)) - 0.5) * 0.05 + 0.012) * Jitter;

    float2 p = float2(uv.x + shift, uv.y);
    float2 px = float2(1.0 / Resolution.x, 0.0);

    // colors bleed to the right and red and blue come apart
    float r = tex2D(TextureSampler, p + px * ChromaOffset).r;
    float g = tex2D(TextureSampler, p).g;
    float b = tex2D(TextureSampler, p - px * ChromaOffset).b;
    float3 c = float3(r, g, b);
    float3 smear = (tex2D(TextureSampler, p - px * 2.0).rgb + tex2D(TextureSampler, p - px * 4.0).rgb) * 0.5;
    c = lerp(c, smear, 0.25);

    // washed color, a little warm
    float lum = dot(c, float3(0.3, 0.59, 0.11));
    c = lerp(float3(lum, lum, lum), c, 0.8) * float3(1.04, 1.0, 0.93);
    c = (c - 0.5) * 0.92 + 0.52;

    // grain, the noise in the tracking band, and now and then a white streak
    float n = Hash(uv * Resolution + frac(t) * 71.3);
    c += (n - 0.5) * Noise;
    c += band * (n - 0.3) * 0.25;
    c += bottom * (n - 0.35) * 0.3;
    float streak = step(0.9985, Hash(float2(row, frame + 31.0)));
    c += streak * Hash(float2(floor(uv.x * 40.0), frame)) * 0.6;

    // soft scanlines
    c *= 0.93 + 0.07 * sin(uv.y * Resolution.y * 3.14159 * 2.0);

    return float4(lerp(original.rgb, saturate(c), Amount), 1.0);
}

technique Vhs
{
    pass P0
    {
        PixelShader = compile ps_3_0 main();
    }
}
