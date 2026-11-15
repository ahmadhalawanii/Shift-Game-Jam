Shader "Lucid/BossFeather"
{
    // Boss projectiles drawn as glowing feathers. Takes the same per-bullet properties as Lucid/EclipseOrb
    // (_Color and _Thorn), so every attack keeps its own colour. UVs: u across the vane, v along the shaft.
    Properties
    {
        _Color ("Colour", Color) = (0.2, 0.8, 1, 1)
        _Thorn ("Danger (0 soft, 1 sharp)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Thorn;
            CBUFFER_END
            struct A { float4 p : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; };

            V vert(A i)
            {
                V o;
                o.p = TransformObjectToHClip(i.p.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float u = abs(i.uv.x - 0.5) * 2.0, v = i.uv.y;
                // Shaft: a hot line down the middle that thins towards the tip.
                float shaft = 1.0 - smoothstep(0.0, lerp(0.16, 0.05, v), u);
                // Barbs sweep out and up from the shaft on both sides.
                float barbs = abs(frac(v * 22.0 - u * 5.5) - 0.5) * 2.0;
                float barbLine = (1.0 - smoothstep(0.25, 0.6, barbs)) * smoothstep(0.08, 0.3, u);
                // Light runs up the feather.
                float run = 0.75 + 0.25 * sin(v * 9.0 - _Time.y * 9.0);
                float edge = smoothstep(0.55, 1.0, u);
                float tip = smoothstep(0.55, 1.0, v);
                float3 hot = lerp(_Color.rgb, float3(1, 1, 1), 0.7);
                float3 body = _Color.rgb * (0.16 + 0.5 * barbLine * run + 0.25 * tip);
                float3 col = body + _Color.rgb * edge * (0.9 + 0.7 * _Thorn) * run + hot * shaft * 1.5;
                // Dangerous feathers get a darker blade so the bright edge reads as sharp.
                col = lerp(col, col * float3(0.75, 0.6, 0.7) + hot * shaft * 0.4, _Thorn * (1.0 - edge) * 0.5);
                return half4(col + float3(0.012, 0.008, 0.025), 1);
            }
            ENDHLSL
        }
    }
}
