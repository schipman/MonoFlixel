// NOTE: "= value" defaults are documentation only (not compiled in); set them from C#.
// Water-style wobble. Based on Drown's Wiggle.fx, with tweakable strength.
sampler ColorMapSampler : register(s0);

float fTimer;
float2 Resolution;     // the picture it reads
float2 OutputSize;     // what it draws to
float Amplitude = 0.004;   // UV distance of the wobble (0.001 was the original)
float Frequency = 20.0;    // how many waves fit on screen
float Speed = 3.0;         // how fast it moves

// Pixel-art friendly smooth sampling: texels stay crisp, and only the edge between two texels is blended, over one pixel of the
// OUTPUT. Drawn at the size of the game screen (output = source) this is plain bilinear filtering, so a picture that is moved by
// less than a pixel moves smoothly instead of jumping from pixel to pixel; drawn larger it is crisp. Needs a linear sampler.
float2 SharpUV(float2 uv)
{
    float2 scale = max(OutputSize / Resolution, 1.0);
    float2 t = uv * Resolution;
    float2 i = floor(t);
    float2 d = frac(t) - 0.5;
    float2 region = 0.5 - 0.5 / scale;
    float2 f = (d - clamp(d, -region, region)) * scale + 0.5;
    return (i + f) / Resolution;
}

float4 main(float2 Tex: TEXCOORD0) : COLOR
{
    float t = fTimer * Speed;
    Tex.x += sin(t + Tex.y * Frequency) * Amplitude;
    Tex.y += cos(t + Tex.x * Frequency) * Amplitude;
    return tex2D(ColorMapSampler, SharpUV(Tex));
}

technique Wiggle
{
    pass P0
    {
        PixelShader = compile ps_2_0 main();
    }
}
