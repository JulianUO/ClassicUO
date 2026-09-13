// SPDX-License-Identifier: BSD-2-Clause

float2 textureSize;
float4x4 MatrixTransform;
float Time;
float ScanlineIntensity;
float Softness;

sampler decal : register(s0);

struct VS_INPUT
{
    float4 Position : POSITION0;
    float3 Normal	: NORMAL0;
    float3 TexCoord : TEXCOORD0;
    float3 Hue		: TEXCOORD1;
};

struct PS_INPUT
{
    float4 Position : POSITION0;
    float3 TexCoord : TEXCOORD0;
    float2 ScreenPos : TEXCOORD1;
};

PS_INPUT main_vertex(VS_INPUT IN)
{
    PS_INPUT OUT;
    OUT.Position = mul(IN.Position, MatrixTransform);
    OUT.TexCoord = IN.TexCoord;
    OUT.ScreenPos = IN.TexCoord.xy * textureSize;
    return OUT;
}

float4 main_fragment(PS_INPUT IN) : COLOR0
{
    float2 uv = IN.TexCoord.xy;
    float2 texel = 1.0 / textureSize;

    // Soft blur reminiscent of CRT phosphor bloom
    float3 c = tex2D(decal, uv).rgb * 0.40;
    c += tex2D(decal, uv + float2(texel.x, 0)).rgb * 0.15;
    c += tex2D(decal, uv - float2(texel.x, 0)).rgb * 0.15;
    c += tex2D(decal, uv + float2(0, texel.y)).rgb * 0.10;
    c += tex2D(decal, uv - float2(0, texel.y)).rgb * 0.10;
    c += tex2D(decal, uv + float2(texel.x, texel.y)).rgb * 0.05;
    c += tex2D(decal, uv + float2(-texel.x, texel.y)).rgb * 0.05;

    // Horizontal scanlines
    float scan = 0.55 + 0.45 * sin(IN.ScreenPos.y * 3.14159265);
    scan = lerp(1.0, scan, ScanlineIntensity);
    c *= scan;

    // Subtle mask / aperture grille
    float mask = 0.92 + 0.08 * sin(IN.ScreenPos.x * 3.14159265);
    c *= mask;

    // Mild vignette
    float2 q = uv - 0.5;
    float vig = saturate(1.0 - dot(q, q) * 0.85);
    c *= vig;

    // Slight warmth / crush blacks like late-90s monitors
    c = pow(saturate(c), 0.95);
    c.r *= 1.02;
    c.b *= 0.98;

    return float4(c, 1.0);
}

technique CRTTechnique
{
    pass p0
    {
        VertexShader = compile vs_3_0 main_vertex();
        PixelShader = compile ps_3_0 main_fragment();
    }
}
