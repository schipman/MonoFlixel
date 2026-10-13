// First step of FlxBloom: keep only the part of the picture that is brighter than Threshold.
// The result keeps the hue of the bright part (a bright orange stays orange, not white).
// NOTE: default values written in a .fx are not compiled in; FlxBloom sets Threshold from code.
sampler TextureSampler : register(s0);

float Threshold;

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float3 c = tex2D(TextureSampler, uv).rgb;
    float peak = max(c.r, max(c.g, c.b));
    float keep = max(peak - Threshold, 0.0) / max(0.001, 1.0 - Threshold);   // 0 below the threshold, up to 1 at full brightness
    float scale = peak > 0.0001 ? keep / peak : 0.0;
    return float4(c * scale, 1.0);
}

technique BloomBright
{
    pass P0
    {
        PixelShader = compile ps_3_0 main();
    }
}
