using System;
using System.Collections.Generic;
using UnityEditor;
using static BuildKit;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the Dreamwalker (the player figure): one low-poly mesh, its material, the "Lucid" visual prefab,
// and the smoke material used by the dash. The mesh has no skeleton; Lucid/Dreamwalker animates it from the
// vertex colours written here (r = part / 8, g = flow weight, b = painted shade, a = glow mask).
public static class MemoryBuilder
{
    const int Body = 0, Cloak = 1, Ribbon = 2, HandL = 3, HandR = 4, LegL = 5, LegR = 6, Core = 7;
    const float Tau = Mathf.PI * 2f;

    static readonly List<Vector3> V = new List<Vector3>();
    static readonly List<Color> C = new List<Color>();
    static readonly List<Vector2> U = new List<Vector2>();
    static readonly List<int> T = new List<int>();

    static Color Col(int part, float flow, float shade, float glow) { return new Color(part / 8f, flow, shade, glow); }

    // Direction around the body: a = 0 is the back (-Z), 0.5 the front (+Z).
    static Vector3 Dir(float a) { return new Vector3(Mathf.Sin(a * Tau), 0f, -Mathf.Cos(a * Tau)); }

    // A grid of rings, each sampled from a0 to a1 around the body. uv.x is a unless u is given.
    static void Loft(int rings, int sides, float a0, float a1, Func<int, float, Vector3> pos, Func<int, float, Color> col, float? u = null)
    {
        int start = V.Count, cols = sides + 1;
        for (int i = 0; i < rings; i++)
            for (int j = 0; j <= sides; j++)
            {
                float a = Mathf.Lerp(a0, a1, j / (float)sides);
                V.Add(pos(i, a)); C.Add(col(i, a)); U.Add(new Vector2(u ?? a, i / (rings - 1f)));
            }
        for (int i = 0; i < rings - 1; i++)
            for (int j = 0; j < sides; j++)
            {
                int p = start + i * cols + j;
                T.Add(p); T.Add(p + cols); T.Add(p + cols + 1);
                T.Add(p); T.Add(p + cols + 1); T.Add(p + 1);
            }
    }

    static void Ball(Vector3 c, Vector3 r, Color col, int rings = 6, int segs = 8)
    {
        Loft(rings, segs, 0f, 1f, (i, a) =>
        {
            float lat = Mathf.PI * i / (rings - 1f);
            Vector3 d = Dir(a) * Mathf.Sin(lat) - Vector3.up * Mathf.Cos(lat);
            return c + Vector3.Scale(d, r);
        }, (i, a) => col);
    }

    static void Leg(float x, int part)
    {
        float[] y = { 0f, 0.03f, 0.3f, 0.64f }, r = { 0.004f, 0.052f, 0.034f, 0.06f };
        Loft(4, 7, 0f, 1f, (i, a) => new Vector3(x, y[i], i < 2 ? 0.025f : 0f) + Dir(a) * r[i], (i, a) => Col(part, 0f, 1f, 0f));
    }

