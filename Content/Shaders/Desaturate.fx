// NOTE: "= value" defaults are documentation only (not compiled in); set them from C#.
// Full-screen desaturate. Amount: 0 = original colours, 1 = greyscale.
sampler TextureSampler : register(s0);

float Amount = 1.0;

float4 main(float2 texCoord : TEXCOORD0) : COLOR0
{
    float4 tex = tex2D(TextureSampler, texCoord);
    float grey = dot(tex.rgb, float3(0.3, 0.59, 0.11));
    tex.rgb = lerp(tex.rgb, grey.xxx, Amount);
    return tex;
}

technique Desaturate
{
    pass P0
    {
        PixelShader = compile ps_2_0 main();
    }
}
