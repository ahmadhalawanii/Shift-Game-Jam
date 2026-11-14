// The wave front itself: a sphere shell that WorldShift scales with the front radius.
// It bends the scene behind it and glows where it cuts through geometry.
// Needs the URP opaque texture and depth texture (the world builder turns both on).
Shader "Lucid/World/Seam"
{
    Properties
    {
        _Distortion ("Distortion", Range(0, 0.2)) = 0.045
        _Intersect ("Intersection width (m)", Float) = 1.6
        _Film ("Film brightness", Range(0, 2)) = 0.35
        _Opacity ("Opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+50" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Seam"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "LucidWorld.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Distortion;
                float _Intersect;
                float _Film;
                float _Opacity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 screenUV = i.positionCS.xy / _ScaledScreenParams.xy;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float fragDepth = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float diff = sceneDepth - fragDepth;

                half3 N = normalize(i.normalWS);
                half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                half grazing = pow(1.0 - saturate(abs(dot(N, V))), 2.5);
                half cut = saturate(1.0 - abs(diff) / max(_Intersect, 0.01)) * step(0.0, diff + 0.2);
                half streaks = LucidNoise(i.positionWS * 0.9 + float3(0.0, _Time.y * 2.5, 0.0));
                half strength = saturate(cut * cut + grazing * 0.5 + streaks * 0.12) * _ShiftSeam * _Opacity;

                // Refraction: push the screen sample along the shell normal in view space.
                float3 viewN = TransformWorldToViewDir(N);
                float2 offset = viewN.xy * (_Distortion * strength * (0.6 + streaks));
                half3 scene = SampleSceneColor(screenUV + offset);

                half3 seamColor = LucidSeamColor(_ShiftTo);
                half3 color = scene + seamColor * (cut * cut * 1.1 + grazing * _Film + streaks * cut * 0.5) * _ShiftSeam;
                return half4(color, saturate(strength * 1.6));
            }
            ENDHLSL
        }
    }
}
