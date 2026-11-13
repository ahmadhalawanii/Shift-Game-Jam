Shader "Lucid/MemoryFigure"
{
    // Camera-facing quads placed at points sampled from a human shape.
    // Mesh data: uv0 = quad corner (-0.5..0.5), uv1 = (dissolve threshold, random seed), uv2.x = body part.
    // Body parts: 0 head, 1 torso, 2 left arm, 3 right arm, 4 left leg, 5 right leg.
    // The pose is animated here from script-driven parameters (no skeleton).
    Properties
    {
        _Col1 ("Feet colour", Color) = (1.0, 0.85, 0.66, 1)
        _Col2 ("Head colour", Color) = (1.0, 0.7, 0.88, 1)
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _DarkCol ("Underlay colour", Color) = (0.12, 0.05, 0.2, 1)
        _Alpha ("Light alpha", Float) = 0.95
        _DarkAlpha ("Underlay alpha", Float) = 0.35
        _Size ("Point size", Float) = 0.065
        _Strength ("Memory strength", Range(0, 1.1)) = 1
        _Mix ("Nightmare mix", Range(0, 1)) = 0
        _Walk ("Walk phase", Float) = 0
        _Speed01 ("Speed 0..1", Float) = 0
        _Lean ("Lean (radians)", Float) = 0
        _Aim ("Aim 0..1", Float) = 0
        _Shield ("Shield 0..1", Float) = 0
        _Stretch ("Dash stretch 0..1", Float) = 0
        _Hit ("Hit flinch 0..1", Float) = 0
        _Kick ("Recoil 0..1", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Col1, _Col2, _Tint, _DarkCol;
            float _Alpha, _DarkAlpha, _Size, _Strength, _Mix;
            float _Walk, _Speed01, _Lean, _Aim, _Shield, _Stretch, _Hit, _Kick;
        CBUFFER_END

        struct Attributes
        {
            float3 posOS : POSITION;
            float2 corner : TEXCOORD0;
            float2 data : TEXCOORD1;
            float2 part : TEXCOORD2;
        };

        struct Varyings
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            float a : TEXCOORD1;
            float h : TEXCOORD2;
        };

        // Rotate p around the X axis through pivot. Positive angle swings points above the pivot forward (+Z).
        float3 RotX(float3 p, float3 pivot, float a)
        {
            p -= pivot;
            float c = cos(a), s = sin(a);
            p = float3(p.x, p.y * c - p.z * s, p.y * s + p.z * c);
            return p + pivot;
        }

        float3 Pose(float3 p, float part, float seed)
        {
            float sp = _Speed01;
            float ph = _Walk;
            float HALF_TURN = 3.14159;
            float t = _Time.y;

            if (part > 3.5)
            {
                // Legs swing from the hips, opposite phase each side, and lift a little.
                float off = part > 4.5 ? HALF_TURN : 0.0;
                float a = sin(ph + off) * 0.75 * sp;
                p = RotX(p, float3(0, 0.95, 0), a);
                p.y += max(0.0, sin(ph + off + 1.0)) * 0.1 * sp;
            }
            else if (part > 1.5)
            {
                // Arms swing opposite the legs. Aiming or shielding raises them forward.
                float side = part > 2.5 ? 1.0 : -1.0;
                float off = part > 2.5 ? 0.0 : HALF_TURN;
                float swing = sin(ph + off) * 0.6 * sp + sin(t * 1.3 + off) * 0.05;
                float raise = max(_Aim, _Shield);
                float ang = lerp(swing, -1.45, raise);
                float3 pivot = float3(side * 0.3, 1.55, 0);
                p = RotX(p, pivot, ang);
                p.x += side * 0.12 * _Shield;
                p.z -= _Kick * 0.18 * _Aim;
            }

            if (part < 3.5)
            {
                // Upper body leans into the run and bobs.
                p = RotX(p, float3(0, 0.95, 0), _Lean + sin(ph * 2.0) * 0.04 * sp);
                p.y += abs(sin(ph)) * 0.05 * sp;
                p.y += sin(t * 1.8) * 0.015;
            }

            // Dash stretch along the heading.
            p.z += (p.y - 0.95) * _Stretch * 0.6;
            p.y *= 1.0 - _Stretch * 0.15;

            // Memory trails behind at speed, longest near the feet.
            p.z -= sp * (1.0 - saturate(p.y / 1.9)) * 0.35 * (0.3 + seed);

            // Hit flinch: shake.
            float s = seed * 6.2831;
            p += _Hit * 0.14 * float3(sin(t * 40.0 + s * 3.0), sin(t * 47.0 + s * 5.0), sin(t * 53.0 + s * 7.0));
            return p;
        }

        Varyings vert(Attributes v, float sizeScale)
        {
            Varyings o;
            float s = v.data.y * 6.2831;
            float t = _Time.y;
            float3 p = Pose(v.posOS, v.part.x, v.data.y);
            p += 0.025 * float3(sin(t * 1.3 + s * 3.0), cos(t * 1.1 + s * 5.0), sin(t * 0.9 + s * 7.0));
            float d = smoothstep(_Strength - 0.2, _Strength, v.data.x);
            p.y += d * 0.9 * (0.3 + v.data.y);
            p.x += d * 0.3 * sin(s + t);
            p.z += d * 0.3 * cos(s * 2.0 + t);
            float gl = step(0.965, frac(p.y * 9.0 + floor(t * 6.0) * 0.37)) * _Mix;
            p.x += gl * 0.18 * sin(s * 3.0);
            float3 wp = TransformObjectToWorld(p);
            float3 vp = TransformWorldToView(wp);
            float size = _Size * (1.0 - d * 0.6) * sizeScale;
            vp.xy += v.corner * size;
            o.pos = TransformWViewToHClip(vp);
            o.uv = v.corner + 0.5;
            o.a = 1.0 - d;
            o.h = v.posOS.y;
            return o;
        }
        ENDHLSL

        // Dark soft underlay so the figure still reads against bright skies.
        Pass
        {
            Name "Underlay"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vertDark
            #pragma fragment fragDark
            Varyings vertDark(Attributes v) { return vert(v, 1.5); }
            half4 fragDark(Varyings i) : SV_Target
            {
                float r = length(i.uv - 0.5);
                clip(0.5 - r);
                float soft = smoothstep(0.5, 0.0, r);
                return half4(_DarkCol.rgb, soft * i.a * _DarkAlpha);
            }
            ENDHLSL
        }

        // Additive light.
        Pass
        {
            Name "Light"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            HLSLPROGRAM
            #pragma vertex vertLight
            #pragma fragment fragLight
            Varyings vertLight(Attributes v) { return vert(v, 1.0); }
            half4 fragLight(Varyings i) : SV_Target
            {
                float r = length(i.uv - 0.5);
                clip(0.5 - r);
                float soft = smoothstep(0.5, 0.0, r);
                float3 col = lerp(_Col1.rgb, _Col2.rgb, saturate(i.h / 1.95)) * _Tint.rgb;
                return half4(col, soft * i.a * _Alpha);
            }
            ENDHLSL
        }
    }
}
