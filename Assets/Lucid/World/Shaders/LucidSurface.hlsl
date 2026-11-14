#ifndef LUCID_SURFACE_INCLUDED
#define LUCID_SURFACE_INCLUDED

// Shared body of Lucid/World/Environment and Lucid/World/Foliage.
// Painterly two-tone lighting with one palette per world. The world state is evaluated per pixel
// from the wave, so a swap rolls across the surface. Vertices can fold (blossoms), grow (roots,
// thorns), sway and lean, all timed from the same wave.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "LucidWorld.hlsl"

CBUFFER_START(UnityPerMaterial)
    half4 _DreamColor;
    half4 _DreamShade;
    half4 _DreamAccent;
    half4 _NightColor;
    half4 _NightShade;
    half4 _NightAccent;
    half4 _DreamEmission;
    half4 _NightEmission;
    half4 _DreamRim;
    half4 _NightRim;
    half4 _CorruptionColor;
    float _RimPower;
    float _RampMid;
    float _RampSoft;
    float _VariationScale;
    float _VariationAmount;
    float _Corruption;
    float _EmissionMask;
    float _MaskPower;
    float _EmissionVeins;
    float _Pulse;
    float _PulseSpeed;
    float _PlayerGlow;
    float _FogInfluence;
    float _ExistIn;
    float _Wind;
    float _WindHeight;
    float _FoldDream;
    float _FoldNight;
    float _FoldHinge;
    float _FoldHingeY;
    float _GrowDream;
    float _GrowNight;
    float _GrowWidth;
    float _MorphSeconds;
    float _SeamPush;
    float _PlayerBend;
    float _LeanToPlayer;
    float _Translucency;
CBUFFER_END

struct LucidAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct LucidVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// Object-space morph, then world-space motion. Returns the world position and normal.
void LucidDeform(float3 positionOS, float3 normalOS, out float3 positionWS, out float3 normalWS)
{
    float3 restWS = TransformObjectToWorld(positionOS);
    float m = LucidShiftSoft(restWS, _MorphSeconds);
    // A small overshoot while the state is changing makes the morph feel elastic.
    float moving = sin(saturate(m) * PI);

    float3 p = positionOS;
    float3 n = normalOS;

    float fold = lerp(_FoldDream, _FoldNight, m);
    if (abs(fold) > 0.0001)
    {
        float r = length(p.xz);
        float2 radial = p.xz / max(r, 0.0001);
        float rr = r - _FoldHinge;
        if (rr > 0.0)
        {
            float s, c;
            sincos(fold, s, c);
            float y0 = p.y - _FoldHingeY;
            float nr = rr * c - y0 * s;
            float ny = rr * s + y0 * c;
            p.xz = radial * (_FoldHinge + nr);
            p.y = _FoldHingeY + ny;
            float nRad = dot(n.xz, radial);
            float3 tangent = float3(n.x - radial.x * nRad, 0.0, n.z - radial.y * nRad);
            float nRad2 = nRad * c - n.y * s;
            float nY2 = nRad * s + n.y * c;
            n = tangent + float3(radial.x * nRad2, nY2, radial.y * nRad2);
        }
    }

    float grow = lerp(_GrowDream, _GrowNight, m) * (1.0 + 0.12 * moving);
    float wide = lerp(1.0, grow, _GrowWidth);
    p *= float3(wide, grow, wide);

    positionWS = TransformObjectToWorld(p);
    normalWS = TransformObjectToWorldNormal(n);

    float weight = saturate(abs(positionOS.y) / max(_WindHeight, 0.01));
    weight *= weight;

    if (_Wind > 0.0)
    {
        float t = _Time.y;
        float gust = sin(t * 0.9 + restWS.x * 0.21 + restWS.z * 0.17) * 0.6 + sin(t * 2.3 + restWS.x * 0.9 - restWS.z * 0.7) * 0.25;
        float side = sin(t * 1.4 + restWS.z * 0.33 + restWS.y * 0.5) * 0.4;
        positionWS.xz += float2(gust, side) * (_Wind * weight);
    }

    float3 toPlayer = _LucidPlayerPos.xyz - restWS;
    float flatDist = length(toPlayer.xz);
    float2 toPlayerDir = toPlayer.xz / max(flatDist, 0.001);
    if (_PlayerBend > 0.0)
    {
        // Grass parts around the player.
        float push = saturate(1.0 - flatDist / 1.6) * saturate(1.0 - abs(toPlayer.y) / 2.5);
        positionWS.xz -= toPlayerDir * (push * _PlayerBend * weight);
        positionWS.y -= push * _PlayerBend * weight * 0.35;
    }
    if (_LeanToPlayer > 0.0)
    {
        // Thorns reach for the player in Nightmare.
        float reach = saturate(1.0 - flatDist / 6.0) * saturate(1.0 - abs(toPlayer.y) / 4.0) * m;
        positionWS.xz += toPlayerDir * (reach * _LeanToPlayer * weight);
    }

    if (_SeamPush > 0.0)
    {
        float w = max(_ShiftEdgeWidth, 0.01);
        float x = LucidFrontDistance(restWS);
        float crest = exp(-abs(x + w * 0.5) * (2.0 / w)) * _ShiftSeam;
        positionWS += normalWS * (crest * _SeamPush);
    }
}

