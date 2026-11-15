Shader "Lucid/HazardEnergy"
{
    // Additive energy for boss telegraphs, beams and rings. Same inputs as Lucid/GlowParticle in trail
    // mode (vertex colour, v across the width), so shapes keep their colour, alpha and size; this only
    // adds a hot core and plasma that flows through the line.
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            struct A { float3 p : POSITION; float4 c : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float4 c : COLOR; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };

            float hash1(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }
            float vnoise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(lerp(hash1(i), hash1(i + float3(1, 0, 0)), f.x), lerp(hash1(i + float3(0, 1, 0)), hash1(i + float3(1, 1, 0)), f.x), f.y);
                float b = lerp(lerp(hash1(i + float3(0, 0, 1)), hash1(i + float3(1, 0, 1)), f.x), lerp(hash1(i + float3(0, 1, 1)), hash1(i + float3(1, 1, 1)), f.x), f.y);
                return lerp(a, b, f.z);
            }

            V vert(A a)
            {
                V o;
                o.world = TransformObjectToWorld(a.p);
                o.pos = TransformWorldToHClip(o.world);
                o.c = a.c;
                o.uv = a.uv;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float soft = saturate(1.0 - abs(i.uv.y - 0.5) * 2.0);
                float glow = soft * soft;
                float core = pow(soft, 7.0);
                float t = _Time.y;
                float flow = vnoise(i.world * 1.5 + float3(t * 0.9, t * 1.7, -t * 0.6)) * vnoise(i.world * 4.3 - float3(t * 2.1, 0.0, t * 1.4));
                float plasma = 0.7 + flow * 1.9;
                float flicker = 0.93 + 0.07 * sin(t * 41.0 + i.world.x * 2.7 + i.world.z * 3.1);
                float3 tint = i.c.rgb * _Color.rgb;
                float3 rgb = tint * 2.0 * plasma + lerp(tint, float3(1, 1, 1), 0.65) * core * 2.2;
                float alpha = i.c.a * _Color.a * saturate(glow + core * 0.35) * flicker;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
