using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Nightmare dressing: the same road, corrupted. Towering leaning silhouettes, glossy black tendrils, falling ash.
// Everything here is NightmareOnly. The wet black ground and pools reflect the eclipse sky.
public static class NightmareBuilder
{
    const string PrefabDir = "Assets/Prefabs/Props";
    const string MatDir = "Assets/Materials/Nightmare";
    const string MeshDir = "Assets/Meshes";

    static void Ensure(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }

    static Color H(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }

    static Material Lit(string name, string hex, float emission, float smooth)
    {
        string p = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_BaseColor", H(hex));
        m.SetFloat("_Smoothness", smooth);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            m.SetColor("_EmissionColor", H(hex) * emission);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // A curling tube that tapers to a point.
    static Mesh Tube(string name, int seed)
    {
        string path = MeshDir + "/" + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        Random.InitState(seed);
        int rings = 30, radial = 8;
        float height = Random.Range(5f, 9f);
        float bend = Random.Range(1.5f, 3.5f);
        float ph = Random.value * 6.28f, ph2 = Random.value * 6.28f;
        var centers = new Vector3[rings + 1];
        for (int i = 0; i <= rings; i++)
        {
            float t = i / (float)rings;
            centers[i] = new Vector3(Mathf.Sin(t * 3.1f + ph) * bend * t, t * height, Mathf.Cos(t * 2.3f + ph2) * bend * t);
        }
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i <= rings; i++)
        {
            float t = i / (float)rings;
            Vector3 next = centers[Mathf.Min(i + 1, rings)];
            Vector3 prev = centers[Mathf.Max(i - 1, 0)];
            Vector3 fwd = (next - prev).normalized;
            Vector3 right = Vector3.Cross(fwd, Mathf.Abs(fwd.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, fwd);
            float r = Mathf.Lerp(0.55f, 0.03f, Mathf.Pow(t, 0.8f)) * (1f + 0.18f * Mathf.Sin(t * 22f + ph));
            for (int j = 0; j < radial; j++)
            {
                float a = j / (float)radial * Mathf.PI * 2f;
                verts.Add(centers[i] + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * r);
            }
        }
        for (int i = 0; i < rings; i++)
            for (int j = 0; j < radial; j++)
            {
                int a = i * radial + j, b = i * radial + (j + 1) % radial;
                int c = (i + 1) * radial + j, d = (i + 1) * radial + (j + 1) % radial;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static void BuildTendrils()
    {
        var mat = Lit("Nightmare_Tendril", "#0A0508", 0f, 0.8f);
        var vein = Lit("Nightmare_Vein", "#FF2A10", 1.2f, 0.4f);
        for (int v = 0; v < 3; v++)
        {
            var root = new GameObject("Tendril" + v);
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = Tube("Tendril" + v, 30 + v * 7);
            body.AddComponent<MeshRenderer>().sharedMaterial = mat;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Tendril" + v + ".prefab");
            Object.DestroyImmediate(root);
        }
    }

    static void BuildTower()
    {
        var stone = Lit("Nightmare_Tower", "#0C0714", 0f, 0.3f);
        var window = Lit("Nightmare_Window", "#FF3A18", 2.2f, 0.2f);
        Random.InitState(77);
        var root = new GameObject("NightmareTower");
        for (int s = 0; s < 6; s++)
        {
            float w = Mathf.Lerp(7f, 3f, s / 5f);
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "Segment" + s;
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = new Vector3(s * 0.9f, s * 8f + 4f, 0);
            b.transform.localRotation = Quaternion.Euler(0, s * 7f, s * 1.5f);
            b.transform.localScale = new Vector3(w, 8f, w);
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.GetComponent<Renderer>().sharedMaterial = stone;
            for (int k = 0; k < 4; k++)
            {
                var win = GameObject.CreatePrimitive(PrimitiveType.Cube);
                win.name = "Window";
                win.transform.SetParent(b.transform, false);
                float y = Random.Range(-0.35f, 0.35f);
                win.transform.localPosition = new Vector3(Random.Range(-0.3f, 0.3f), y, -0.51f);
                win.transform.localScale = new Vector3(0.08f, 0.05f, 0.02f);
                Object.DestroyImmediate(win.GetComponent<Collider>());
                win.GetComponent<Renderer>().sharedMaterial = window;
            }
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/NightmareTower.prefab");
        Object.DestroyImmediate(root);
    }

    static void BuildAsh()
    {
        var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Combat/GlowParticle.mat");
        var go = new GameObject("NightmareAsh");
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.playOnAwake = true;
        main.startLifetime = 7f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(H("#FF4A1E"), H("#7A1A10"));
        main.gravityModifier = 0.08f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 900;
        var em = ps.emission; em.rateOverTime = 110f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(60, 16, 60);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.8f; noise.frequency = 0.3f; noise.scrollSpeed = 0.3f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = glow;
        PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/NightmareAsh.prefab");
        Object.DestroyImmediate(go);
    }

    public static void BuildPrefabs()
    {
        Ensure("Assets/Materials"); Ensure(MatDir); Ensure(MeshDir); Ensure("Assets/Prefabs"); Ensure(PrefabDir);
        BuildTendrils();
        BuildTower();
        BuildAsh();
        AssetDatabase.SaveAssets();
    }

    static GameObject Put(string prefab, Vector3 pos, Vector3 euler, Vector3 scale, Transform parent)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + prefab + ".prefab");
        var g = (GameObject)PrefabUtility.InstantiatePrefab(p);
        g.transform.position = pos;
        g.transform.eulerAngles = euler;
        g.transform.localScale = scale;
        g.transform.SetParent(parent, true);
        return g;
    }

    static void NightmareOnly(GameObject g)
    {
        var o = g.AddComponent<WorldObject>();
        o.visibleIn = WorldVisibility.NightmareOnly;
        o.affectColliders = false;
    }

    public static void Populate(Transform root, Transform player)
    {
        Random.InitState(99);
        // Giant leaning towers lining the road, far enough to read as silhouettes in the fog.
        for (int i = 0; i < 16; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float x = side * Random.Range(40f, 95f);
            float z = Random.Range(-10f, 135f);
            float s = Random.Range(1.2f, 2.2f);
            NightmareOnly(Put("NightmareTower", new Vector3(x, -2f, z), new Vector3(0, Random.Range(0f, 360f), 0), Vector3.one * s, root));
        }
        // Tendrils pushing up out of the ground beside the road.
        for (int i = 0; i < 90; i++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = side * (7f + Mathf.Pow(Random.value, 1.3f) * 35f);
            float z = Random.Range(-8f, 130f);
            float s = Random.Range(1.2f, 4.5f);
            NightmareOnly(Put("Tendril" + Random.Range(0, 3), new Vector3(x, -0.2f, z), new Vector3(0, Random.Range(0f, 360f), 0), Vector3.one * s, root));
        }
        // Ash falling around the player.
        NightmareOnly(Put("NightmareAsh", player.position + Vector3.up * 6f, Vector3.zero, Vector3.one, player));
    }
}
