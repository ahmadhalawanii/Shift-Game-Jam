using System.Collections.Generic;
using UnityEditor;
using static BuildKit;
using UnityEngine;
using UnityEngine.Rendering;

// Dream world dressing, inspired by wisteria tunnels, mirror salt flats, light shafts and glowing painterly forests.
// Everything here is DreamOnly or swaps material, so the Nightmare version can replace it.
public static class DreamBuilder
{
    const string PrefabDir = "Assets/Prefabs/Props";
    const string MatDir = "Assets/Materials/Dream";
    const string MeshDir = "Assets/Meshes";

    static Material Lit(string name, string hex, float emission, float smooth) { return BuildKit.Lit(MatDir, name, hex, emission, smooth); }

    static Material Custom(string name, string shader) { return GetOrCreate(MatDir, name, shader); }

    // Height of the wisteria arch at position u (0..1) across the road.
    static float ArchY(float u) { return 3.2f + 4.2f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)), 0.7f); }

    // ---- procedural wisteria curtain: hanging strands of small blossoms across an arch ----

    static void AddBlob(Vector3 c, float r, List<Vector3> verts, List<int> tris)
    {
        int seg = 6;
        int baseIndex = verts.Count;
        verts.Add(c + new Vector3(0, r * 1.3f, 0));
        float[] ys = { 0.5f, 0f, -0.5f };
        float[] rs = { 0.85f, 1f, 0.85f };
        for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f + ring * 0.5f;
                verts.Add(c + new Vector3(Mathf.Cos(a) * r * rs[ring], ys[ring] * r * 1.3f, Mathf.Sin(a) * r * rs[ring]));
            }
        verts.Add(c + new Vector3(0, -r * 1.3f, 0));
        int bottom = verts.Count - 1;
        for (int i = 0; i < seg; i++)
        {
            int n = (i + 1) % seg;
            tris.Add(baseIndex); tris.Add(baseIndex + 1 + n); tris.Add(baseIndex + 1 + i);
            for (int ring = 0; ring < 2; ring++)
            {
                int a0 = baseIndex + 1 + ring * seg + i, a1 = baseIndex + 1 + ring * seg + n;
                int b0 = baseIndex + 1 + (ring + 1) * seg + i, b1 = baseIndex + 1 + (ring + 1) * seg + n;
                tris.Add(a0); tris.Add(a1); tris.Add(b0);
                tris.Add(a1); tris.Add(b1); tris.Add(b0);
            }
            tris.Add(bottom); tris.Add(baseIndex + 1 + 2 * seg + i); tris.Add(baseIndex + 1 + 2 * seg + n);
        }
    }

    static Mesh WisteriaMesh()
    {
        string path = MeshDir + "/WisteriaCurtain.asset";
        AssetDatabase.DeleteAsset(path);
        Random.InitState(21);
        var verts = new List<Vector3>();
        var trisA = new List<int>();
        var trisB = new List<int>();
        for (int s = 0; s < 230; s++)
        {
            float u = Random.value;
            float x = Mathf.Lerp(-7.5f, 7.5f, u);
            float top = ArchY(u);
            float z = Random.Range(-3.2f, 3.2f);
            float len = Random.Range(2.2f, 5.4f);
            int blobs = Mathf.RoundToInt(len / 0.19f);
            var list = (s % 2 == 0) ? trisA : trisB;
            for (int j = 0; j < blobs; j++)
            {
                float f = j / (float)blobs;
                float r = Mathf.Lerp(0.15f, 0.05f, f) * Random.Range(0.75f, 1.25f);
                var c = new Vector3(x + Mathf.Sin(j * 0.9f + s) * 0.12f, top - j * 0.19f, z + Mathf.Cos(j * 0.7f + s) * 0.12f);
                AddBlob(c, r, verts, list);
            }
        }
        var mesh = new Mesh { name = "WisteriaCurtain", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(trisA, 0);
        mesh.SetTriangles(trisB, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static void BuildWisteriaArch()
    {
        var violet = Lit("Wisteria_Violet", "#8B5CF0", 0.35f, 0.3f);
        var lilac = Lit("Wisteria_Lilac", "#C79BFF", 0.45f, 0.3f);
        var wood = Lit("Wisteria_Wood", "#3A2540", 0f, 0.2f);
        var root = new GameObject("WisteriaArch");
        var curtain = new GameObject("Curtain");
        curtain.transform.SetParent(root.transform, false);
        curtain.AddComponent<MeshFilter>().sharedMesh = WisteriaMesh();
        var mr = curtain.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { violet, lilac };
        mr.shadowCastingMode = ShadowCastingMode.Off;

        // Posts and arching branch.
        foreach (float side in new[] { -1f, 1f })
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(side * 7.6f, 1.8f, 0);
            post.transform.localScale = new Vector3(0.35f, 1.8f, 0.35f);
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.GetComponent<Renderer>().sharedMaterial = wood;
        }
        for (int i = 0; i < 16; i++)
        {
            float u0 = i / 16f, u1 = (i + 1) / 16f;
            Vector3 a = new Vector3(Mathf.Lerp(-7.5f, 7.5f, u0), ArchY(u0), 0);
            Vector3 b = new Vector3(Mathf.Lerp(-7.5f, 7.5f, u1), ArchY(u1), 0);
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = "Branch";
            seg.transform.SetParent(root.transform, false);
            seg.transform.localPosition = (a + b) / 2f;
            seg.transform.localRotation = Quaternion.FromToRotation(Vector3.right, b - a);
            seg.transform.localScale = new Vector3((b - a).magnitude * 1.05f, 0.32f, 0.32f);
            Object.DestroyImmediate(seg.GetComponent<Collider>());
            seg.GetComponent<Renderer>().sharedMaterial = wood;
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/WisteriaArch.prefab");
        Object.DestroyImmediate(root);
    }

    static void BuildLightShaft()
    {
        var m = Custom("LightShaft", "Lucid/LightShaft");
        m.SetColor("_Color", H("#FFD6E8"));
        m.SetFloat("_Intensity", 0.42f);
        EditorUtility.SetDirty(m);
        var root = new GameObject("LightShaft");
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Beam";
        q.transform.SetParent(root.transform, false);
        q.transform.localScale = new Vector3(5f, 26f, 1f);
        q.transform.localPosition = new Vector3(0, -13f, 0);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.GetComponent<Renderer>().sharedMaterial = m;
        q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/LightShaft.prefab");
        Object.DestroyImmediate(root);
    }

    static void BuildPool()
    {
        var dream = Lit("Pool_Dream", "#3A2A70", 0f, 0.98f);
        var night = Lit("Pool_Night", "#070204", 0.25f, 0.98f);
        night.SetColor("_EmissionColor", H("#7A1208") * 0.25f);
        var root = new GameObject("MirrorPool");
        var p = GameObject.CreatePrimitive(PrimitiveType.Plane);
        p.name = "Water";
        p.transform.SetParent(root.transform, false);
        p.transform.localPosition = new Vector3(0, 0.04f, 0);
        p.transform.localScale = new Vector3(1.4f, 1f, 2.2f);
        Object.DestroyImmediate(p.GetComponent<Collider>());
        p.GetComponent<Renderer>().sharedMaterial = dream;
        p.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var sw = p.AddComponent<WorldMaterialSwap>(); sw.dream = dream; sw.nightmare = night;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/MirrorPool.prefab");
        Object.DestroyImmediate(root);
    }

    static GameObject Particles(string name, Color a, Color b, float rate, float life, float speed, float size, float gravity, Vector3 box)
    {
        var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Combat/GlowParticle.mat");
        var go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.playOnAwake = true;
        main.startLifetime = life;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 700;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = glow;
        return go;
    }

    static void BuildAtmosphere()
    {
        var petals = Particles("DreamPetals", H("#FFB6DA"), H("#C79BFF"), 45f, 9f, 0.6f, 0.22f, -0.01f, new Vector3(60, 14, 60));
        PrefabUtility.SaveAsPrefabAsset(petals, PrefabDir + "/DreamPetals.prefab");
        Object.DestroyImmediate(petals);
    }

    public static void BuildPrefabs()
    {
        Ensure("Assets/Materials"); Ensure(MatDir); Ensure(MeshDir); Ensure("Assets/Prefabs"); Ensure(PrefabDir);
        BuildLightShaft();
        BuildPool();
        BuildAtmosphere();
        AssetDatabase.SaveAssets();
    }

    static GameObject Put(string prefab, Vector3 pos, Vector3 euler, Transform parent)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + prefab + ".prefab");
        var g = (GameObject)PrefabUtility.InstantiatePrefab(p);
        g.transform.position = pos;
        g.transform.eulerAngles = euler;
        g.transform.SetParent(parent, true);
        return g;
    }

    static void DreamOnly(GameObject g)
    {
        var o = g.AddComponent<WorldObject>();
        o.visibleIn = WorldVisibility.DreamOnly;
    }

    // Places the Dream dressing along the road (+Z).
    public static void Populate(Transform root, Transform player)
    {
        Random.InitState(5);
        BuildMoonGarden(root);

        // Light shafts slanting through the haze.
        for (int i = 0; i < 9; i++)
        {
            float z = 8f + i * 11f;
            float x = Random.Range(-14f, 14f);
            DreamOnly(Put("LightShaft", new Vector3(x, 22f, z), new Vector3(0, Random.Range(-20f, 20f), Random.Range(-14f, 14f)), root));
        }

        // Mirror pools beside the road (they turn to black glass in Nightmare).
        float[] pz = { 20f, 46f, 72f, 98f };
        for (int i = 0; i < pz.Length; i++)
            Put("MirrorPool", new Vector3((i % 2 == 0 ? -1f : 1f) * 17f, 0, pz[i]), new Vector3(0, Random.Range(0f, 40f), 0), root);

        // Drifting petals around the player.
        var petals = Put("DreamPetals", player.position + Vector3.up * 5f, Vector3.zero, player);
        DreamOnly(petals);
    }

    static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = position; g.transform.localScale = scale;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().sharedMaterial = material;
        return g;
    }

    static void BuildMoonGarden(Transform parent)
    {
        var garden = new GameObject("Moon Garden - Dream");
        garden.transform.SetParent(parent, false);
        var bark = Lit("MoonTree_Ivory", "#E3D4BE", 0f, 0.35f);
        var jade = Lit("MoonTree_Jade", "#69BFA9", 0f, 0.3f);
        var mint = Lit("MoonTree_Mint", "#B4DBC6", 0f, 0.3f);
        var stone = Lit("Garden_WhiteStone", "#DCE3DB", 0f, 0.5f);
        var glow = Lit("Garden_Starlight", "#FFE3A4", 0.7f, 0.5f);
        for (int i = 0; i < 16; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector3 p = new Vector3(side * (19f + i % 3 * 5f), 0, i * 8f - 5f);
            var tree = new GameObject("Ivory umbrella tree"); tree.transform.SetParent(garden.transform, false); tree.transform.localPosition = p;
            Shape(PrimitiveType.Cylinder, "Trunk", tree.transform, new Vector3(0, 3.5f, 0), new Vector3(0.7f, 3.5f, 0.7f), bark);
            for (int b = 0; b < 5; b++)
            {
                float angle = b * Mathf.PI * 0.4f + i;
                Vector3 end = new Vector3(Mathf.Cos(angle) * 3.5f, 7f + b * 0.35f, Mathf.Sin(angle) * 3.5f);
                Vector3 start = new Vector3(0, 4f, 0);
                var branch = Shape(PrimitiveType.Cylinder, "Sweeping branch", tree.transform, (start + end) * 0.5f, new Vector3(0.24f, (end-start).magnitude * 0.5f, 0.24f), bark);
                branch.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
                Shape(PrimitiveType.Sphere, "Broad jade canopy", tree.transform, end, new Vector3(6.4f, 1.5f, 5.4f), b % 2 == 0 ? jade : mint);
            }
            if (i % 2 == 0)
            {
                Vector3 island = p + new Vector3(side * 13f, 16f + i % 3 * 3f, 6f);
                Shape(PrimitiveType.Sphere, "Floating garden island", garden.transform, island, new Vector3(10f, 3f, 7f), stone);
                Shape(PrimitiveType.Sphere, "Floating moss crown", garden.transform, island + Vector3.up * 1.1f, new Vector3(10.2f, 1f, 7.2f), jade);
            }
        }
        // A distant, open circular monument gives the journey a destination and silhouette.
        for (int i = 0; i < 40; i++)
        {
            float angle = i * Mathf.PI * 2f / 40f;
            var block = Shape(PrimitiveType.Cube, "Broken moon halo", garden.transform,
                new Vector3(Mathf.Cos(angle) * 16f, 22f + Mathf.Sin(angle) * 16f, 132f), new Vector3(2.3f, 0.8f, 1.3f), i % 5 == 0 ? glow : stone);
            block.transform.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg + 90f);
        }
        DreamOnly(garden);
    }
}
