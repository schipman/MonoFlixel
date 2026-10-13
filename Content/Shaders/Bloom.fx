// NOTE: the "= value" defaults below are documentation only (they are not compiled in); set them from C#.
// Single-pass bloom: keep what is brighter than Threshold, blur it and add it back on top.
// TexelSize (1/width, 1/height of the source) is filled in by FlxPostProcess.
// Use a Linear sampler (FlxPostProcess.Sampler = SamplerState.LinearClamp) so the glow is smooth;
// the scene itself is read at texel centres, so the pixel art stays crisp.
sampler TextureSampler : register(s0);

float2 TexelSize = float2(1.0 / 320.0, 1.0 / 240.0);
float Threshold = 0.5;
float Intensity = 1.2;
float Radius = 2.0;          // distance between taps, in source pixels

float3 Bright(float2 uv)
{
    float3 c = tex2D(TextureSampler, uv).rgb;
    return max(c - Threshold, 0.0) / (1.0 - Threshold);
}

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    // pixel-crisp scene: sample exactly at the centre of the source texel
    float2 centre = (floor(uv / TexelSize) + 0.5) * TexelSize;
    float4 scene = tex2D(TextureSampler, centre);

    // smooth glow: wide gaussian over bilinear taps, sampled at the true (unsnapped) uv so it varies per window pixel
    float3 glow = 0;
    float total = 0;
    for (int y = -4; y <= 4; y++)
    {
        for (int x = -4; x <= 4; x++)
        {
            float w = exp(-(x * x + y * y) / 10.0);
            glow += Bright(uv + float2(x, y) * Radius * 0.75 * TexelSize) * w;
            total += w;
        }
    }

    scene.rgb += glow / total * Intensity * 5.0;
    return scene;
}

technique Bloom
{
    pass P0
    {
        PixelShader = compile ps_3_0 main();
    }
}
