// NOTE: "= value" defaults are documentation only (not compiled in); set them from C#.
// Night vision: a green image intensifier. Brightness is boosted (dark things are lifted), the picture is tinted green, grainy,
// glows a little around bright spots, flickers and is cut off in a round lens with dark corners.
sampler TextureSampler : register(s0);

float fTimer;
float2 Resolution;
float Gain;            // how much brighter (2 is a good start)
float Noise;           // amount of grain (0.15 normal)
float Contrast;        // 1 = none
float VignetteSize;    // radius of the lens (in screen height units); 0.6 nearly fills a wide screen

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float2 px = 1.0 / Resolution;

    // the picture, with a little glow from the neighbours
    float3 c = tex2D(TextureSampler, uv).rgb * 0.6
             + tex2D(TextureSampler, uv + float2(px.x * 1.5, 0)).rgb * 0.1
             + tex2D(TextureSampler, uv - float2(px.x * 1.5, 0)).rgb * 0.1
             + tex2D(TextureSampler, uv + float2(0, px.y * 1.5)).rgb * 0.1
             + tex2D(TextureSampler, uv - float2(0, px.y * 1.5)).rgb * 0.1;
    float lum = dot(c, float3(0.3, 0.59, 0.11));

    lum = saturate((lum - 0.5) * Contrast + 0.5) * Gain;
    lum = lum / (1.0 + lum * 0.35);                         // soft limit, so bright spots bloom instead of clipping

    float grain = Hash(uv * Resolution + frac(fTimer) * 91.7);
    lum += (grain - 0.5) * Noise * (0.4 + lum);

    float scan = 0.93 + 0.07 * sin(uv.y * Resolution.y * 3.14159);
    float flicker = 1.0 + 0.04 * sin(fTimer * 53.0) + 0.02 * sin(fTimer * 17.0);

    // the round lens
    float2 d = (uv - 0.5) * float2(Resolution.x / Resolution.y, 1.0);
    float lens = 1.0 - smoothstep(VignetteSize, VignetteSize + 0.3, length(d));

    float3 green = float3(0.12, 1.0, 0.25) * lum * scan * flicker * lens;
    return float4(saturate(green), 1.0);
}

technique NightVision
{
    pass P0
    {
        PixelShader = compile ps_3_0 main();
    }
}