    static Mesh BuildMesh()
    {
        V.Clear(); C.Clear(); U.Clear(); T.Clear();

        Leg(-0.09f, LegL);
        Leg(0.09f, LegR);

        // Torso under the cloak, kept dark so the heart-core reads against it.
        float[] ty = { 0.55f, 0.9f, 1.2f, 1.34f }, tr = { 0.11f, 0.15f, 0.14f, 0.08f };
        Loft(4, 9, 0f, 1f, (i, a) => new Vector3(0f, ty[i], 0f) + Dir(a) * tr[i], (i, a) => Col(Body, 0f, 0.42f, 0f));

        // Cloak: open at the front so the core shows, longer at the back, with soft folds.
        const float open = 0.39f;
        Loft(10, 24, -open, open, (i, a) =>
        {
            float s = i / 9f;
            float back = 1f - Mathf.Abs(a) / open;
            float hem = Mathf.Lerp(0.56f, 0.26f, back);
            float r = Mathf.Lerp(0.2f, 0.45f, Mathf.Pow(s, 0.8f)) * (1f + 0.07f * s * Mathf.Sin(a * Tau * 7f));
            Vector3 d = Dir(a);
            d.z *= 1f + 0.2f * back * s;
            return new Vector3(0f, Mathf.Lerp(1.31f, hem, s), 0f) + d * r;
        }, (i, a) => Col(Cloak, i / 9f, 0.88f + 0.12f * Mathf.Cos(a * Tau * 7f), 0f));

        // Mantle over the shoulders.
        float[] my = { 1.38f, 1.33f, 1.24f, 1.17f }, mr = { 0.09f, 0.17f, 0.26f, 0.29f };
        Loft(4, 14, 0f, 1f, (i, a) => new Vector3(0f, my[i], 0f) + Dir(a) * mr[i], (i, a) => Col(Body, 0f, 1.04f, 0f));

        // Hood: a dome with a dark face hollow at the front and a soft peak falling back.
        Vector3 hc = new Vector3(0f, 1.52f, -0.01f), hr = new Vector3(0.2f, 0.215f, 0.225f);
        Func<int, float, float> hollow = (i, a) =>
        {
            float lat = Mathf.PI * i / 9f;
            Vector3 d = Dir(a) * Mathf.Sin(lat) - Vector3.up * Mathf.Cos(lat);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.8f, d.z)) * (Mathf.Abs(d.y) < 0.8f ? 1f : 0f);
        };
        Loft(10, 16, 0f, 1f, (i, a) =>
        {
            float lat = Mathf.PI * i / 9f;
            Vector3 d = Dir(a) * Mathf.Sin(lat) - Vector3.up * Mathf.Cos(lat);
            Vector3 p = hc + Vector3.Scale(d, hr);
            p = Vector3.Lerp(p, hc + new Vector3(d.x * hr.x * 0.6f, d.y * hr.y * 0.7f, 0.04f), hollow(i, a));
            float peak = Mathf.Clamp01(d.y) * Mathf.Clamp01(-d.z + 0.3f);
            return p + new Vector3(0f, 0.05f, -0.16f) * peak;
        }, (i, a) => Col(Body, 0f, Mathf.Lerp(1f, 0.08f, hollow(i, a)), 0f));

        // Eyes, heart-core, floating hands.
        Ball(new Vector3(-0.065f, 1.51f, 0.075f), Vector3.one * 0.026f, Col(Body, 0f, 1f, 0.9f), 5, 6);
        Ball(new Vector3(0.065f, 1.51f, 0.075f), Vector3.one * 0.026f, Col(Body, 0f, 1f, 0.9f), 5, 6);
        Ball(new Vector3(0f, 1.03f, 0.15f), Vector3.one * 0.075f, Col(Core, 0f, 1f, 1f), 7, 10);
        Ball(new Vector3(-0.3f, 0.95f, 0.08f), new Vector3(0.055f, 0.07f, 0.06f), Col(HandL, 0f, 1f, 0.22f));
        Ball(new Vector3(0.3f, 0.95f, 0.08f), new Vector3(0.055f, 0.07f, 0.06f), Col(HandR, 0f, 1f, 0.22f));

        // Ribbons of dream-cloth from the back of the shoulders.
        float[] rx = { -0.13f, 0f, 0.13f }, rl = { 0.85f, 1.08f, 0.8f };
        for (int k = 0; k < 3; k++)
        {
            int n = k;
            Loft(9, 1, 0f, 1f, (i, a) =>
            {
                float s = i / 8f;
                Vector3 root = new Vector3(rx[n], 1.27f, -0.18f);
                return root + new Vector3(rx[n] * 0.5f * s + (a - 0.5f) * 0.08f * (1f - 0.6f * s), -rl[n] * 0.9f * s, -0.14f * s - 0.1f * s * s);
            }, (i, a) => Col(Ribbon, i / 8f, 0.95f, 0f), 0.17f + n * 0.31f);
        }

        var mesh = new Mesh { name = "Dreamwalker" };
        mesh.SetVertices(V);
        mesh.SetColors(C);
        mesh.SetUVs(0, U);
        mesh.SetTriangles(T, 0);
        mesh.RecalculateNormals();
        mesh.bounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(3.5f, 3.5f, 3.5f)); // room for cloth and the dissolve
        return mesh;
    }

    public static void Build()
    {
        Ensure("Assets/Meshes"); Ensure("Assets/Materials"); Ensure("Assets/Materials/Memory"); Ensure("Assets/Prefabs"); Ensure("Assets/Prefabs/Props");
        Ensure("Assets/Resources"); Ensure("Assets/Resources/Combat");

        // The old point-cloud figure is replaced by the Dreamwalker.
        AssetDatabase.DeleteAsset("Assets/Meshes/MemoryFigure.asset");
        AssetDatabase.DeleteAsset("Assets/Materials/Memory/MemoryFigure.mat");

        string meshPath = "Assets/Meshes/Dreamwalker.asset";
        AssetDatabase.DeleteAsset(meshPath);
        var mesh = BuildMesh();
        AssetDatabase.CreateAsset(mesh, meshPath);

        var mat = GetOrCreate("Assets/Materials/Memory", "Dreamwalker", "Lucid/Dreamwalker");
        // Loaded at runtime by SmokeDash (Resources), so the shader is in the build.
        GetOrCreate("Assets/Resources/Combat", "SmokePuff", "Lucid/SmokePuff");

        var root = new GameObject("Lucid");
        var body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = body.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;   // nothing dark may follow the player
        r.receiveShadows = false;
        var lg = new GameObject("Glow");
        lg.transform.SetParent(body.transform, false);
        lg.transform.localPosition = new Vector3(0f, 1.05f, 0.35f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.range = 6f; l.intensity = 1.2f; l.color = new Color(1f, 0.8f, 0.65f);

        var av = root.AddComponent<MemoryAvatar>();
        av.ghosts = new Renderer[] { r };
        av.glow = l;
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Props/Lucid.prefab");
        UnityEngine.Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
    }
}
