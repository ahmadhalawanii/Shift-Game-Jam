// Double-sided version of Lucid/World/Environment for leaves, petals, grass and blossoms.
// Adds light shining through thin surfaces. Wind, folding and bending come from the shared body.
Shader "Lucid/World/Foliage"
{
    Properties
    {
        [Header(Dream)]
        _DreamColor ("Dream lit", Color) = (0.4, 0.75, 0.85, 1)
        _DreamShade ("Dream shade", Color) = (0.18, 0.2, 0.5, 1)
        _DreamAccent ("Dream accent", Color) = (0.9, 0.55, 0.78, 1)
        [HDR] _DreamEmission ("Dream emission", Color) = (0, 0, 0, 1)
        [HDR] _DreamRim ("Dream rim", Color) = (0.75, 0.9, 1, 1)
        [Header(Nightmare)]
        _NightColor ("Nightmare lit", Color) = (0.1, 0.13, 0.22, 1)
        _NightShade ("Nightmare shade", Color) = (0.02, 0.03, 0.08, 1)
        _NightAccent ("Nightmare accent", Color) = (0.29, 0.12, 0.42, 1)
        [HDR] _NightEmission ("Nightmare emission", Color) = (0, 0, 0, 1)
        [HDR] _NightRim ("Nightmare rim", Color) = (0.4, 0.6, 0.75, 1)
        [HDR] _CorruptionColor ("Corruption veins", Color) = (0.6, 0.2, 0.95, 1)
        _Corruption ("Corruption amount", Range(0, 1)) = 0
        [Header(Shading)]
        _RimPower ("Rim power", Range(0.5, 8)) = 2.5
        _RampMid ("Ramp midpoint", Range(0, 1)) = 0.45
        _RampSoft ("Ramp softness", Range(0.01, 0.5)) = 0.22
        _VariationScale ("Paint variation scale", Float) = 0.6
        _VariationAmount ("Paint variation amount", Range(0, 1)) = 0.5
        _EmissionMask ("Emission uses vertex alpha", Range(0, 1)) = 0
        _MaskPower ("Emission mask power", Range(0.5, 8)) = 1
        _EmissionVeins ("Emission as thin veins", Range(0, 1)) = 0
        _Pulse ("Emission pulse", Range(0, 1)) = 0
        _PulseSpeed ("Pulse speed", Float) = 1.5
        _PlayerGlow ("Brighten near player (Dream)", Range(0, 4)) = 0
        _FogInfluence ("Fog influence", Range(0, 1)) = 1
        [Enum(Both, 0, DreamOnly, 1, NightmareOnly, 2)] _ExistIn ("Exists in", Float) = 0
        [Header(Motion)]
        _Wind ("Wind", Range(0, 1)) = 0.15
        _WindHeight ("Wind full height", Float) = 1
        _FoldDream ("Fold angle Dream (rad)", Float) = 0
        _FoldNight ("Fold angle Nightmare (rad)", Float) = 0
        _FoldHinge ("Fold hinge radius", Float) = 0.2
        _FoldHingeY ("Fold hinge height", Float) = 0
        _GrowDream ("Height scale Dream", Float) = 1
        _GrowNight ("Height scale Nightmare", Float) = 1
        _GrowWidth ("Width follows height", Range(0, 1)) = 0
        _MorphSeconds ("Morph seconds", Float) = 0.45
        _SeamPush ("Seam ripple", Range(0, 1)) = 0.1
        _PlayerBend ("Bend away from player", Range(0, 1)) = 0
        _LeanToPlayer ("Lean to player (Nightmare)", Range(0, 2)) = 0
        _Translucency ("Translucency", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex LucidVert
            #pragma fragment LucidFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_instancing
            #define LUCID_FOLIAGE 1
            #include "LucidSurface.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex LucidVert
            #pragma fragment LucidDepthFrag
            #pragma multi_compile_instancing
            #define LUCID_FOLIAGE 1
            #include "LucidSurface.hlsl"
            ENDHLSL
        }
    }
}
