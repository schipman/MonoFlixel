// NOTE: "= value" defaults are documentation only (not compiled in); set them from C#.
// Refraction, from Drown's refraction.fx: a scrolling displacement picture (waterfall.jpg) moves the screen around, like looking
// through moving water. Strength scales the original (1 is as strong as the original, 0.1 is a light shimmer).
sampler ColorMapSampler : register(s0);

Texture2D DisplacementTex;
sampler2D DisplacementSampler = sampler_state
{
    Texture = <DisplacementTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
};

float fTimer;
float Strength = 0.1;      // how far the picture moves (1 = the original)
float Scale = 3.0;         // size of the displacement picture on screen (bigger = wider swells)
float Speed = 0.02;        // how fast the displacement scrolls

float4 main(float2 Tex : TEXCOORD0) : COLOR
{
    float2 scroll = float2(fTimer * Speed * 0.6, fTimer * Speed);
    float2 d = tex2D(DisplacementSampler, scroll + Tex / Scale).rg - 0.5;
    Tex += d * 0.4 * Strength;
    return tex2D(ColorMapSampler, Tex);
}

technique Refraction
{
    pass P0
    {
        PixelShader = compile ps_2_0 main();
    }
}
