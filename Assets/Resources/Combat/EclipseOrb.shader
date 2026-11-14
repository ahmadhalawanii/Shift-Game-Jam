Shader "Lucid/EclipseOrb"
{
    Properties
    {
        _Color("Rim color",Color)=(0.2,0.8,1,1)
        _Thorn("Thorn shape",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Thorn;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 n:NORMAL; };
            struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
            V vert(A i)
            {
                V o;
                float3 n=normalize(i.n);
                float spike=pow(max(abs(n.x),max(abs(n.y),abs(n.z))),12);
                o.world=TransformObjectToWorld(i.p.xyz*(1+_Thorn*spike*0.38));
                o.p=TransformWorldToHClip(o.world); o.normal=TransformObjectToWorldNormal(i.n); return o;
            }
            half4 frag(V i):SV_Target
            {
                float3 n=normalize(i.normal);
                float rim=1-saturate(dot(n,normalize(GetWorldSpaceViewDir(i.world))));
                float band=smoothstep(0.22,0.55,rim);
                float vein=pow(saturate(sin(n.x*19+n.y*13+sin(n.z*17+_Time.y*2))),14)*0.18;
                return half4(_Color.rgb*(0.45+band*1.3+vein)+float3(0.012,0.008,0.025),1);
            }
            ENDHLSL
        }
    }
}
