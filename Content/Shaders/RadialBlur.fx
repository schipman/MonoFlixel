// NOTE: "= value" defaults are documentation only (not compiled in); set them from C#.
// Radial (zoom) blur: every pixel is smeared along the line towards Center. Nothing happens near the center (Inner), and the
// blur grows towards the edges, like speed lines or a zoom through the lens.
sampler TextureSampler : register(s0);

float2 Center;      // in uv (0.5, 0.5 is the middle of the screen)
float Strength;     // how much of the way to the center the blur reaches: 0.05 mild, 0.3 strong
float Inner;        // radius (in uv) of the clear area around the center

static const int SAMPLES = 20;

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float2 dir = uv - Center;
    float dist = length(dir);
    float k = saturate((dist - Inner) / max(0.0001, 0.75 - Inner));

    float3 sum = 0;
    for (int i = 0; i < SAMPLES; i++)
    {
        float t = i / (SAMPLES - 1.0);
        sum += tex2D(TextureSampler, uv - dir * (Strength * k * t)).rgb;
    }
    return float4(sum / SAMPLES, 1.0);
}

technique RadialBlur
{
    pass P0
    {
        PixelShader = compile ps_3_0 main();
    }
}
