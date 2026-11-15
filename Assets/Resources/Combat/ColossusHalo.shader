Shader "Lucid/ColossusHalo"
{
    // Additive disc of slowly turning light rays behind the Sleeper's head. UVs are centred on the quad.
    Properties
    {
        _Color ("Tint (a = strength)", Color) = (1, 0.85, 0.7, 0.6)
        _Rays ("Ray count", Float) = 14
        _Speed ("Turn speed", Float) = 0.12
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Rays;
                float _Speed;
            CBUFFER_END

            struct A { float3 p : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            float hash(float n) { return frac(sin(n * 127.1) * 43758.5453); }
            float noise(float x)
            {
                float i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(hash(i), hash(i + 1.0), f);
            }

            V vert(A a)
            {
                V o;
                o.pos = TransformObjectToHClip(a.p);
                o.uv = a.uv;
                return o;
            }

            // Rays repeat over a full turn so there is no seam at the angle wrap.
            float rays(float ang, float count, float t)
            {
                float x = (ang / 6.2831853 + 0.5) * count;
                float i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(fmod(i, count) + floor(t)), b = hash(fmod(i + 1.0, count) + floor(t));
                float a2 = hash(fmod(i, count) + floor(t) + 1.0), b2 = hash(fmod(i + 1.0, count) + floor(t) + 1.0);
                return lerp(lerp(a, b, f), lerp(a2, b2, f), smoothstep(0.0, 1.0, frac(t)));
            }

            half4 frag(V i) : SV_Target
            {
                float2 c = i.uv * 2.0 - 1.0;
                float r = length(c);
                float ang = atan2(c.y, c.x);
                float t = _Time.y * _Speed;
                float a = rays(ang + t, _Rays, t * 1.7) * 0.6 + rays(ang - t * 0.6, _Rays * 2.0 + 3.0, t * 2.3) * 0.4;
                a = pow(saturate(a), 2.2);
                float fall = pow(saturate(1.0 - r), 1.6);
                float core = pow(saturate(1.0 - r * 1.9), 2.0);
                float ring = exp(-pow((r - 0.46) * 16.0, 2.0)) * 0.35;
                float glow = fall * (0.18 + a * 1.25) + core * 0.5 + ring * fall * 2.0;
                return half4(_Color.rgb * 1.6, saturate(glow) * _Color.a);
            }
            ENDHLSL
        }
    }
}
