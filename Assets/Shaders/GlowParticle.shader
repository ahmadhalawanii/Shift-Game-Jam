Shader "Lucid/GlowParticle"
{
    // Additive soft glow for particles (radial) and trails (_Trail = 1: fade across the width and along the length).
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Trail ("Trail mode", Float) = 0
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
                float _Trail;
            CBUFFER_END

            struct A { float3 p : POSITION; float4 c : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float4 c : COLOR; float2 uv : TEXCOORD0; };

            V vert(A a)
            {
                V o;
                o.pos = TransformObjectToHClip(a.p);
                o.c = a.c;
                o.uv = a.uv;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float a;
                if (_Trail > 0.5)
                {
                    a = saturate(1.0 - abs(i.uv.y - 0.5) * 2.0);
                    a = a * a;
                }
                else
                {
                    float r = length(i.uv - 0.5) * 2.0;
                    a = saturate(1.0 - r);
                    a = a * a;
                }
                return half4(i.c.rgb * _Color.rgb * 2.0, i.c.a * a * _Color.a);
            }
            ENDHLSL
        }
    }
}
