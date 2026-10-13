// NOTE: "= value" defaults are documentation only (not compiled in); set them from C#.
// Heat vision: a thermal camera. The picture is turned into a "temperature" (bright and reddish things are hot) and painted with
// the classic thermal palette: black, deep blue, purple, red, orange, yellow, white. A little shimmer and softness like a low
// resolution sensor.
sampler TextureSampler : register(s0);

float fTimer;
float2 Resolution;
float2 OutputSize;
float Amount;       // 0 = normal picture, 1 = full thermal
float Shimmer;      // heat haze, 0 = none, 1 = normal

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

float3 Palette(float t)
{
    t = saturate(t);
    float3 c = lerp(float3(0.0, 0.0, 0.12), float3(0.12, 0.0, 0.55), saturate(t * 5.0));
    c = lerp(c, float3(0.75, 0.0, 0.45), saturate(t * 5.0 - 1.0));
    c = lerp(c, float3(1.0, 0.18, 0.0), saturate(t * 5.0 - 2.0));
    c = lerp(c, float3(1.0, 0.72, 0.0), saturate(t * 5.0 - 3.0));
    c = lerp(c, float3(1.0, 1.0, 0.65), saturate(t * 5.0 - 4.0));
    return c;
}

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(TextureSampler, uv);

    // heat haze: the picture wobbles slowly
    float2 p = uv;
    p.x += sin(uv.y * Resolution.y * 0.35 + fTimer * 3.0) * 0.0012 * Shimmer;
    p.y += sin(uv.x * Resolution.x * 0.25 + fTimer * 2.3) * 0.0008 * Shimmer;

    // a soft sensor: the middle pixel and its neighbours
    p = SharpUV(p);
    float2 px = 1.0 / Resolution;
    float3 c = tex2D(TextureSampler, p).rgb * 0.5
             + tex2D(TextureSampler, p + float2(px.x, 0)).rgb * 0.125
             + tex2D(TextureSampler, p - float2(px.x, 0)).rgb * 0.125
             + tex2D(TextureSampler, p + float2(0, px.y)).rgb * 0.125
             + tex2D(TextureSampler, p - float2(0, px.y)).rgb * 0.125;

    float lum = dot(c, float3(0.3, 0.59, 0.11));
    float warm = max(0.0, c.r - (c.g + c.b) * 0.5);
    float heat = lum * 0.75 + warm * 0.8;

    // a little contrast, so the palette is used over its whole range
    heat = saturate((heat - 0.1) * 1.25);

    float3 thermal = Palette(heat);
    return float4(lerp(original.rgb, thermal, Amount), 1.0);
}

technique HeatVision
{
    pass P0
    {
        PixelShader = compile ps_3_0 main();
    }
}
