Shader "Lucid/ColossusEye"
{
    // The Sleeper's third eye: a burning iris with fibres, a dark limbal ring and a slit pupil.
    // UVs are centred on the eye. _Color.a is the brightness.
    Properties
    {
        _Color ("Iris (a = brightness)", Color) = (1, 0.7, 0.15, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            struct A { float3 p : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            V vert(A a)
            {
                V o;
                o.pos = TransformObjectToHClip(a.p);
                o.uv = a.uv;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float2 c = i.uv * 2.0 - 1.0;
                float r = length(c);
                float ang = atan2(c.y, c.x);
                // Radial fibres that slowly crawl.
                float fibres = noise(float2(ang * 9.0, r * 2.5 - _Time.y * 0.35)) * 0.6 + noise(float2(ang * 31.0, r * 6.0 + _Time.y * 0.2)) * 0.4;
                float iris = smoothstep(0.98, 0.78, r);
                float hot = smoothstep(0.85, 0.22, r);
                float3 col = _Color.rgb * (0.25 + 1.9 * hot) * (0.45 + 1.1 * fibres);
                col = lerp(col, float3(1.0, 0.95, 0.8) * 2.4, pow(hot, 6.0) * 0.55);
                // Slit pupil.
                float slit = 1.0 - smoothstep(0.75, 1.15, length(c / float2(0.16, 0.72)));
                col = lerp(col, float3(0.02, 0.0, 0.03), slit);
                // Bright ring around the pupil and a dark outer ring.
                col += _Color.rgb * exp(-pow((length(c / float2(0.2, 0.78)) - 1.0) * 5.0, 2.0)) * 1.4;
                col *= iris;
                col += _Color.rgb * exp(-pow((r - 0.9) * 9.0, 2.0)) * 0.5;
                return half4(col * _Color.a * 1.6, 1);
            }
            ENDHLSL
        }
    }
}
