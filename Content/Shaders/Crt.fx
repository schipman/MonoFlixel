// NOTE: "= value" defaults are documentation only (not compiled in); set them from C#.
// A CRT monitor in one pass, in the spirit of the RetroArch CRT shaders (crt-easymode, crt-lottes, crt-geom):
//  - barrel curvature of the glass with rounded corners,
//  - "sharp bilinear" sampling: pixels stay crisp but their edges are soft, and the picture does not shimmer when scaled,
//  - scanlines: every game pixel row is a bright beam that gets wider (and so fills the gap) the brighter it is,
//  - an aperture grille / shadow mask of red, green and blue stripes at the resolution of the window,
//  - the beam's glow sideways, vignette, a faint flicker, and all the mixing done in linear light.
sampler TextureSampler : register(s0);

float fTimer;
float2 Resolution;      // the game screen
float2 OutputSize;      // what it is drawn at (the window)
float Curvature;        // 0 flat, 0.1 slight, 0.25 strong
float ScanlineStrength; // 0 none, 1 full
float MaskStrength;     // 0 none, 1 full
float Vignette;         // 0 none, 1 strong
float Brightness;       // makes up for what the scanlines and mask take away (1.2 to 1.4)
float Sharpness;        // 1 = soft, 3 = crisp pixels with soft edges (default), more = harder edges

float2 Warp(float2 uv)
{
    float2 c = uv * 2.0 - 1.0;
    c *= 1.0 + (c.yx * c.yx) * Curvature;
    c /= 1.0 + Curvature * 0.12;                            // a hair of zoom, so the edges are not cut off
    return c * 0.5 + 0.5;
}

float3 Sample(float2 uv)
{
    float3 c = tex2D(TextureSampler, uv).rgb;
    return c * c;                                           // to linear light (gamma 2)
}

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float2 w = Warp(uv);

    // the rounded screen shape
    float2 b = abs(w - 0.5) - (0.5 - 0.03);
    float dd = length(max(b, 0.0)) - 0.03;
    float shape = 1.0 - smoothstep(-0.002, 0.004, dd);
    if (Curvature < 0.0001) shape = 1.0;                    // a flat screen has no glass edge: the whole picture is shown
    if (shape <= 0.0) return float4(0, 0, 0, 1);

    // which game pixel are we in
    float2 p = w * Resolution;
    float2 i = floor(p);
    float2 f = p - i;

    // sharp bilinear: along the line the picture is blended only near the edge of a pixel; each line is one color
    float scaleX = max(max(OutputSize.x / Resolution.x, 1.0) * (Sharpness / 3.0), 1.0);
    float dx = f.x - 0.5;
    float rx = 0.5 - 0.5 / scaleX;
    float fx = (dx - clamp(dx, -rx, rx)) * scaleX + 0.5;                // only the very edge of a pixel is blended
    float2 q = float2((i.x + fx) / Resolution.x, (i.y + 0.5) / Resolution.y);
    float3 c = Sample(q);

    // the beam bleeds sideways a little
    float2 px = float2(1.0 / Resolution.x, 0.0);
    c = c * 0.78 + (Sample(q - px) + Sample(q + px)) * 0.11;

    // scanlines: a gaussian beam across the line, wider when brighter
    float lum = max(c.r, max(c.g, c.b));
    float sigma = lerp(0.2, 0.42, saturate(lum));
    float d = f.y - 0.5;
    float beam = exp(-(d * d) / (2.0 * sigma * sigma));
    float average = saturate(sigma * 2.5066);
    float scan = lerp(1.0, beam, ScanlineStrength) / lerp(1.0, average, ScanlineStrength);
    c *= scan;

    // aperture grille: red, green and blue stripes, three window pixels per triad
    float stripe = fmod(floor(uv.x * OutputSize.x), 3.0);
    float low = 1.0 - MaskStrength;
    float3 mask = float3(stripe < 0.5 ? 1.0 : low, (stripe > 0.5 && stripe < 1.5) ? 1.0 : low, stripe > 1.5 ? 1.0 : low);
    c *= mask / ((1.0 + 2.0 * low) / 3.0);

    // vignette, flicker
    float2 vv = w * (1.0 - w);
    float vig = lerp(1.0, pow(saturate(16.0 * vv.x * vv.y), 0.35), Vignette);
    float flicker = 1.0 + 0.012 * sin(fTimer * 110.0);

    c *= Brightness * vig * flicker * shape;
    return float4(sqrt(saturate(c)), 1.0);                  // back from linear light
}

technique Crt
{
    pass P0
    {
        PixelShader = compile ps_3_0 main();
    }
}
