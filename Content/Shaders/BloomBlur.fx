// Second step of FlxBloom: a 15 tap gaussian blur in one direction. FlxBloom runs it sideways, then up and down,
// a few times, on a texture the size of the window (or a fraction of it), so the glow is smooth at screen resolution.
// Direction is one texel step along the blur direction times the spread: (texel.x * spread, 0) or (0, texel.y * spread).
sampler TextureSampler : register(s0);

float2 Direction;

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float3 sum = 0;
    float total = 0;

    for (int i = -7; i <= 7; i++)
    {
        float w = exp(-(i * i) / 18.0);          // sigma = 3 taps
        sum += tex2D(TextureSampler, uv + Direction * i).rgb * w;
        total += w;
    }
    return float4(sum / total, 1.0);
}

technique BloomBlur
{
    pass P0
    {
        PixelShader = compile ps_3_0 main();
    }
}
