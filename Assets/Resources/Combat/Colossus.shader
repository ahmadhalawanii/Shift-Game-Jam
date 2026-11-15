Shader "Lucid/Colossus"
{
    // The Sleeper's body. Porcelain with gold kintsugi seams in Dream; the seams split into molten
    // cracks in Nightmare. Cracks are a 3D cell pattern in object space, so no textures or UVs are needed.
    // Petal mode adds leaf veins from the UVs (u across, v along).
    Properties
    {
        _BaseColor ("Base", Color) = (0.92, 0.88, 0.86, 1)
        _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _SeamColor ("Dream seam", Color) = (1, 0.76, 0.32, 1)
        _CrackColor ("Nightmare crack", Color) = (1, 0.32, 0.06, 1)
        _RimColor ("Rim (a = strength)", Color) = (0.7, 0.85, 1, 0.6)
        _Mix ("Nightmare amount", Range(0, 1)) = 0
        _Flash ("Hit flash", Range(0, 1)) = 0
        _Charge ("Charge", Range(0, 1)) = 0
        _CrackScale ("Crack scale", Float) = 0.55
        _Petal ("Petal mode", Float) = 0
        _Gloss ("Gloss", Range(0, 1)) = 0.7
        _SelfLit ("Self lit", Range(0, 1)) = 0
        _Cracks ("Crack amount", Range(0, 1)) = 1
        _FaceColor ("Masked colour (vertex alpha = 0)", Color) = (0.8, 0.85, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _EmissionColor;
            float4 _SeamColor;
            float4 _CrackColor;
            float4 _RimColor;
            float _Mix;
            float _Flash;
            float _Charge;
            float _CrackScale;
            float _Petal;
            float _Gloss;
            float _SelfLit;
            float _Cracks;
            float4 _FaceColor;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 p : POSITION; float3 n : NORMAL; float4 c : COLOR; float2 uv : TEXCOORD0; };
            struct V
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 local : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float4 c : COLOR;
                float fog : TEXCOORD4;
            };

            float3 hash3(float3 p)
            {
                p = float3(dot(p, float3(127.1, 311.7, 74.7)), dot(p, float3(269.5, 183.3, 246.1)), dot(p, float3(113.5, 271.9, 124.6)));
                return frac(sin(p) * 43758.5453);
            }

            float hash1(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }

            float vnoise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(lerp(hash1(i), hash1(i + float3(1, 0, 0)), f.x), lerp(hash1(i + float3(0, 1, 0)), hash1(i + float3(1, 1, 0)), f.x), f.y);
                float b = lerp(lerp(hash1(i + float3(0, 0, 1)), hash1(i + float3(1, 0, 1)), f.x), lerp(hash1(i + float3(0, 1, 1)), hash1(i + float3(1, 1, 1)), f.x), f.y);
                return lerp(a, b, f.z);
            }

            // x = distance to the nearest cell point, y = distance to the second nearest.
            float2 cells(float3 p)
            {
                float3 base = floor(p), f = frac(p);
                float d1 = 8.0, d2 = 8.0;
                for (int z = -1; z <= 1; z++)
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float3 g = float3(x, y, z);
                    float3 r = g + hash3(base + g) - f;
                    float d = dot(r, r);
                    if (d < d1) { d2 = d1; d1 = d; }
                    else if (d < d2) d2 = d;
                }
                return sqrt(float2(d1, d2));
            }

            V vert(A a)
            {
                V o;
                o.world = TransformObjectToWorld(a.p.xyz);
                o.pos = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(a.n);
                o.local = a.p.xyz;
                o.uv = a.uv;
                o.c = a.c;
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float3 p = i.local * _CrackScale;
                float2 cell = cells(p);
                float edge = cell.y - cell.x;
                float n = vnoise(p * 0.7 + 7.3) * 0.6 + vnoise(p * 2.1 + 1.7) * 0.4;
                float fine = vnoise(i.local * 5.0);

                // A few seams in Dream; the whole surface breaks up in Nightmare.
                float cover = smoothstep(lerp(0.50, 0.36, _Mix), lerp(0.62, 0.58, _Mix), n) * _Cracks;
                float w = lerp(0.03, 0.05, _Mix) + _Charge * 0.02;
                float crack = (1.0 - smoothstep(0.0, w, edge)) * cover;
                float groove = (1.0 - smoothstep(0.0, w * 3.5, edge)) * cover;

                // Leaf veins for petals and roots.
                float u = abs(i.uv.x - 0.5) * 2.0, v = i.uv.y;
                float mid = 1.0 - smoothstep(0.0, 0.06, u);
                float br = abs(frac(v * 9.0 - u * 2.4) - 0.5) * 2.0;
                float branch = (1.0 - smoothstep(0.0, lerp(0.16, 0.09, _Mix), br)) * smoothstep(0.04, 0.2, u) * (1.0 - smoothstep(0.7, 1.0, u));
                float vein = saturate(mid + branch * 0.65) * smoothstep(0.02, 0.14, v) * (1.0 - smoothstep(0.86, 1.0, v));
                crack = lerp(crack, max(crack * 0.6, vein), _Petal);
                groove = lerp(groove, max(groove * 0.6, vein), _Petal);

                float cavity = lerp(0.42, 1.0, i.c.r);
                float3 albedo = lerp(_FaceColor.rgb, _BaseColor.rgb, i.c.a) * (0.84 + 0.16 * n) * (0.94 + 0.06 * fine) * cavity;
                // Petals fade from a deep base to a pale edge.
                albedo *= lerp(1.0, lerp(0.62, 1.12, saturate(v * 1.3)) * lerp(1.0, 1.15, u), _Petal);
                albedo = lerp(albedo, albedo * 0.38, groove * 0.85);

                float3 seam = lerp(_SeamColor.rgb, _CrackColor.rgb, _Mix);
                float pulse = 0.62 + 0.38 * sin(_Time.y * lerp(1.1, 3.6, _Mix) + n * 9.0 + i.local.y * 0.7);
                albedo = lerp(albedo, seam * 0.55, crack * (1.0 - _Mix) * 0.9);
                float3 emission = _EmissionColor.rgb + seam * crack * (lerp(0.55, 2.6, _Mix) * lerp(1.0, 0.6, _Petal * _Mix) + _Charge * 2.0) * pulse;
                // Heat spreads out of the cracks in Nightmare.
                emission += _CrackColor.rgb * groove * _Mix * 0.10 * pulse + albedo * _SelfLit;

                float3 N = normalize(i.normal);
                float3 V3 = normalize(GetWorldSpaceViewDir(i.world));
                Light L = GetMainLight(TransformWorldToShadowCoord(i.world));
                float ndl = dot(N, L.direction);
                float wrap = saturate(ndl * 0.6 + 0.4);
                float shadow = lerp(0.45, 1.0, L.shadowAttenuation);
                float3 ambient = SampleSH(N) + 0.06;
                float3 col = albedo * (ambient + L.color * wrap * shadow);

                float3 H = normalize(L.direction + V3);
                float gloss = _Gloss * lerp(1.0, 0.45, _Mix);
                float spec = pow(saturate(dot(N, H)), lerp(18.0, 140.0, gloss)) * gloss * saturate(ndl * 4.0) * L.shadowAttenuation;
                col += L.color * spec * (0.55 + crack * (1.0 - _Mix) * 2.0);

                float fres = pow(1.0 - saturate(dot(N, V3)), 3.0);
                col += _RimColor.rgb * fres * _RimColor.a * (1.0 + _Charge);
                // Soft sheen so the porcelain never goes flat in the pastel Dream light.
                col += albedo * pow(1.0 - saturate(dot(N, V3)), 1.5) * 0.18 * (1.0 - _Mix);
                col += emission;
                col = lerp(col, float3(1.6, 1.6, 1.6), _Flash * (0.25 + 0.6 * fres));
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;

            float4 vert(float4 p : POSITION, float3 n : NORMAL) : SV_POSITION
            {
                float3 world = TransformObjectToWorld(p.xyz);
                float3 normal = TransformObjectToWorldNormal(n);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 toLight = normalize(_LightPosition - world);
                #else
                    float3 toLight = _LightDirection;
                #endif
                float4 clip = TransformWorldToHClip(ApplyShadowBias(world, normal, toLight));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return clip;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half4 frag(float4 pos : SV_POSITION) : SV_Target { return pos.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct DV { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; };
            DV vert(float4 p : POSITION, float3 n : NORMAL)
            {
                DV o;
                o.pos = TransformObjectToHClip(p.xyz);
                o.normal = TransformObjectToWorldNormal(n);
                return o;
            }
            half4 frag(DV i) : SV_Target { return half4(normalize(i.normal), 0); }
            ENDHLSL
        }
    }
}
