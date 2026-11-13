Shader "Lucid/DreamSky"
{
    Properties
    {
        _Horizon ("Horizon", Color) = (1.0, 0.62, 0.42, 1)
        _Pink ("Pink", Color) = (1.0, 0.45, 0.72, 1)
        _Violet ("Violet", Color) = (0.6, 0.36, 0.86, 1)
        _Top ("Top", Color) = (0.13, 0.27, 0.78, 1)
        _VoidDir ("Void Direction", Vector) = (0.45, 0.5, 0.75, 0)
        _VoidSize ("Void Size (radians)", Range(0.02, 0.8)) = 0.12
        _Mix ("Nightmare Mix", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Horizon, _Pink, _Violet, _Top;
            float4 _VoidDir;
            float _VoidSize, _Mix;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(hash(i + float3(0, 0, 0)), hash(i + float3(1, 0, 0)), f.x),
                         lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x),
                         lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float fbm(float3 p)
            {
                float s = 0.0;
                float a = 0.5;
                for (int k = 0; k < 4; k++) { s += a * noise(p); p *= 2.03; a *= 0.5; }
                return s;
            }

            float3 hsv(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 vd = normalize(_VoidDir.xyz);
                float h = d.y;
                float t = saturate(h);

                // Dream gradient: orange horizon, pink, violet, deep blue.
                float3 c = lerp(_Horizon.rgb, _Pink.rgb, smoothstep(0.0, 0.12, t));
                c = lerp(c, _Violet.rgb, smoothstep(0.08, 0.32, t));
                c = lerp(c, _Top.rgb, smoothstep(0.25, 0.75, t));
                c = lerp(c, _Pink.rgb * 0.7, saturate(-h * 3.0));

                // Soft drifting clouds.
                float cl = fbm(float3(d.x * 1.5 + _Time.y * 0.012, d.y * 12.0, d.z * 1.5));
                float cm = smoothstep(0.45, 0.78, cl) * smoothstep(0.03, 0.2, h) * 0.7;
                c = lerp(c, float3(1.0, 0.62, 0.78), cm);

                // Local frame around the void direction.
                float3 rt = normalize(cross(float3(0, 1, 0), vd));
                float3 up2 = cross(vd, rt);
                float lx = dot(d, rt);
                float ly = dot(d, up2);
                float r = acos(saturate(dot(d, vd)));
                float a = atan2(ly, lx);

                // Dream: rainbow bending toward the void, then the black smudge.
                float rb = smoothstep(0.035, 0.0, abs(r - _VoidSize * 4.2)) * smoothstep(-0.4, 0.5, sin(a + 0.5));
                float3 dream = c + hsv(float3(saturate((r - _VoidSize * 3.6) / (_VoidSize * 1.4)) * 0.85, 0.55, 1.0)) * rb * 0.5;
                float edge = fbm(float3(cos(a) * 1.5, sin(a) * 1.5, r * 18.0 + _Time.y * 0.05)) * 0.5;
                float blob = smoothstep(_VoidSize * 1.7 + edge * 0.05, _VoidSize * 0.2, r);
                dream = lerp(dream, float3(0.01, 0.01, 0.03), saturate(blob));

                // Nightmare: fire vortex swirling into a black sun.
                // Swirl by rotating the direction, then sampling noise on cos/sin so there is no angle seam.
                float phi = 2.6 / (r * 2.2 + 0.3) - _Time.y * 0.12;
                float2 q = float2(cos(a + phi), sin(a + phi));
                float f = fbm(float3(q * 1.6, r * 4.0 + _Time.y * 0.03) + fbm(float3(q * 2.5, r * 7.0 + 1.7)) * 1.2);
                float glow = exp(-r * 3.0);
                float v = saturate(f * 1.0 + glow * 0.8 - 0.18);
                float3 n = lerp(float3(0.03, 0.005, 0.005), float3(0.3, 0.06, 0.02), smoothstep(0.0, 0.45, v));
                n = lerp(n, float3(0.95, 0.38, 0.06), smoothstep(0.45, 0.85, v));
                n = lerp(n, float3(1.0, 0.85, 0.55), smoothstep(0.85, 1.1, v) * glow);
                float disc = smoothstep(_VoidSize * 1.2, _VoidSize * 1.1, r);
                n = lerp(n, float3(0, 0, 0), disc);
                n = lerp(n, float3(0.04, 0.01, 0.01), saturate(-h * 2.5));

                return fixed4(lerp(dream, n, _Mix), 1.0);
            }
            ENDCG
        }
    }
}
