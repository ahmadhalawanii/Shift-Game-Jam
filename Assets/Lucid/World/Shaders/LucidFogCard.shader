// Soft mist: billboard fog cards between depth layers and flat mist planes over the abyss.
// Fades where it meets geometry and near the camera, so it never shows a hard edge.
Shader "Lucid/World/FogCard"
{
    Properties
    {
        _DreamColor ("Dream tint", Color) = (0.91, 0.55, 0.78, 1)
        _NightColor ("Nightmare tint", Color) = (0.17, 0.3, 0.45, 1)
        [HDR] _DreamGlow ("Dream glow", Color) = (0, 0, 0, 1)
        [HDR] _NightGlow ("Nightmare glow", Color) = (0, 0, 0, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        _NightOpacity ("Nightmare opacity scale", Range(0, 3)) = 1.4
        [Enum(None, 0, Full, 1, Upright, 2)] _Billboard ("Billboard", Float) = 2
        _NoiseScale ("Noise scale", Float) = 0.08
        _Scroll ("Scroll speed", Float) = 0.15
        _DepthFade ("Depth fade (m)", Float) = 6
        _NearFade ("Near fade (m)", Float) = 8
        _EdgeSoft ("Edge softness", Range(0.2, 4)) = 1.4
        _Ripple ("Seam ripple", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" "IgnoreProjector" = "True" }
        Pass
        {
            Name "FogCard"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "LucidWorld.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DreamColor;
                half4 _NightColor;
                half4 _DreamGlow;
                half4 _NightGlow;
                float _Opacity;
                float _NightOpacity;
                float _Billboard;
                float _NoiseScale;
                float _Scroll;
                float _DepthFade;
                float _NearFade;
                float _EdgeSoft;
                float _Ripple;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS;
                if (_Billboard > 0.5)
                {
                    float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                    float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                    float3 camRight = UNITY_MATRIX_V[0].xyz;
                    float3 camUp = _Billboard > 1.5 ? float3(0, 1, 0) : UNITY_MATRIX_V[1].xyz;
                    positionWS = pivotWS + camRight * (input.positionOS.x * scale.x) + camUp * (input.positionOS.y * scale.y);
                }
                else positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float seam;
                float blend = LucidShift(i.positionWS, seam);

                half r = saturate(1.0 - length(i.uv * 2.0 - 1.0));
                half shape = pow(r, _EdgeSoft);
                float3 q = i.positionWS * _NoiseScale + float3(_Time.y * _Scroll * 0.1, _Time.y * _Scroll * 0.03, 0.0);
                half cloud = LucidFbm(q);
                half alpha = shape * smoothstep(0.15, 0.85, cloud + shape * 0.35);

                float2 screenUV = i.positionCS.xy / _ScaledScreenParams.xy;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float fragDepth = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                alpha *= saturate((sceneDepth - fragDepth) / max(_DepthFade, 0.01));
                alpha *= saturate((fragDepth - 0.5) / max(_NearFade, 0.01));
                alpha *= _Opacity * lerp(1.0, _NightOpacity, blend);

                half3 color = lerp(_DreamColor.rgb, _NightColor.rgb, blend);
                color += lerp(_DreamGlow.rgb, _NightGlow.rgb, blend) * cloud;
                float3 dir = normalize(i.positionWS - _WorldSpaceCameraPos);
                float toward = saturate(dot(dir, _LucidKeyDir.xyz));
                color += lerp(_LucidFogGlowDream.rgb, _LucidFogGlowNight.rgb, blend) * (toward * toward * toward) * 0.6;

                // The wave shows as a bright ripple travelling through the mist.
                color += LucidSeamColor(blend) * seam * _Ripple * 1.5;
                alpha = saturate(alpha + seam * _Ripple * 0.35 * shape);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
