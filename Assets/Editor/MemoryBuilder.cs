using UnityEditor;
using static BuildKit;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the memory figure mesh (camera-facing quads at points sampled from a human shape),
// its material, and the "Lucid" player visual prefab.
public static class MemoryBuilder
{
    const int Count = 3600;

    static int k;
    static Vector3[] centers;
    static float[] parts;

    static void Add(float cx, float cy, float cz, float rx, float ry, float rz, int n, float part = 1f)
    {
        for (int j = 0; j < n && k < Count; j++, k++)
        {
            float u = Random.value * 2f - 1f;
            float a = Random.value * Mathf.PI * 2f;
            float s = Mathf.Sqrt(1f - u * u);
            float r = Mathf.Pow(Random.value, 0.33f);
            centers[k] = new Vector3(cx + rx * r * s * Mathf.Cos(a), cy + ry * r * u, cz + rz * r * s * Mathf.Sin(a));
            parts[k] = part;
        }
    }

    static Mesh BuildMesh()
    {
        Random.InitState(7);
        centers = new Vector3[Count];
        parts = new float[Count];
        k = 0;
        Add(0, 1.72f, 0, 0.2f, 0.24f, 0.2f, 420, 0f);  // head
        Add(0, 1.15f, 0, 0.3f, 0.55f, 0.18f, 1300, 1f); // torso
        Add(-0.38f, 1.2f, 0, 0.09f, 0.5f, 0.09f, 330, 2f); // left arm
        Add(0.38f, 1.2f, 0, 0.09f, 0.5f, 0.09f, 330, 3f);  // right arm
        Add(-0.14f, 0.45f, 0, 0.11f, 0.5f, 0.11f, 400, 4f); // left leg
        Add(0.14f, 0.45f, 0, 0.11f, 0.5f, 0.11f, 400, 5f);  // right leg
        while (k < Count) Add(0, 1.0f, 0, 0.3f, 0.9f, 0.2f, 1);

        var verts = new Vector3[Count * 4];
        var uv0 = new Vector2[Count * 4];
        var uv1 = new Vector2[Count * 4];
        var uv2 = new Vector2[Count * 4];
        var tris = new int[Count * 6];
        Vector2[] corners = { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) };
        for (int i = 0; i < Count; i++)
        {
            float seed = Random.value;
            float thresh = Random.value * 0.6f + (centers[i].y / 1.96f) * 0.4f;
            for (int c = 0; c < 4; c++)
            {
                verts[i * 4 + c] = centers[i];
                uv0[i * 4 + c] = corners[c];
                uv1[i * 4 + c] = new Vector2(thresh, seed);
                uv2[i * 4 + c] = new Vector2(parts[i], 0f);
            }
            int b = i * 4, t = i * 6;
            tris[t] = b; tris[t + 1] = b + 2; tris[t + 2] = b + 1;
            tris[t + 3] = b; tris[t + 4] = b + 3; tris[t + 5] = b + 2;
        }
        var mesh = new Mesh { name = "MemoryFigure", vertices = verts, triangles = tris };
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetUVs(2, uv2);
        mesh.bounds = new Bounds(new Vector3(0, 1f, 0), new Vector3(4, 4, 4)); // room for drifting points
        return mesh;
    }

    public static void Build()
    {
        Ensure("Assets/Meshes"); Ensure("Assets/Materials"); Ensure("Assets/Materials/Memory"); Ensure("Assets/Prefabs"); Ensure("Assets/Prefabs/Props");

        string meshPath = "Assets/Meshes/MemoryFigure.asset";
        AssetDatabase.DeleteAsset(meshPath);
        var mesh = BuildMesh();
        AssetDatabase.CreateAsset(mesh, meshPath);

        string matPath = "Assets/Materials/Memory/MemoryFigure.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Lucid/MemoryFigure"));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        var root = new GameObject("Lucid");
        // One figure only: the old delayed afterimages read as a dark trail (MemoryAvatar also strips them at runtime).
        var ghosts = new Renderer[1];
        for (int i = 0; i < 1; i++)
        {
            var g = new GameObject("Ghost" + i);
            g.transform.SetParent(root.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ghosts[i] = r;
        }
        var lg = new GameObject("Glow");
        lg.transform.SetParent(root.transform, false);
        lg.transform.localPosition = new Vector3(0, 1.3f, 0.2f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.range = 9f; l.intensity = 1.5f; l.color = new Color(1f, 0.8f, 0.65f);

        var av = root.AddComponent<MemoryAvatar>();
        av.ghosts = ghosts;
        av.glow = l;
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Props/Lucid.prefab");
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
    }
}
