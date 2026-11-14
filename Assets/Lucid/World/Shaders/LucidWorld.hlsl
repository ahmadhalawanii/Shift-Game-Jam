#ifndef LUCID_WORLD_INCLUDED
#define LUCID_WORLD_INCLUDED

// Shared by every Lucid/World shader. The globals are set by WorldShift (the wave) and
// LucidWorldLook (fog, ambient, key light). 0 = Dream, 1 = Nightmare everywhere.

float _WorldBlend;          // global animated mix (sky, post)
float4 _ShiftOrigin;        // world position the wave started from
float _ShiftRadius;         // current radius of the wave front
float _ShiftEdgeWidth;      // thickness of the seam in metres
float _ShiftFrom;           // state outside the front
float _ShiftTo;             // state inside the front
float _ShiftSeam;           // seam brightness, 0 when no wave is running
float _ShiftSpeed;          // metres per second, used to time morphs behind the front
float4 _ShiftSeamColorDream;
float4 _ShiftSeamColorNight;

float4 _LucidPlayerPos;     // xyz = player chest, w = glow radius

float4 _LucidFogColorDream, _LucidFogColorNight;
float4 _LucidFogGlowDream, _LucidFogGlowNight;     // extra colour when looking toward the key light
float4 _LucidFogParamsDream, _LucidFogParamsNight; // x density, y height falloff, z start distance, w max opacity
float4 _LucidKeyDir;                               // unit vector toward the landmark light
float4 _LucidKeyColorDream, _LucidKeyColorNight;
float4 _LucidAmbientSkyDream, _LucidAmbientGroundDream;
float4 _LucidAmbientSkyNight, _LucidAmbientGroundNight;

float LucidHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float LucidNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(lerp(LucidHash(i + float3(0, 0, 0)), LucidHash(i + float3(1, 0, 0)), f.x),
             lerp(LucidHash(i + float3(0, 1, 0)), LucidHash(i + float3(1, 1, 0)), f.x), f.y),
        lerp(lerp(LucidHash(i + float3(0, 0, 1)), LucidHash(i + float3(1, 0, 1)), f.x),
             lerp(LucidHash(i + float3(0, 1, 1)), LucidHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

float LucidFbm(float3 p)
{
    return LucidNoise(p) * 0.6 + LucidNoise(p * 2.13 + 7.3) * 0.28 + LucidNoise(p * 4.31 + 2.1) * 0.12;
}

// Signed distance to the wave front in metres. Negative = the wave has already passed here.
float LucidFrontDistance(float3 positionWS)
{
    return distance(positionWS, _ShiftOrigin.xyz) - _ShiftRadius;
}

// Local world state for a surface point, with a wobbling front. seam is 0..1 at the front itself.
float LucidShift(float3 positionWS, out float seam)
{
    float w = max(_ShiftEdgeWidth, 0.01);
    float wobble = (LucidNoise(positionWS * 0.45 + _Time.y * 0.7) - 0.5) * w * 1.8;
    float x = LucidFrontDistance(positionWS) + wobble;
    float inside = 1.0 - smoothstep(-w, 0.0, x);
    seam = exp(-abs(x + w * 0.5) * (3.2 / w)) * _ShiftSeam;
    return lerp(_ShiftFrom, _ShiftTo, inside);
}

// Cheap version for vertices. The state eases over `seconds` after the front has passed,
// so blossoms fold and roots grow behind the wave instead of snapping.
float LucidShiftSoft(float3 positionWS, float seconds)
{
    float width = max(_ShiftSpeed * seconds, 0.5);
    float x = LucidFrontDistance(positionWS);
    float inside = 1.0 - smoothstep(-width, 0.0, x);
    inside = inside * inside * (3.0 - 2.0 * inside);
    return lerp(_ShiftFrom, _ShiftTo, inside);
}

half3 LucidSeamColor(float blend)
{
    return lerp(_ShiftSeamColorDream.rgb, _ShiftSeamColorNight.rgb, saturate(_ShiftTo));
}

// Distance and height fog. Points below the camera gather more mist, points above stay clearer,
// so the abyss fills in and the landmark stays visible. influence scales the result per material.
half3 LucidApplyFog(half3 color, float3 positionWS, float blend, float influence)
{
    float3 v = positionWS - _WorldSpaceCameraPos;
    float dist = length(v);
    float3 dir = v / max(dist, 0.0001);
    float4 p = lerp(_LucidFogParamsDream, _LucidFogParamsNight, blend);
    float height = clamp(exp(-p.y * v.y), 0.2, 3.5);
    float od = p.x * max(0.0, dist - p.z) * height;
    float f = saturate(1.0 - exp(-od)) * p.w * influence;
    half3 fog = lerp(_LucidFogColorDream.rgb, _LucidFogColorNight.rgb, blend);
    float toward = saturate(dot(dir, _LucidKeyDir.xyz));
    fog += lerp(_LucidFogGlowDream.rgb, _LucidFogGlowNight.rgb, blend) * (toward * toward * toward);
    return lerp(color, fog, f);
}

// Fog amount only, for additive shaders that fade out instead of taking the fog colour.
float LucidFogAmount(float3 positionWS, float blend, float influence)
{
    float3 v = positionWS - _WorldSpaceCameraPos;
    float dist = length(v);
    float4 p = lerp(_LucidFogParamsDream, _LucidFogParamsNight, blend);
    float height = clamp(exp(-p.y * v.y), 0.2, 3.5);
    float od = p.x * max(0.0, dist - p.z) * height;
    return saturate(1.0 - exp(-od)) * p.w * influence;
}

// 0..1, how close a point is to the player.
float LucidPlayerNear(float3 positionWS)
{
    float d = distance(positionWS, _LucidPlayerPos.xyz);
    float n = saturate(1.0 - d / max(_LucidPlayerPos.w, 0.01));
    return n * n;
}

#endif
