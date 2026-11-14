// Sky for both worlds. Dream: indigo to violet with stars, aurora ribbons and magenta horizon mist.
// Nightmare: near-black teal with a pale moon that becomes an eclipse ring during the boss fight.
// A swap sweeps across the dome from the direction of the wave origin.
Shader "Lucid/World/Sky"
{
    Properties
    {
        [Header(Dream)]
        _DreamTop ("Dream top", Color) = (0.106, 0.106, 0.369, 1)
        _DreamMid ("Dream mid", Color) = (0.416, 0.247, 0.627, 1)
        [HDR] _AuroraA ("Aurora A", Color) = (0.37, 0.89, 0.94, 1)
        [HDR] _AuroraB ("Aurora B", Color) = (0.91, 0.55, 0.78, 1)
        [HDR] _DreamStars ("Dream stars", Color) = (1.6, 1.7, 2.2, 1)
        [Header(Nightmare)]
        _NightTop ("Nightmare top", Color) = (0.02, 0.043, 0.102, 1)
        _NightMid ("Nightmare mid", Color) = (0.047, 0.106, 0.2, 1)
        [HDR] _MoonColor ("Moon", Color) = (0.9, 1.25, 1.5, 1)
        _MoonSize ("Moon size (rad)", Range(0.01, 0.3)) = 0.075
        _CloudDark ("Cloud darkness", Range(0, 1)) = 0.75
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LucidWorld.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DreamTop;
                half4 _DreamMid;
                half4 _AuroraA;
                half4 _AuroraB;
                half4 _DreamStars;
                half4 _NightTop;
                half4 _NightMid;
                half4 _MoonColor;
                float _MoonSize;
                float _CloudDark;
            CBUFFER_END

            float4 _ShiftSkyDir;        // direction the sweep starts from
            float _ShiftSkyProgress;    // 0..1 while a wave runs, 1 when idle
            float4 _LucidMoonDir;
            float _LucidEclipse;        // 0 = full moon, 1 = total eclipse ring

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            float Stars(float3 d, float density, float threshold)
            {
                float3 sp = d * density;
                float3 id = floor(sp);
                float3 f = frac(sp) - 0.5;
                float rnd = LucidHash(id);
                float twinkle = 0.65 + 0.35 * sin(_Time.y * (1.0 + rnd * 3.0) + rnd * 40.0);
                return step(threshold, rnd) * smoothstep(0.32, 0.0, length(f)) * twinkle;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float t = _Time.y;

                // Sweep of the swap across the dome.
                float ang = acos(clamp(dot(d, normalize(_ShiftSkyDir.xyz + float3(0, 0.0001, 0))), -1.0, 1.0));
                float front = _ShiftSkyProgress * 3.7;
                float x = ang + (LucidNoise(d * 3.0 + t * 0.2) - 0.5) * 0.35 - front;
                float inside = 1.0 - smoothstep(-0.25, 0.0, x);
                float blend = lerp(_ShiftFrom, _ShiftTo, inside);
                float seam = exp(-abs(x + 0.12) * 9.0) * _ShiftSeam;

                float toward = saturate(dot(d, _LucidKeyDir.xyz));
                float up = saturate(h);

                // ----- Dream -----
                half3 horizonD = _LucidFogColorDream.rgb;
                half3 dream = lerp(horizonD, _DreamMid.rgb, smoothstep(0.0, 0.3, up));
                dream = lerp(dream, _DreamTop.rgb, smoothstep(0.22, 0.85, up));
                // Soft nebula mist low in the sky.
                float neb = LucidFbm(d * 2.4 + float3(t * 0.006, 0.0, t * 0.004));
                dream += horizonD * smoothstep(0.45, 0.85, neb) * (1.0 - smoothstep(0.05, 0.55, up)) * 0.45;
                // Aurora ribbons.
                float2 auv = d.xz / (h + 0.28);
                float band = LucidFbm(float3(auv.x * 1.1 + t * 0.025, auv.y * 0.32, t * 0.018));
                float rays = 0.55 + 0.45 * LucidNoise(float3(auv.x * 13.0, auv.y * 1.5, t * 0.25));
                float aur = smoothstep(0.5, 0.72, band) * smoothstep(0.06, 0.3, h) * (1.0 - smoothstep(0.55, 0.95, h)) * rays;
                dream += lerp(_AuroraA.rgb, _AuroraB.rgb, LucidNoise(float3(auv * 0.7, t * 0.05))) * aur * 0.55;
                float starsD = Stars(d, 150.0, 0.985) + Stars(d, 70.0, 0.992) * 1.6;
                dream += _DreamStars.rgb * starsD * smoothstep(0.02, 0.3, h) * (1.0 - aur);
                dream += _LucidFogGlowDream.rgb * (toward * toward * toward) * (1.0 - smoothstep(0.0, 0.75, up) * 0.6);

                // ----- Nightmare -----
                half3 horizonN = _LucidFogColorNight.rgb;
                half3 night = lerp(horizonN, _NightMid.rgb, smoothstep(0.0, 0.26, up));
                night = lerp(night, _NightTop.rgb, smoothstep(0.18, 0.8, up));
                // The moon slides up into place as the world turns.
                float3 moonDir = normalize(_LucidMoonDir.xyz + float3(0.0, -(1.0 - _WorldBlend) * 0.45, 0.0));
                float3 mr = normalize(cross(float3(0, 1, 0), moonDir));
                float3 mu = cross(moonDir, mr);
                float2 mp = float2(dot(d, mr), dot(d, mu));
                float facing = step(0.0, dot(d, moonDir));
                float mdist = length(mp);
                float disc = smoothstep(_MoonSize + 0.004, _MoonSize - 0.004, mdist) * facing;
                // Eclipse: a dark disc slides over the moon until only a ring is left.
                float2 occ = float2((1.0 - _LucidEclipse) * _MoonSize * 2.3, (1.0 - _LucidEclipse) * _MoonSize * 0.6);
                float cover = smoothstep(_MoonSize * 0.93 + 0.004, _MoonSize * 0.93 - 0.004, length(mp - occ)) * facing;
                float halo = (exp(-mdist * 4.5) * 0.4 + exp(-mdist * 16.0) * 0.55) * facing;
                night += _MoonColor.rgb * (disc * (1.0 - cover) * 1.3 + halo * (0.55 + 0.45 * _LucidEclipse));
                // Dark cloud banks, lit at the edge near the moon.
                float cl = LucidFbm(float3(d.x * 2.2 + t * 0.01, d.y * 6.0, d.z * 2.2 - t * 0.006));
                float cloud = smoothstep(0.48, 0.74, cl) * smoothstep(-0.02, 0.2, h);
                float cloudRim = smoothstep(0.46, 0.56, cl) * (1.0 - smoothstep(0.56, 0.7, cl)) * halo;
                night = lerp(night, night * (1.0 - _CloudDark), cloud * (1.0 - disc * 0.5));
                night += _MoonColor.rgb * cloudRim * 0.7;
                night += half3(0.5, 0.6, 0.8) * Stars(d, 110.0, 0.993) * smoothstep(0.1, 0.4, h) * (1.0 - cloud) * 0.35;
                night += _LucidFogGlowNight.rgb * (toward * toward * toward) * (1.0 - smoothstep(0.0, 0.6, up) * 0.6);

                half3 color = lerp(dream, night, blend);

                // Below the horizon the sky sinks into the mist of the abyss.
                half3 below = lerp(horizonD, horizonN, blend) * 0.55;
                color = lerp(color, below, saturate(-h * 3.5));

                color += LucidSeamColor(blend) * seam * 1.6;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
