using System;
using System.Collections.Generic;
using UnityEngine;

// Procedural mesh toolkit for the redesigned world. Everything organic in the level (trunks, roots, rock,
// islands, petals, blossoms, grass, strands) is generated here, then saved as assets by WorldKit.
// Vertex colour: rgb multiplies the material palette (painted variation and soft occlusion),
// alpha is the emission mask (glowing veins, tips and handholds).
public class MeshData
{
    public readonly List<Vector3> v = new List<Vector3>();
    public readonly List<Vector3> n = new List<Vector3>();
    public readonly List<Color> c = new List<Color>();
    public readonly List<Vector2> uv = new List<Vector2>();
    public readonly List<int> t = new List<int>();
    public bool hasNormals = true;

    public int Add(Vector3 p, Color col) { return Add(p, Vector3.up, col, Vector2.zero); }
    public int Add(Vector3 p, Vector3 normal, Color col) { return Add(p, normal, col, Vector2.zero); }
    public int Add(Vector3 p, Vector3 normal, Color col, Vector2 texcoord)
    {
        v.Add(p); n.Add(normal); c.Add(col); uv.Add(texcoord);
        return v.Count - 1;
    }

    public void Tri(int a, int b, int d) { t.Add(a); t.Add(b); t.Add(d); }
    public void Quad(int a, int b, int d, int e) { Tri(a, b, d); Tri(a, d, e); }

    // Merge another mesh in, transformed.
    public MeshData Append(MeshData o, Matrix4x4 m)
    {
        int start = v.Count;
        for (int i = 0; i < o.v.Count; i++)
        {
            v.Add(m.MultiplyPoint3x4(o.v[i]));
            n.Add(m.MultiplyVector(o.n[i]).normalized);
            c.Add(o.c[i]);
            uv.Add(o.uv[i]);
        }
        // A mirrored transform flips the winding.
        bool flip = m.determinant < 0f;
        for (int i = 0; i < o.t.Count; i += 3)
        {
            t.Add(start + o.t[i]);
            t.Add(start + o.t[i + (flip ? 2 : 1)]);
            t.Add(start + o.t[i + (flip ? 1 : 2)]);
        }
        return this;
    }

    public MeshData Append(MeshData o) { return Append(o, Matrix4x4.identity); }
    public MeshData Append(MeshData o, Vector3 pos, Quaternion rot, Vector3 scale) { return Append(o, Matrix4x4.TRS(pos, rot, scale)); }
    public MeshData Append(MeshData o, Vector3 pos, Quaternion rot, float scale) { return Append(o, Matrix4x4.TRS(pos, rot, Vector3.one * scale)); }

    public MeshData Tint(Func<Vector3, Color, Color> f)
    {
        for (int i = 0; i < c.Count; i++) c[i] = f(v[i], c[i]);
        return this;
    }

    // Every generator stores the direction each vertex is meant to face. This turns each triangle so its
    // front agrees with that, so no generator has to get the winding order right by hand.
    public void FixWinding()
    {
        for (int i = 0; i < t.Count; i += 3)
        {
            int a = t[i], b = t[i + 1], d = t[i + 2];
            Vector3 face = Vector3.Cross(v[b] - v[a], v[d] - v[a]);
            if (Vector3.Dot(face, n[a] + n[b] + n[d]) < 0f) { t[i + 1] = d; t[i + 2] = b; }
        }
    }

    // smooth: let Unity average normals over shared vertices. Otherwise the stored normals are kept.
    public Mesh ToMesh(string name, bool smooth = true, bool withUV = false)
    {
        FixWinding();
        var mesh = new Mesh { name = name };
        if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v);
        mesh.SetColors(c);
        if (withUV) mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        if (smooth) mesh.RecalculateNormals(); else mesh.SetNormals(n);
        mesh.RecalculateBounds();
        return mesh;
    }
}

public static class WorldMeshes
{
    // ---------- Noise ----------

