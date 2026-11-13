Shader "Lucid/LightShaft"
{
    // A soft additive beam of light on a quad. uv.y runs from the source (0) to the far end (1).
    Properties
    {
        _Color ("Colour", Color) = (1, 0.85, 0.9, 1)
        _Intensity ("Intensity", Float) = 0.35
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
                float _Intensity;
            CBUFFER_END

            struct A { float3 p : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; };

            V vert(A a)
            {
                V o;
                o.wp = TransformObjectToWorld(a.p);
                o.pos = TransformWorldToHClip(o.wp);
                o.uv = a.uv;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float across = saturate(1.0 - abs(i.uv.x - 0.5) * 2.0);
                across = across * across;
                float along = pow(saturate(1.0 - i.uv.y), 1.4);
                float shimmer = 0.8 + 0.2 * sin(_Time.y * 0.6 + i.wp.x * 0.7 + i.wp.z * 0.4);
                float a = across * along * _Intensity * shimmer;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
