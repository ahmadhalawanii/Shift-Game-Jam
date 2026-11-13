using UnityEditor;
using static BuildKit;
using UnityEngine;
using UnityEngine.Rendering;

// Turns the Poly Haven models and textures into URP prefabs and materials, and scatters the Level 1 meadow.
public static class NatureBuilder
{
    const string PH = "Assets/ThirdParty/PolyHaven";
    const string Out = "Assets/Prefabs/Nature";
    const string MatOut = "Assets/Materials/Nature";

        static readonly string[] Solids = { "boulder_01", "rock_face_01", "dead_tree_trunk" };
    static readonly string[] Flowers = { "celandine_01", "dandelion_01", "flower_gazania", "flower_ursinia", "flower_heliophila" };

    static Texture2D Tex(string path) { return AssetDatabase.LoadAssetAtPath<Texture2D>(path); }

    static void Import(string path, bool normal, bool alpha)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        bool dirty = false;
        if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
        if (alpha && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
        if (dirty) ti.SaveAndReimport();
    }

    static Material Lit(string name)
    {
        string p = MatOut + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, p);
        }
        return m;
    }

    static void SetNormal(Material m, Texture2D n)
    {
        if (n == null) return;
        m.SetTexture("_BumpMap", n);
        m.EnableKeyword("_NORMALMAP");
    }

    public static void BuildAll()
    {
        Ensure("Assets/Prefabs"); Ensure(Out); Ensure("Assets/Materials"); Ensure(MatOut);
        BuildMaterials();
        foreach (var n in Flowers) MakePrefab(n, true);
        MakePrefab("grass_medium_01", true);
        foreach (var n in Solids) MakePrefab(n, false);
        AssetDatabase.SaveAssets();
    }

    static void BuildMaterials()
    {
        string[,] tex = { { "Nature_GrassGround", "grass_ground", "60" }, { "Nature_BurnedGround", "burned_ground_01", "60" }, { "Nature_Asphalt", "asphalt_02", "6" }, { "Nature_AsphaltWet", "asphalt_02", "6" } };
        for (int i = 0; i < 4; i++)
        {
            string name = tex[i, 0], t = tex[i, 1];
            string d = PH + "/Textures/" + t + "/" + t + "_diff_1k.jpg";
            string n = PH + "/Textures/" + t + "/" + t + "_nor_gl_1k.jpg";
            Import(n, true, false);
            var m = Lit(name);
            m.SetTexture("_BaseMap", Tex(d));
            SetNormal(m, Tex(n));
            float tile = float.Parse(tex[i, 2]);
            m.SetTextureScale("_BaseMap", new Vector2(tile, tile));
            m.SetTextureScale("_BumpMap", new Vector2(tile, tile));
            m.SetFloat("_Smoothness", name == "Nature_AsphaltWet" ? 0.93f : (t == "asphalt_02" ? 0.45f : 0.12f));
            if (name == "Nature_AsphaltWet") m.SetColor("_BaseColor", new Color(0.3f, 0.26f, 0.3f));
            if (name == "Nature_GrassGround") m.SetColor("_BaseColor", new Color(0.82f, 0.62f, 1.0f));
            if (name == "Nature_BurnedGround") { m.SetColor("_BaseColor", new Color(0.3f, 0.2f, 0.22f)); m.SetFloat("_Smoothness", 0.88f); }
            EditorUtility.SetDirty(m);
        }
    }

    static void MakePrefab(string name, bool cutout)
    {
        string fbx = PH + "/Models/" + name + "/" + name + "_1k.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (model == null) { Debug.LogWarning("Missing model " + fbx); return; }
        string td = PH + "/Models/" + name + "/textures/";
        string diff = cutout ? td + name + "_diffa_1k.png" : td + name + "_diff_1k.jpg";
        string nor = td + name + "_nor_gl_1k.exr";
        Import(diff, false, cutout);
        Import(nor, true, false);
        var m = Lit("Nature_" + name);
        m.SetTexture("_BaseMap", Tex(diff));
        SetNormal(m, Tex(nor));
        m.SetFloat("_Smoothness", 0.2f);
        m.SetFloat("_Metallic", 0f);
        m.SetColor("_BaseColor", new Color(1.0f, 0.82f, 1.0f));
        m.enableInstancing = true;
        if (cutout)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Cull", 0f);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
        }
        EditorUtility.SetDirty(m);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
        PrefabUtility.SaveAsPrefabAsset(go, Out + "/" + name + ".prefab");
        Object.DestroyImmediate(go);
    }

    static GameObject Spawn(string prefab, Transform parent, Vector3 pos, float scale)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(Out + "/" + prefab + ".prefab");
        if (p == null) return null;
        var g = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        g.transform.localScale = Vector3.one * scale;
        return g;
    }

    static GameObject Group(string name, WorldVisibility w)
    {
        var g = new GameObject(name);
        var o = g.AddComponent<WorldObject>();
        o.visibleIn = w;
        return g;
    }

    // Scatters Level 1 nature. Road runs along Z at x = 0 (half width 4).
    public static void Populate()
    {
        Random.InitState(1337);
        var meadow = Group("Meadow_DreamOnly", WorldVisibility.DreamOnly);
        var wood = Group("DeadWood_NightmareOnly", WorldVisibility.NightmareOnly);
        var rocks = new GameObject("Rocks");

        var centers = new Vector3[16];
        for (int i = 0; i < centers.Length; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            centers[i] = new Vector3(side * Random.Range(8f, 40f), 0, Random.Range(-8f, 120f));
        }

        for (int i = 0; i < 900; i++)
        {
            Vector3 p;
            if (Random.value < 0.7f)
            {
                var c = centers[Random.Range(0, centers.Length)];
                p = c + new Vector3(Random.insideUnitCircle.x, 0, Random.insideUnitCircle.y) * 7f;
            }
            else
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                p = new Vector3(side * (5.5f + Mathf.Pow(Random.value, 1.6f) * 70f), 0, Random.Range(-10f, 130f));
            }
            if (Mathf.Abs(p.x) < 6.5f) continue;
            Spawn(Flowers[Random.Range(0, Flowers.Length)], meadow.transform, p, Random.Range(6f, 10f));
        }
        for (int i = 0; i < 380; i++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            var p = new Vector3(side * (8.5f + Mathf.Pow(Random.value, 1.4f) * 55f), 0, Random.Range(-10f, 120f));
            Spawn("grass_medium_01", meadow.transform, p, Random.Range(4f, 7f));
        }
        for (int i = 0; i < 14; i++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            Spawn("boulder_01", rocks.transform, new Vector3(side * Random.Range(12f, 60f), 0, Random.Range(-5f, 120f)), Random.Range(2f, 5f));
        }
        for (int i = 0; i < 6; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Spawn("rock_face_01", rocks.transform, new Vector3(side * Random.Range(60f, 95f), -2f, 50f + i * 20f), Random.Range(14f, 26f));
        }
        for (int i = 0; i < 10; i++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            Spawn("dead_tree_trunk", wood.transform, new Vector3(side * Random.Range(9f, 40f), 0, Random.Range(0f, 110f)), Random.Range(2.5f, 4f));
        }
    }
}