    public static float P3(Vector3 p)
    {
        return (Mathf.PerlinNoise(p.x + 31.7f, p.y + 11.3f) + Mathf.PerlinNoise(p.y + 71.1f, p.z + 5.9f) + Mathf.PerlinNoise(p.z + 13.4f, p.x + 47.2f)) / 3f;
    }

    public static float Fbm(Vector3 p)
    {
        return P3(p) * 0.6f + P3(p * 2.13f + Vector3.one * 7.3f) * 0.28f + P3(p * 4.31f + Vector3.one * 2.1f) * 0.12f;
    }

    static Vector3 Seed(int seed) { return new Vector3(seed * 12.9898f % 97f, seed * 78.233f % 89f, seed * 37.719f % 83f); }

    public static Color Shade(Color a, float k) { return new Color(a.r * k, a.g * k, a.b * k, a.a); }
    public static Color Alpha(Color a, float alpha) { return new Color(a.r, a.g, a.b, alpha); }
    public static Color Grey(float k, float alpha = 0f) { return new Color(k, k, k, alpha); }

    // ---------- Curves ----------

    // Smooth path through the control points (Catmull-Rom), resampled to `samples` points.
    public static List<Vector3> Curve(int samples, params Vector3[] ctrl)
    {
        var pts = new List<Vector3>(samples);
        int segs = ctrl.Length - 1;
        for (int i = 0; i < samples; i++)
        {
            float u = (float)i / (samples - 1) * segs;
            int k = Mathf.Min(segs - 1, Mathf.FloorToInt(u));
            float f = u - k;
            Vector3 p0 = ctrl[Mathf.Max(0, k - 1)], p1 = ctrl[k], p2 = ctrl[k + 1], p3 = ctrl[Mathf.Min(ctrl.Length - 1, k + 2)];
            pts.Add(0.5f * ((2f * p1) + (-p0 + p2) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f));
        }
        return pts;
    }

    // ---------- Tube: trunks, roots, branches, thorns, vines ----------

