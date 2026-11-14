// Additive light: glow flora cores, hanging strands, motes, light shafts, lantern halos, eyes.
// Colour follows the wave. Strands can sway and retract; cards can face the camera.
Shader "Lucid/World/Glow"
{
    Properties
    {
        [HDR] _DreamColor ("Dream colour", Color) = (0.37, 0.89, 0.94, 1)
        [HDR] _NightColor ("Nightmare colour", Color) = (0.29, 0.12, 0.42, 1)
        [Enum(Mesh, 0, Card, 1, Strand, 2)] _Shape ("Shape", Float) = 0
        [Enum(None, 0, Full, 1, Upright, 2)] _Billboard ("Billboard", Float) = 0
        _Fresnel ("Mesh core power", Range(0.2, 6)) = 1.5
        _Pulse ("Pulse", Range(0, 1)) = 0.2
        _PulseSpeed ("Pulse speed", Float) = 1.3
        _Flicker ("Nightmare flicker", Range(0, 1)) = 0
        _PlayerGlow ("Brighten near player (Dream)", Range(0, 4)) = 0
        _FogInfluence ("Fog influence", Range(0, 1)) = 1
        [Enum(Both, 0, DreamOnly, 1, NightmareOnly, 2)] _ExistIn ("Exists in", Float) = 0
        _DepthFade ("Depth fade (m)", Float) = 0
        _Wind ("Wind", Range(0, 2)) = 0
        _WindHeight ("Wind full height", Float) = 4
        _GrowDream ("Length scale Dream", Float) = 1
        _GrowNight ("Length scale Nightmare", Float) = 1
        _MorphSeconds ("Morph seconds", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
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
                float _Shape;
                float _Billboard;
                float _Fresnel;
                float _Pulse;
                float _PulseSpeed;
                float _Flicker;
                float _PlayerGlow;
                float _FogInfluence;
                float _ExistIn;
                float _DepthFade;
                float _Wind;
                float _WindHeight;
                float _GrowDream;
                float _GrowNight;
                float _MorphSeconds;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 pivotWS : TEXCOORD3;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 p = input.positionOS.xyz;
                float3 restWS = TransformObjectToWorld(p);
                float m = LucidShiftSoft(restWS, _MorphSeconds);
                p.y *= lerp(_GrowDream, _GrowNight, m);

                float3 positionWS;
                float3 normalWS;
                if (_Billboard > 0.5)
                {
                    float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                    float3 camRight = UNITY_MATRIX_V[0].xyz;
                    float3 camUp = _Billboard > 1.5 ? float3(0, 1, 0) : UNITY_MATRIX_V[1].xyz;
                    positionWS = pivotWS + camRight * (p.x * scale.x) + camUp * (p.y * scale.y);
                    normalWS = -UNITY_MATRIX_V[2].xyz;
                }
                else
                {
                    positionWS = TransformObjectToWorld(p);
                    normalWS = TransformObjectToWorldNormal(input.normalOS);
                }

                if (_Wind > 0.0)
                {
                    float weight = saturate(abs(input.positionOS.y) / max(_WindHeight, 0.01));
                    weight *= weight;
                    float t = _Time.y;
                    float gust = sin(t * 0.8 + restWS.x * 0.19 + restWS.z * 0.23) * 0.6 + sin(t * 2.1 + restWS.x * 0.8 - restWS.z * 0.6) * 0.25;
                    float side = sin(t * 1.3 + restWS.z * 0.31 + restWS.y * 0.4) * 0.4;
                    positionWS.xz += float2(gust, side) * (_Wind * weight);
                }

                o.positionWS = positionWS;
                o.normalWS = normalWS;
                o.pivotWS = pivotWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = input.uv;
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float seam;
                float blend = LucidShift(i.positionWS, seam);

                half a = 1.0;
                if (_Shape > 1.5)
                {
                    // Strand or shaft: soft across the width, fading toward the tip.
                    half across = saturate(1.0 - abs(i.uv.x * 2.0 - 1.0));
                    half along = smoothstep(0.0, 0.08, i.uv.y) * (1.0 - smoothstep(0.35, 1.0, i.uv.y));
                    a = across * across * along;
                }
                else if (_Shape > 0.5)
                {
                    half r = saturate(1.0 - length(i.uv * 2.0 - 1.0));
                    a = r * r * (3.0 - 2.0 * r);
                    a *= a;
                }
                else
                {
                    half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                    a = pow(saturate(abs(dot(normalize(i.normalWS), V))), _Fresnel);
                }

                if (_ExistIn > 0.5) a *= _ExistIn < 1.5 ? 1.0 - blend : blend;

                float seed = LucidHash(i.pivotWS * 0.37);
                half pulse = 1.0 + _Pulse * sin(_Time.y * _PulseSpeed + seed * 6.28318);
                half flicker = 1.0 - _Flicker * blend * step(0.62, LucidNoise(float3(_Time.y * 9.0, seed * 31.0, 0.0)));
                half closeness = LucidPlayerNear(i.positionWS);
                half3 color = lerp(_DreamColor.rgb, _NightColor.rgb, blend) * i.color.rgb;
                color *= a * i.color.a * pulse * flicker * (1.0 + closeness * _PlayerGlow * (1.0 - blend));
                color *= 1.0 + seam * 2.0;

                if (_DepthFade > 0.0)
                {
                    float2 screenUV = i.positionCS.xy / _ScaledScreenParams.xy;
                    float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float fragDepth = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                    color *= saturate((sceneDepth - fragDepth) / _DepthFade);
                }

                color *= 1.0 - LucidFogAmount(i.positionWS, blend, _FogInfluence);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