// Dissolve for pieces that exist in one world only. Returns the glow of the dissolve edge.
half LucidExistence(float3 positionWS, float blend)
{
    if (_ExistIn < 0.5) return 0.0;
    float presence = _ExistIn < 1.5 ? 1.0 - blend : blend;
    float cut = presence - (LucidNoise(positionWS * 1.7) * 0.6 + 0.2);
    clip(cut);
    return 1.0 - smoothstep(0.0, 0.12, cut);
}

LucidVaryings LucidVert(LucidAttributes input)
{
    LucidVaryings o = (LucidVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 positionWS, normalWS;
    LucidDeform(input.positionOS.xyz, input.normalOS, positionWS, normalWS);
    o.positionWS = positionWS;
    o.normalWS = normalWS;
    o.positionCS = TransformWorldToHClip(positionWS);
    o.color = input.color;
    return o;
}

half4 LucidFrag(LucidVaryings i, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    float seam;
    float blend = LucidShift(i.positionWS, seam);
    half edge = LucidExistence(i.positionWS, blend);

    half3 N = normalize(i.normalWS) * IS_FRONT_VFACE(frontFace, 1.0, -1.0);
    half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
    float n = LucidFbm(i.positionWS * _VariationScale);

    half3 litD = lerp(_DreamColor.rgb, _DreamAccent.rgb, saturate((n - 0.35) * 1.7) * _VariationAmount);
    half3 litN = lerp(_NightColor.rgb, _NightAccent.rgb, saturate((n - 0.35) * 1.7) * _VariationAmount);
    half3 albedo = lerp(litD, litN, blend) * i.color.rgb;
    half3 shade = lerp(_DreamShade.rgb, _NightShade.rgb, blend) * i.color.rgb;

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half ndl = dot(N, mainLight.direction) * 0.5 + 0.5;
    ndl += (n - 0.5) * _VariationAmount * 0.5; // the terminator wanders like a brush edge
    half band = smoothstep(_RampMid - _RampSoft, _RampMid + _RampSoft, ndl);
    band *= lerp(1.0, mainLight.shadowAttenuation, 0.85);

    half3 key = lerp(_LucidKeyColorDream.rgb, _LucidKeyColorNight.rgb, blend);
    half3 ambient = lerp(
        lerp(_LucidAmbientGroundDream.rgb, _LucidAmbientSkyDream.rgb, N.y * 0.5 + 0.5),
        lerp(_LucidAmbientGroundNight.rgb, _LucidAmbientSkyNight.rgb, N.y * 0.5 + 0.5), blend);
    half3 color = lerp(shade, albedo * key, band) + albedo * ambient * 0.3;

    #if defined(LUCID_FOLIAGE)
    half through = pow(saturate(dot(V, -mainLight.direction)), 3.0) * _Translucency;
    color += albedo * key * through;
    #endif

    // Rim light, strongest on edges that face up or toward the key light.
    half rim = pow(1.0 - saturate(dot(N, V)), _RimPower);
    rim *= smoothstep(-0.3, 0.7, N.y * 0.6 + dot(N, mainLight.direction) * 0.6);
    color += lerp(_DreamRim.rgb, _NightRim.rgb, blend) * rim;

    // Point lights (the player's glow, lanterns). In Nightmare they reveal a little of the Dream colour.
    #if defined(_ADDITIONAL_LIGHTS)
    half3 lightAlbedo = lerp(litD, litN, blend * 0.55) * i.color.rgb;
    InputData inputData = (InputData)0;
    inputData.positionWS = i.positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
        half wrap = saturate(dot(N, light.direction) * 0.6 + 0.4);
        color += lightAlbedo * light.color * (light.distanceAttenuation * light.shadowAttenuation * wrap * 0.55);
    LIGHT_LOOP_END
    #endif

    // Corruption veins crawl over surfaces in Nightmare.
    if (_Corruption > 0.0)
    {
        float v = abs(LucidNoise(i.positionWS * 0.85 + float3(0.0, _Time.y * 0.05, 0.0)) - 0.5);
        // Veins gather in patches, so some of each surface stays clean and readable.
        float patch = smoothstep(0.38, 0.62, LucidNoise(i.positionWS * 0.16 + 11.0));
        half vein = smoothstep(0.035, 0.0, v) * blend * _Corruption * patch;
        color = lerp(color, color * 0.35, vein * 0.6);
        color += _CorruptionColor.rgb * vein * (0.65 + 0.35 * sin(_Time.y * 2.0 + i.positionWS.y * 1.3));
    }

    half3 emission = lerp(_DreamEmission.rgb, _NightEmission.rgb, blend);
    half painted = pow(saturate(i.color.a), _MaskPower);
    if (_EmissionVeins > 0.0)
    {
        // Thin glowing veins inside the painted area, instead of lighting the whole face.
        float ridge = abs(LucidNoise(i.positionWS * float3(1.7, 0.5, 1.7) + 3.1) - 0.5);
        half vein = smoothstep(0.075, 0.0, ridge) + smoothstep(0.2, 0.0, ridge) * 0.12;
        painted = lerp(painted, smoothstep(0.05, 0.7, painted) * vein, _EmissionVeins);
    }
    half mask = lerp(1.0, painted, _EmissionMask);
    half pulse = 1.0 + _Pulse * sin(_Time.y * _PulseSpeed + n * 6.28318);
    half closeness = LucidPlayerNear(i.positionWS);
    color += emission * (mask * pulse * (1.0 + closeness * _PlayerGlow * (1.0 - blend)));

    half3 seamColor = LucidSeamColor(blend);
    color += seamColor * (seam * 1.3 + edge * 2.0);

    color = LucidApplyFog(color, i.positionWS, blend, _FogInfluence);
    return half4(color, 1.0);
}

// ---------- Shadow caster ----------

float3 _LightDirection;
float3 _LightPosition;

LucidVaryings LucidShadowVert(LucidAttributes input)
{
    LucidVaryings o = (LucidVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
    float3 positionWS, normalWS;
    LucidDeform(input.positionOS.xyz, input.normalOS, positionWS, normalWS);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
    float3 lightDirectionWS = _LightDirection;
    #endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    #if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    o.positionCS = positionCS;
    o.positionWS = positionWS;
    o.normalWS = normalWS;
    o.color = input.color;
    return o;
}

half4 LucidDepthFrag(LucidVaryings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    if (_ExistIn > 0.5)
    {
        float seam;
        float blend = LucidShift(i.positionWS, seam);
        LucidExistence(i.positionWS, blend);
    }
    return 0;
}

#endif
