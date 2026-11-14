Shader "Lucid/Dreamwalker"
{
    // The Dreamwalker: a small hooded wanderer with a glowing heart-core. One mesh, no skeleton.
    // The pose is animated here from parameters set by MemoryAvatar.
    // Vertex colour: r = part / 8, g = flow weight (0 where cloth is attached, 1 at the hem or tip),
    //                b = painted shade, a = glow mask.   uv.x = position around the body (0..1) or a ribbon seed.
    // Parts: 0 body (hood, mantle, torso), 1 cloak, 2 ribbons, 3 left hand, 4 right hand, 5 left leg, 6 right leg, 7 heart-core.
    Properties
    {
        _DreamCloth ("Dream cloth", Color) = (0.86, 0.84, 0.95, 1)
        _DreamHem ("Dream hem", Color) = (0.93, 0.66, 0.86, 1)
        _DreamBody ("Dream body", Color) = (0.34, 0.31, 0.5, 1)
        _DreamCore ("Dream core (HDR)", Color) = (3.2, 2.0, 1.7, 1)
        _DreamRim ("Dream edge light", Color) = (0.75, 0.85, 1.0, 1)
        _NightCloth ("Nightmare cloth", Color) = (0.035, 0.03, 0.05, 1)
        _NightHem ("Nightmare ember (HDR)", Color) = (3.0, 0.7, 0.12, 1)
        _NightBody ("Nightmare body", Color) = (0.02, 0.02, 0.03, 1)
        _NightCore ("Nightmare core (HDR)", Color) = (3.6, 0.8, 0.12, 1)
        _NightRim ("Nightmare edge light", Color) = (0.9, 0.25, 0.08, 1)
        _Mix ("Nightmare mix", Range(0, 1)) = 0
        _Walk ("Walk phase", Float) = 0
        _Speed01 ("Speed 0..1", Float) = 0
        _Lean ("Lean forward (radians)", Float) = 0
        _Roll ("Lean sideways (radians)", Float) = 0
        _Aim ("Aim 0..1", Float) = 0
        _Shield ("Ward pose 0..1", Float) = 0
        _Kick ("Recoil 0..1", Float) = 0
        _Reload ("Reload 0..1", Float) = 0
        _Hit ("Hurt flinch 0..1", Float) = 0
        _Whip ("Cloak whip forward 0..1", Float) = 0
        _Dissolve ("Dissolve to smoke 0..1", Range(0, 1)) = 0
        _Health ("Health 0..1", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _DreamCloth, _DreamHem, _DreamBody, _DreamCore, _DreamRim;
            float4 _NightCloth, _NightHem, _NightBody, _NightCore, _NightRim;
            float _Mix, _Walk, _Speed01, _Lean, _Roll, _Aim, _Shield, _Kick, _Reload, _Hit, _Whip, _Dissolve, _Health;
        CBUFFER_END

        struct Attributes
        {
            float3 posOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 pos : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            float3 viewWS : TEXCOORD1;
            float4 data : TEXCOORD2;   // part, flow, shade, glow
            float3 rest : TEXCOORD3;   // rest position, for stable noise
            float2 uv : TEXCOORD4;
        };

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float Noise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
        }

        // Rotate p about the X axis through pivot. A positive angle swings points below the pivot backward.
        float3 RotX(float3 p, float3 pivot, float a)
        {
            float s = sin(a), c = cos(a);
            float3 d = p - pivot;
            return pivot + float3(d.x, d.y * c - d.z * s, d.y * s + d.z * c);
        }
        float3 RotZ(float3 p, float3 pivot, float a)
        {
            float s = sin(a), c = cos(a);
            float3 d = p - pivot;
            return pivot + float3(d.x * c - d.y * s, d.x * s + d.y * c, d.z);
        }

        float3 Pose(float3 p, float part, float flow, float seed)
        {
            float t = _Time.y;
            float run = _Speed01;
            float bob = sin(t * 1.8) * 0.015 * (1.0 - run) + abs(sin(_Walk)) * 0.03 * run;

            if (part > 4.5 && part < 6.5)
            {
                // Slim legs swing from the hip.
                float side = part < 5.5 ? 0.0 : 3.14159;
                p = RotX(p, float3(p.x, 0.62, 0.0), sin(_Walk + side) * 0.75 * run);
            }
            else
            {
                p.y += bob;
                if (part > 2.5 && part < 4.5)
                {
                    // Floating hands: drift at rest, swing with the stride, move to the aim, ward and reload poses.
                    bool right = part > 3.5;
                    float3 rest = float3(right ? 0.3 : -0.3, 0.95, 0.08);
                    float3 d = p - rest;
                    float3 h = rest;
                    h.y += sin(t * 2.1 + (right ? 0.0 : 1.7)) * 0.025;
                    h.z += sin(_Walk + (right ? 3.14159 : 0.0)) * 0.14 * run;
                    if (right)
                    {
                        h = lerp(h, float3(0.2, 1.2, 0.46), _Aim);
                        h += float3(0.0, 0.06, -0.14) * _Kick;
                        float3 dip = float3(0.12 + cos(_Reload * 6.283) * 0.05, 0.92 + sin(_Reload * 6.283) * 0.05, 0.24);
                        h = lerp(h, dip, sin(saturate(_Reload) * 3.14159));
                    }
                    else
                    {
                        h = lerp(h, float3(-0.1, 1.22, 0.4), _Shield);
                        h = lerp(h, float3(-0.14, 1.12, 0.36), _Aim * 0.6 * (1.0 - _Shield));
                    }
                    p = h + d * (1.0 + _Kick * 0.25);
                }
                else if (part > 0.5 && part < 1.5)
                {
                    // Cloak: streams behind with speed, sways into strafes, ripples, whips forward when the dash reforms.
                    float f2 = flow * flow;
                    float ripple = sin(t * 3.2 + seed * 18.85 + p.y * 5.0);
                    p.z -= f2 * (0.05 + run * 0.42);
                    p.y += f2 * run * 0.12;
                    p.x += f2 * _Roll * 1.1;
                    p.xz += normalize(p.xz + 0.0001) * ripple * flow * (0.025 + run * 0.035);
                    p.z += f2 * _Whip * 0.75;
                    p.y += f2 * _Whip * 0.2;
                }
                else if (part > 1.5 && part < 2.5)
                {
                    // Ribbons of dream-cloth: hang at rest, trail out flat when running.
                    float f2 = flow * flow;
                    p.z -= f2 * (0.12 + run * 0.75);
                    p.y += f2 * run * 0.6;
                    p.x += sin(t * 4.3 + flow * 5.0 + seed * 40.0) * flow * (0.05 + run * 0.06) + f2 * _Roll * 1.4;
                    p.z += f2 * _Whip * 0.9;
                }
                else if (part > 6.5)
                {
                    // Heart-core: beats, faster at low health.
                    float3 c = float3(0.0, 1.03, 0.15);
                    float beat = pow(saturate(sin(t * lerp(9.0, 3.2, _Health))), 6.0);
                    p = c + (p - c) * (1.0 + beat * 0.22);
                }
            }

            // Whole body: flinch back when hurt, lean into movement.
            p.z -= _Hit * 0.14 * p.y;
            p = RotX(p, float3(0, 0, 0), _Lean);
            p = RotZ(p, float3(0, 0, 0), _Roll);

            // Dissolving into smoke: the body frays apart and streams backward.
            float n = Hash(p.xy * 37.0 + p.z * 11.0);
            p += float3((n - 0.5) * 0.5, (Hash(p.zx * 23.0) - 0.3) * 0.5, -n * 1.4) * _Dissolve * _Dissolve;
            return p;
        }

        Varyings Vert(Attributes v)
        {
            Varyings o;
            float part = round(v.color.r * 8.0);
            float3 p = Pose(v.posOS, part, v.color.g, v.uv.x);
            float3 ws = TransformObjectToWorld(p);
            o.pos = TransformWorldToHClip(ws);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.viewWS = GetWorldSpaceViewDir(ws);
            o.data = float4(part, v.color.g, v.color.b, v.color.a);
            o.rest = v.posOS;
            o.uv = v.uv;
            return o;
        }

        // Shared cutout: the smoke dissolve, and in Nightmare the cloak hem turns to ragged ink.
        // Returns how close the pixel is to the dissolve edge (for the glow).
        float Cutout(Varyings i)
        {
            float part = i.data.x, flow = i.data.y;
            float n = Noise(i.rest.xy * 22.0 + i.rest.z * 9.0);
            clip(n - _Dissolve * 1.05 + 0.02 * (1.0 - _Dissolve) + 0.001);
            if (part > 0.5 && part < 2.5)
            {
                float t = _Time.y;
                float jag = abs(frac(i.uv.x * 11.0 + Noise(float2(i.uv.x * 30.0, t * 0.7)) * 0.35) - 0.5) * 2.0;
                float ink = Noise(float2(i.uv.x * 26.0, flow * 7.0 - t * 0.9));
                float fray = _Mix * smoothstep(0.5, 1.0, flow);
                clip(1.0 - flow - _Mix * 0.2 * jag + 0.001);
                clip(ink - fray * 0.62);
            }
            return saturate((n - _Dissolve * 1.05) * 9.0);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float edge = Cutout(i);
                float part = i.data.x, flow = i.data.y, shade = i.data.z, glow = i.data.w;
                float mix = _Mix;
                float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);
                float3 v = normalize(i.viewWS);

                // Palette per part.
                float3 dream = lerp(_DreamCloth.rgb, _DreamHem.rgb, smoothstep(0.35, 1.0, flow));
                float3 night = _NightCloth.rgb;
                if (part > 4.5 && part < 6.5) { dream = _DreamBody.rgb; night = _NightBody.rgb; }
                if (part > 2.5 && part < 4.5) { dream = float3(1.0, 0.9, 0.82); night = float3(0.16, 0.05, 0.03); }
                float3 albedo = lerp(dream, night, mix) * shade;

                // Soft painterly light: wrapped main light, sky ambient, and a coloured edge light.
                Light main = GetMainLight();
                float wrap = saturate(dot(n, main.direction) * 0.5 + 0.5);
                float3 lit = albedo * (main.color * (0.25 + wrap * 0.75) * 0.8 + SampleSH(n) * 0.6);
                float fres = pow(1.0 - saturate(dot(n, v)), 3.0);
                float3 rim = lerp(_DreamRim.rgb * 0.55, _NightRim.rgb * (0.25 + flow * 0.9), mix) * fres;

                // Glow: the heart-core, the eyes and hands, the hem (pearl in Dream, embers in Nightmare).
                float3 core = lerp(_DreamCore.rgb, _NightCore.rgb, mix);
                float flicker = 0.75 + 0.25 * Noise(float2(i.uv.x * 40.0, _Time.y * 3.0));
                float hem = smoothstep(0.72, 0.98, flow) * step(0.5, part) * step(part, 2.5);
                float3 emission = core * glow * (0.35 + 0.65 * _Health)
                    + lerp(_DreamHem.rgb * 0.35, _NightHem.rgb * flicker, mix) * hem
                    + core * (1.0 - edge) * step(0.001, _Dissolve) * 1.5;
                return half4(lit + rim + emission, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth
            half4 FragDepth(Varyings i) : SV_Target { Cutout(i); return 0; }
            ENDHLSL
        }
    }
}
