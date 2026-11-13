Shader "Lucid/ShieldFresnel"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.75, 0.9, 1)
        _Power ("Rim power", Float) = 2.5
        _Alpha ("Alpha", Float) = 0.9
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Power;
                float _Alpha;
            CBUFFER_END

            struct A { float3 p : POSITION; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; float3 nW : TEXCOORD0; float3 wp : TEXCOORD1; };

            V vert(A a)
            {
                V o;
                o.wp = TransformObjectToWorld(a.p);
                o.pos = TransformWorldToHClip(o.wp);
                o.nW = TransformObjectToWorldNormal(a.n);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float3 v = normalize(GetCameraPositionWS() - i.wp);
                float f = pow(1.0 - saturate(dot(normalize(i.nW), v)), _Power);
                float pulse = 0.85 + 0.15 * sin(_Time.y * 8.0 + i.wp.y * 4.0);
                return half4(_Color.rgb * 1.6, (0.06 + f) * _Alpha * pulse);
            }
            ENDHLSL
        }
    }
}
