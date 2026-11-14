Shader "Lucid/SmokePuff"
{
    // Alpha-blended smoke particle for the dash. Soft noisy puff that erodes into wisps as it ages,
    // so it can be pale mist (Dream) or black ink (Nightmare). Colour and opacity come from the particle.
    // Needs the particle vertex streams Position, Color, UV, StableRandom.x, AgePercent (set by SmokeDash).
    Properties { _Soft ("Edge softness", Range(0.05, 1)) = 0.4 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Soft;
            CBUFFER_END

            struct Attributes { float3 pos : POSITION; float4 color : COLOR; float4 uv : TEXCOORD0; };
            struct Varyings { float4 pos : SV_POSITION; float4 color : COLOR; float4 uv : TEXCOORD0; };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(v.pos);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv.xy * 2.0 - 1.0;
                float rnd = i.uv.z, age = i.uv.w;
                float2 q = p * 1.7 + rnd * 41.0 + float2(0.0, -age * 0.9);
                float n = Noise(q) * 0.65 + Noise(q * 2.3 + 7.0) * 0.35;
                float body = saturate(1.0 - length(p)) * (0.55 + n);
                float cut = 0.12 + age * 0.5;
                float a = smoothstep(cut, cut + _Soft, body) * i.color.a;
                float3 rgb = i.color.rgb * lerp(0.7, 1.2, n) * (1.0 + p.y * 0.18);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
