// NOTE: "= value" defaults are documentation only (not compiled in); set them from C#.
// Chromatic aberration: a cheap lens. Red and blue are pushed apart along the line from the center of the screen, more so towards the
// edges, so edges of things get a red fringe on one side and a blue one on the other (green stays put).
sampler TextureSampler : register(s0);

float2 Resolution;     // the picture it reads
float2 OutputSize;     // what it draws to
float Amount;          // how far red and blue move at the corners, as a share of the screen (0.006 is subtle, 0.02 strong)
float Falloff;         // 1 = grows evenly with the distance from the center, 2 = mostly at the edges

// Pixel-art friendly smooth sampling (see Wiggle.fx): texels stay crisp, their edges are blended over one output pixel.
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

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float2 fromCenter = uv - 0.5;
    float dist = length(fromCenter) / 0.7071;                     // 0 in the middle, 1 in the corners
    float2 shift = fromCenter * Amount * pow(saturate(dist), max(Falloff, 0.01)) * 2.0;

    float r = tex2D(TextureSampler, SharpUV(uv + shift)).r;
    float g = tex2D(TextureSampler, SharpUV(uv)).g;
    float b = tex2D(TextureSampler, SharpUV(uv - shift)).b;
    return float4(r, g, b, 1.0);
}

technique ChromaticAberration
{
    pass P0
    {
        PixelShader = compile ps_3_0 main();
    }
}
