// NOTE: "= value" defaults are documentation only (not compiled in); set them from C#.
// Vignette: the corners of the screen are darkened, softly, so the eye is drawn to the middle.
sampler TextureSampler : register(s0);

float2 Resolution;
float Intensity;       // how dark the corners get: 0 = not at all, 1 = black
float Radius;          // where the darkening starts, as a distance from the center (0.3 close in, 0.6 only in the corners)
float Softness;        // how wide the fade is (0.4 soft)
float Roundness;       // 0 = an oval that follows the shape of the screen, 1 = a circle

float4 main(float2 uv : TEXCOORD0) : COLOR0
{
    float4 c = tex2D(TextureSampler, uv);

    float aspect = Resolution.x / Resolution.y;
    float2 d = (uv - 0.5) * float2(lerp(1.0, aspect, Roundness), 1.0);
    float dist = length(d) / lerp(0.7071, 0.7071 * aspect * 0.5 + 0.35, Roundness);   // about 1 in the corners

    float v = smoothstep(Radius, Radius + max(Softness, 0.001), dist);
    c.rgb *= 1.0 - v * Intensity;
    return float4(c.rgb, 1.0);
}

technique Vignette
{
    pass P0
    {
        PixelShader = compile ps_3_0 main();
    }
}