    // radius(t) and color(t) take t = 0 at the start of the path and 1 at the end.
    // alpha(t, side) returns the emission mask, for glowing veins along the bark.
    public static MeshData Tube(IList<Vector3> path, Func<float, float> radius, int sides, int seed,
        float bump, float bumpFreq, Func<float, Color> color, float squashY = 1f, bool capStart = false, bool capEnd = true,
        Func<float, int, float> alpha = null)
    {
        var m = new MeshData();
        Vector3 s = Seed(seed);
        int count = path.Count;
        Vector3 tangent = (path[1] - path[0]).normalized;
        Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            Vector3 next = i < count - 1 ? path[i + 1] : path[i];
            Vector3 prev = i > 0 ? path[i - 1] : path[i];
            tangent = (next - prev).normalized;
            normal = (normal - tangent * Vector3.Dot(normal, tangent)).normalized;
            Vector3 binormal = Vector3.Cross(tangent, normal);
            float r = radius(t);
            Color col = color(t);
            for (int j = 0; j < sides; j++)
            {
                float a = j * Mathf.PI * 2f / sides;
                Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                float noise = Fbm((path[i] + dir * 0.6f) * bumpFreq + s);
                float rr = r * (1f + bump * (noise - 0.5f) * 2f);
                Vector3 offset = dir * rr;
                offset.y *= squashY;
                Color vc = Shade(col, 0.82f + noise * 0.4f);
                vc.a = alpha != null ? alpha(t, j) : 0f;
                m.Add(path[i] + offset, dir, vc);
            }
        }
        for (int i = 0; i < count - 1; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * sides + j, b = i * sides + (j + 1) % sides;
                m.Quad(a, a + sides, b + sides, b);
            }
        if (capEnd)
        {
            Vector3 endT = (path[count - 1] - path[count - 2]).normalized;
            int tip = m.Add(path[count - 1] + endT * radius(1f) * 0.6f, endT, color(1f));
            int ring = (count - 1) * sides;
            for (int j = 0; j < sides; j++) m.Tri(ring + j, tip, ring + (j + 1) % sides);
        }
        if (capStart)
        {
            Vector3 startT = (path[0] - path[1]).normalized;
            int tip = m.Add(path[0] + startT * radius(0f) * 0.6f, startT, color(0f));
            for (int j = 0; j < sides; j++) m.Tri((j + 1) % sides, tip, j);
        }
        return m;
    }

    // ---------- Blob: rocks, boulders, cliffs chunks, skull parts ----------

    // A displaced sphere. warp can reshape the result (flatten, pinch, dent); color sees the final position.
    public static MeshData Blob(int rings, int segs, Vector3 scale, int seed, float bump, float freq,
        Func<Vector3, Vector3> warp = null, Func<Vector3, Vector3, Color> color = null)
    {
        var m = new MeshData();
        Vector3 s = Seed(seed);
        Func<Vector3, int> add = dir =>
        {
            float noise = Fbm(dir * freq + s);
            Vector3 p = Vector3.Scale(dir * (1f + bump * (noise - 0.5f) * 2f), scale);
            if (warp != null) p = warp(p);
            Color col = color != null ? color(p, dir) : Grey(0.78f + noise * 0.4f);
            return m.Add(p, dir, col);
        };
        int top = add(Vector3.up);
        for (int r = 1; r < rings; r++)
        {
            float phi = Mathf.PI * r / rings;
            for (int j = 0; j < segs; j++)
            {
                float a = j * Mathf.PI * 2f / segs;
                add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(a), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(a)));
            }
        }
        int bottom = add(Vector3.down);
        for (int j = 0; j < segs; j++) m.Tri(top, 1 + (j + 1) % segs, 1 + j);
        for (int r = 0; r < rings - 2; r++)
            for (int j = 0; j < segs; j++)
            {
                int a = 1 + r * segs + j, b = 1 + r * segs + (j + 1) % segs;
                m.Quad(a, b, b + segs, a + segs);
            }
        int last = 1 + (rings - 2) * segs;
        for (int j = 0; j < segs; j++) m.Tri(bottom, last + j, last + (j + 1) % segs);
        return m;
    }

    // ---------- Island: flat-topped platform with a rocky underside ----------

    public static float IslandEdge(float radius, float angle, int seed, float edgeNoise)
    {
        return radius * (1f + edgeNoise * (Mathf.PerlinNoise(Mathf.Cos(angle) * 1.3f + seed * 3.17f, Mathf.Sin(angle) * 1.3f + seed * 1.31f) - 0.5f) * 2f);
    }

    public static MeshData Island(float radius, float depth, int seed, Color top, Color rim, Color bottom, int segs = 28, float edgeNoise = 0.16f)
    {
        var m = new MeshData();
        Vector3 s = Seed(seed);
        // Top: flat for gameplay, with a soft lip.
        int centre = m.Add(Vector3.zero, Vector3.up, top);
        float[] fr = { 0.5f, 0.86f, 1f };
        float[] drop = { 0f, 0f, -0.12f };
        for (int k = 0; k < 3; k++)
            for (int j = 0; j < segs; j++)
            {
                float a = j * Mathf.PI * 2f / segs;
                float R = IslandEdge(radius, a, seed, edgeNoise) * fr[k];
                var p = new Vector3(Mathf.Cos(a) * R, drop[k], Mathf.Sin(a) * R);
                float noise = Fbm(p * 0.5f + s);
                Color col = Color.Lerp(top, rim, k == 2 ? 1f : k * 0.3f);
                m.Add(p, Vector3.up, Shade(col, 0.85f + noise * 0.3f));
            }
        for (int j = 0; j < segs; j++) m.Tri(centre, 1 + (j + 1) % segs, 1 + j);
        for (int k = 0; k < 2; k++)
            for (int j = 0; j < segs; j++)
            {
                int a = 1 + k * segs + j, b = 1 + k * segs + (j + 1) % segs;
                m.Quad(a, b, b + segs, a + segs);
            }
        // Underside: separate vertices so the top edge stays crisp.
        int side = m.v.Count;
        const int layers = 5;
        for (int k = 0; k <= layers; k++)
        {
            float f = (float)k / layers;
            for (int j = 0; j < segs; j++)
            {
                float a = j * Mathf.PI * 2f / segs;
                float R = IslandEdge(radius, a, seed, edgeNoise);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float noise = Fbm(dir * 1.7f + Vector3.up * f * 2.3f + s);
                float rr = R * (1f - Mathf.Pow(f, 1.5f) * 0.92f) * (k == 0 ? 1f : 0.86f + noise * 0.3f);
                var p = dir * rr + Vector3.up * (-0.12f - depth * Mathf.Pow(f, 0.85f));
                m.Add(p, dir, Shade(Color.Lerp(rim, bottom, Mathf.Sqrt(f)), 0.8f + noise * 0.4f));
            }
        }
        for (int k = 0; k < layers; k++)
            for (int j = 0; j < segs; j++)
            {
                int a = side + k * segs + j, b = side + k * segs + (j + 1) % segs;
                m.Quad(a, a + segs, b + segs, b);
            }
        int apex = m.Add(new Vector3(0f, -depth * 1.08f - 0.12f, 0f), Vector3.down, bottom);
        int lastRing = side + layers * segs;
        for (int j = 0; j < segs; j++) m.Tri(lastRing + j, apex, lastRing + (j + 1) % segs);
        return m;
    }

    // Simple convex collider shape for platforms: a low cylinder with its top at y = 0.
    public static MeshData Puck(float radius, float thickness, int segs = 14)
    {
        var m = new MeshData();
        int top = m.Add(Vector3.zero, Vector3.up, Color.white);
        int bot = m.Add(Vector3.down * thickness, Vector3.down, Color.white);
        for (int j = 0; j < segs; j++)
        {
            float a = j * Mathf.PI * 2f / segs;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            m.Add(d * radius, d, Color.white);
            m.Add(d * radius * 0.85f + Vector3.down * thickness, d, Color.white);
        }
        for (int j = 0; j < segs; j++)
        {
            int a = 2 + j * 2, b = 2 + ((j + 1) % segs) * 2;
            m.Tri(top, b, a);
            m.Tri(bot, a + 1, b + 1);
            m.Quad(a, b, b + 1, a + 1);
        }
        return m;
    }

    // ---------- Lathe: mushrooms, wells, lanterns, crystals ----------

    // profile: x = radius, y = height, listed along the outside of the solid from the bottom to the top.
    // color(i) gives the colour (and emission alpha) of profile point i.
    public static MeshData Lathe(Vector2[] profile, int sides, Func<int, Color> color, int seed = 0, float wobble = 0f)
    {
        var m = new MeshData();
        Vector3 s = Seed(seed);
        for (int i = 0; i < profile.Length; i++)
        {
            // Outward direction of the profile at this point: right of the direction of travel.
            Vector2 along = profile[Mathf.Min(profile.Length - 1, i + 1)] - profile[Mathf.Max(0, i - 1)];
            Vector2 outward = new Vector2(along.y, -along.x).normalized;
            for (int j = 0; j < sides; j++)
            {
                float a = j * Mathf.PI * 2f / sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float noise = wobble > 0f ? Fbm(dir * 1.9f + Vector3.up * profile[i].y * 0.8f + s) : 0.5f;
                float r = profile[i].x * (1f + wobble * (noise - 0.5f) * 2f);
                Color col = color(i);
                float keepAlpha = col.a;
                col = Shade(col, 0.88f + noise * 0.24f);
                col.a = keepAlpha;
                m.Add(dir * r + Vector3.up * profile[i].y, dir * outward.x + Vector3.up * outward.y, col);
            }
        }
        for (int i = 0; i < profile.Length - 1; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * sides + j, b = i * sides + (j + 1) % sides;
                m.Quad(a, a + sides, b + sides, b);
            }
        return m;
    }

    // ---------- Petals, blossoms, leaves ----------

    // Base at the origin, growing along +Z, lying flat. curl bends the tip upward (radians over the length).
    public static MeshData Petal(float length, float width, float curl, float cup, int segs, Color baseColor, Color tipColor,
        float veinAlpha = 0f, float tipAlpha = 0f, float baseWidth = 0.18f)
    {
        var m = new MeshData();
        Vector3 pos = Vector3.zero;
        float ds = length / segs;
        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs;
            float theta = curl * t;
            Vector3 forward = new Vector3(0f, Mathf.Sin(theta), Mathf.Cos(theta));
            Vector3 up = new Vector3(0f, Mathf.Cos(theta), -Mathf.Sin(theta));
            if (i > 0) pos += forward * ds;
            float w = width * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.72f)), 0.8f);
            w = Mathf.Max(w, width * 0.5f * baseWidth * (1f - t));
            Color col = Color.Lerp(baseColor, tipColor, t);
            float edgeAlpha = tipAlpha * t * t;
            m.Add(pos - Vector3.right * w + up * (cup * w), up, Alpha(Shade(col, 0.92f), edgeAlpha));
            m.Add(pos, up, Alpha(col, Mathf.Max(veinAlpha, edgeAlpha)));
            m.Add(pos + Vector3.right * w + up * (cup * w), up, Alpha(Shade(col, 0.92f), edgeAlpha));
        }
        for (int i = 0; i < segs; i++)
        {
            int a = i * 3;
            m.Quad(a, a + 3, a + 4, a + 1);
            m.Quad(a + 1, a + 4, a + 5, a + 2);
        }
        return m;
    }

    // A ring of petals around the Y axis, hinged at `hinge` so the Foliage shader can fold it shut.
    public static MeshData PetalRing(int petals, float hinge, float tiltDegrees, float yawOffset, MeshData petal, int seed = 0)
    {
        var m = new MeshData();
        for (int i = 0; i < petals; i++)
        {
            float yaw = yawOffset + i * 360f / petals + (Mathf.PerlinNoise(i * 1.7f + seed, 0.3f) - 0.5f) * 14f;
            float tilt = tiltDegrees + (Mathf.PerlinNoise(i * 2.3f + seed, 5.1f) - 0.5f) * 10f;
            var rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-tilt, 0f, 0f);
            m.Append(petal, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * hinge, rot, Vector3.one);
        }
        return m;
    }

    // ---------- Grass and strands ----------

    public static MeshData GrassClump(int blades, float height, float radius, int seed, Color baseColor, Color tipColor, float tipAlpha = 1f)
    {
        var m = new MeshData();
        var rng = new System.Random(seed);
        for (int b = 0; b < blades; b++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float d = Mathf.Sqrt((float)rng.NextDouble()) * radius;
            float h = height * (0.55f + (float)rng.NextDouble() * 0.6f);
            float w = 0.045f + (float)rng.NextDouble() * 0.04f;
            float lean = 0.15f + (float)rng.NextDouble() * 0.5f;
            float yaw = (float)rng.NextDouble() * Mathf.PI * 2f;
            var root = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            var bend = new Vector3(-side.z, 0f, side.x);
            int start = m.v.Count;
            for (int k = 0; k < 3; k++)
            {
                float t = k / 3f;
                Vector3 p = root + Vector3.up * h * t + bend * (lean * h * t * t);
                Color col = Color.Lerp(baseColor, tipColor, t);
                col.a = tipAlpha * t * t;
                m.Add(p - side * w * (1f - t * 0.6f), bend, col);
                m.Add(p + side * w * (1f - t * 0.6f), bend, col);
            }
            Color tip = tipColor; tip.a = tipAlpha;
            int apex = m.Add(root + Vector3.up * h + bend * (lean * h), bend, tip);
            for (int k = 0; k < 2; k++)
            {
                int q = start + k * 2;
                m.Quad(q, q + 2, q + 3, q + 1);
            }
            m.Tri(start + 4, apex, start + 5);
        }
        return m;
    }

    // Hanging ribbons for Lucid/World/Glow (Strand shape). uv.y runs 0 at the root to 1 at the tip.
    public static MeshData Strands(IList<Vector3> roots, Func<int, float> length, float width, int seed, int segs = 4, float drift = 0.35f)
    {
        var m = new MeshData();
        var rng = new System.Random(seed);
        for (int i = 0; i < roots.Count; i++)
        {
            float len = length(i);
            float yaw = (float)rng.NextDouble() * Mathf.PI * 2f;
            var side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            var sway = new Vector3(-side.z, 0f, side.x) * ((float)rng.NextDouble() - 0.5f) * drift * len;
            float bright = 0.55f + (float)rng.NextDouble() * 0.45f;
            var col = new Color(bright, bright, bright, 1f);
            int start = m.v.Count;
            for (int k = 0; k <= segs; k++)
            {
                float t = (float)k / segs;
                Vector3 p = roots[i] + Vector3.down * (len * t) + sway * (t * t);
                float w = width * (1f - t * 0.5f);
                m.Add(p - side * w, Vector3.forward, col, new Vector2(0f, t));
                m.Add(p + side * w, Vector3.forward, col, new Vector2(1f, t));
            }
            for (int k = 0; k < segs; k++)
            {
                int q = start + k * 2;
                m.Quad(q, q + 2, q + 3, q + 1);
            }
        }
        return m;
    }

    // ---------- Simple shapes ----------

    // Unit quad in the XY plane, centred, with UVs. Used by cards and billboards.
    public static MeshData Quad()
    {
        var m = new MeshData();
        m.Add(new Vector3(-0.5f, -0.5f, 0f), Vector3.back, Color.white, new Vector2(0f, 0f));
        m.Add(new Vector3(0.5f, -0.5f, 0f), Vector3.back, Color.white, new Vector2(1f, 0f));
        m.Add(new Vector3(0.5f, 0.5f, 0f), Vector3.back, Color.white, new Vector2(1f, 1f));
        m.Add(new Vector3(-0.5f, 0.5f, 0f), Vector3.back, Color.white, new Vector2(0f, 1f));
        m.Quad(0, 3, 2, 1);
        return m;
    }

    // Flat unit quad in the XZ plane (mist planes, ground glows).
    public static MeshData FlatQuad()
    {
        var m = new MeshData();
        m.Add(new Vector3(-0.5f, 0f, -0.5f), Vector3.up, Color.white, new Vector2(0f, 0f));
        m.Add(new Vector3(0.5f, 0f, -0.5f), Vector3.up, Color.white, new Vector2(1f, 0f));
        m.Add(new Vector3(0.5f, 0f, 0.5f), Vector3.up, Color.white, new Vector2(1f, 1f));
        m.Add(new Vector3(-0.5f, 0f, 0.5f), Vector3.up, Color.white, new Vector2(0f, 1f));
        m.Quad(0, 3, 2, 1);
        return m;
    }

    // Light shaft: two crossed tapered quads hanging down from the origin. uv.y runs 0 at the top to 1 at the bottom.
    public static MeshData Shaft(float length, float topWidth, float bottomWidth)
    {
        var m = new MeshData();
        for (int k = 0; k < 2; k++)
        {
            Vector3 side = k == 0 ? Vector3.right : Vector3.forward;
            int a = m.Add(-side * topWidth * 0.5f, Vector3.up, Color.white, new Vector2(0f, 0f));
            int b = m.Add(side * topWidth * 0.5f, Vector3.up, Color.white, new Vector2(1f, 0f));
            int d = m.Add(side * bottomWidth * 0.5f + Vector3.down * length, Vector3.up, Color.white, new Vector2(1f, 1f));
            int e = m.Add(-side * bottomWidth * 0.5f + Vector3.down * length, Vector3.up, Color.white, new Vector2(0f, 1f));
            m.Quad(a, b, d, e);
        }
        return m;
    }

    public static MeshData Sphere(int rings, int segs)
    {
        return Blob(rings, segs, Vector3.one * 0.5f, 0, 0f, 1f, null, (p, d) => Color.white);
    }

    // Curved cone: thorns, teeth, horns, claws.
    public static MeshData Thorn(float length, float radius, float bend, int sides = 6, int segs = 5, Color? baseColor = null, Color? tipColor = null, float tipAlpha = 0f)
    {
        Color c0 = baseColor ?? Grey(0.9f), c1 = tipColor ?? Grey(0.6f);
        var path = new List<Vector3>();
        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs;
            path.Add(new Vector3(bend * t * t * length, length * t, 0f));
        }
        return Tube(path, t => Mathf.Max(0.004f, radius * (1f - t)), sides, 3, 0f, 1f,
            t => Color.Lerp(c0, c1, t), 1f, false, true, (t, j) => tipAlpha * t * t);
    }

    // ---------- Cliff: a displaced wall facing -X (the play side), spanning Z and Y ----------

    public static MeshData Cliff(float length, float height, int cols, int rows, int seed, float bump, Color topColor, Color bottomColor)
    {
        var m = new MeshData();
        Vector3 s = Seed(seed);
        for (int r = 0; r <= rows; r++)
            for (int k = 0; k <= cols; k++)
            {
                float u = (float)k / cols, w = (float)r / rows;
                var p = new Vector3(0f, w * height, (u - 0.5f) * length);
                float strata = Mathf.Sin(w * 19f + Fbm(p * 0.07f + s) * 6f) * 0.5f + 0.5f;
                float noise = Fbm(new Vector3(p.z * 0.11f, p.y * 0.16f, 0f) + s);
                float push = (noise - 0.5f) * 2f * bump + strata * bump * 0.25f;
                // Lean back slightly toward the top, and swell at the foot.
                p.x = -push + w * height * 0.08f - (1f - w) * (1f - w) * bump * 0.8f;
                Color col = Color.Lerp(bottomColor, topColor, w);
                m.Add(p, Vector3.left, Shade(col, 0.72f + noise * 0.4f + strata * 0.12f));
            }
        for (int r = 0; r < rows; r++)
            for (int k = 0; k < cols; k++)
            {
                int a = r * (cols + 1) + k;
                m.Quad(a, a + 1, a + cols + 2, a + cols + 1);
            }
        return m;
    }

    // ---------- Shell: the inside of a hollow tree ----------

    // Cylinder wall around Y from y = 0 to height. skip(angle01, height01) leaves openings.
    // Normals face inward (the player is inside) when inward is true.
    public static MeshData Shell(float radius, float height, int segs, int rows, int seed, float bump, bool inward,
        Func<float, float, bool> skip, Func<float, Color> color, Func<float, float> radiusScale = null, Func<float, int, float> alpha = null)
    {
        var m = new MeshData();
        Vector3 s = Seed(seed);
        for (int r = 0; r <= rows; r++)
            for (int j = 0; j < segs; j++)
            {
                float a = j * Mathf.PI * 2f / segs;
                float w = (float)r / rows;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float noise = Fbm(dir * 2.1f + Vector3.up * w * height * 0.14f + s);
                float rr = radius * (radiusScale != null ? radiusScale(w) : 1f) * (1f + bump * (noise - 0.5f) * 2f);
                // Vertical ribs, like the folds of old bark.
                rr *= 1f + Mathf.Sin(a * 9f + noise * 4f) * bump * 0.5f;
                Color col = Shade(color(w), 0.75f + noise * 0.45f);
                col.a = alpha != null ? alpha(w, j) : 0f;
                m.Add(dir * rr + Vector3.up * (w * height), inward ? -dir : dir, col);
            }
        for (int r = 0; r < rows; r++)
            for (int j = 0; j < segs; j++)
            {
                float a01 = (j + 0.5f) / segs, h01 = (r + 0.5f) / rows;
                if (skip != null && skip(a01, h01)) continue;
                int a = r * segs + j, b = r * segs + (j + 1) % segs;
                if (inward) m.Quad(a, a + segs, b + segs, b); else m.Quad(a, b, b + segs, a + segs);
            }
        return m;
    }
}
