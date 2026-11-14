using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static BuildKit;
using static WorldMeshes;
using Object = UnityEngine.Object;

// The asset kit of the redesigned world: materials, generated meshes and prefabs.
// Everything is rebuilt by menu Lucid > Build Lucid World (WorldLevelBuilder), so do not hand-edit the results.
// Mesh vertex colour: rgb tints the material palette, alpha is the emission mask.
public static partial class WorldKit
{
    public const string Root = "Assets/Lucid/World";
    public const string MatDir = Root + "/Materials";
    public const string MeshDir = Root + "/Meshes";
    public const string PrefabDir = Root + "/Prefabs";
    public const string SettingsDir = Root + "/Settings";

    const string EnvShader = "Lucid/World/Environment";
    const string FoliageShader = "Lucid/World/Foliage";
    const string GlowShader = "Lucid/World/Glow";
    const string FogShader = "Lucid/World/FogCard";

    // Palette from the brief.
    public static readonly Color Cyan = H("#5FE3F0"), Gold = H("#FFD98A"), Ember = H("#FF9A4D"), Pink = H("#E88CC8"),
        Magenta = H("#C45AA5"), Light = H("#BFE6FF"), Moon = H("#7FB6D9"), Corrupt = H("#4A1F6B"), Violet = H("#9A4FE0"),
        Hazard = H("#FF5A36"), HazardHot = H("#FFB347");

    // ---------- Materials ----------

    public static Material Ground, Rock, Bark, BarkFar, Silhouette, Thorn, ThornSolid, Petal, BouncePetal, Grass, Flowers,
        Canopy, CanopyFar, SpiritCanopy, Mushroom, Bone, ArenaFloor, PanelBoth, PanelDream, PanelNight, WatcherStone, Crawler,
        MothWing, Bud, Lurker;
    public static Material GlowCyan, LanternGlow, Halo, HaloCyan, Strand, StrandBig, TreeHalo, TreeHeart, Shaft, Mote, Spark,
        Shimmer, WellWater, EyeOrange, EyeViolet, MothBody, GlowLine, PlayerDisc;
    public static Material Mist, MistSea, MistWall, Seam, Sky;

    static Material Fresh(string name, string shader)
    {
        var m = GetOrCreate(MatDir, name, shader);
        var s = Shader.Find(shader);
        if (s == null) throw new InvalidOperationException("Shader not found: " + shader);
        // Back to the shader defaults, so a property dropped from the builder does not linger from an old build.
        var defaults = new Material(s);
        m.shader = s;
        m.CopyPropertiesFromMaterial(defaults);
        Object.DestroyImmediate(defaults);
        m.enableInstancing = false;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material F(this Material m, string property, float value) { m.SetFloat(property, value); return m; }
    static Material C(this Material m, string property, Color value) { m.SetColor(property, value); return m; }

    // A surface material with one palette per world: lit, shade and accent for each.
    static Material Surface(string name, string dLit, string dShade, string dAccent, string nLit, string nShade, string nAccent, bool foliage = false)
    {
        return Fresh(name, foliage ? FoliageShader : EnvShader)
            .C("_DreamColor", H(dLit)).C("_DreamShade", H(dShade)).C("_DreamAccent", H(dAccent))
            .C("_NightColor", H(nLit)).C("_NightShade", H(nShade)).C("_NightAccent", H(nAccent))
            .C("_DreamEmission", Color.black).C("_NightEmission", Color.black)
            .C("_DreamRim", Pink * 0.5f).C("_NightRim", Moon * 0.4f)
            .C("_CorruptionColor", Violet * 1.25f);
    }

    static Material Emit(this Material m, Color dream, Color night, float mask = 1f, float pulse = 0f, float playerGlow = 0f)
    {
        return m.C("_DreamEmission", dream).C("_NightEmission", night).F("_EmissionMask", mask).F("_Pulse", pulse).F("_PlayerGlow", playerGlow);
    }

    static Material Glow(string name, Color dream, Color night, int shape = 0, int billboard = 0)
    {
        return Fresh(name, GlowShader).C("_DreamColor", dream).C("_NightColor", night).F("_Shape", shape).F("_Billboard", billboard);
    }

    public static void BuildMaterials()
    {
        Ground = Surface("Ground", "#5848BC", "#1E1858", "#3FB5B0", "#24405F", "#070E1E", "#1A2C48")
            .Emit(Cyan * 1.3f, Moon * 0.7f, 1f, 0.15f, 1.5f).F("_MaskPower", 3f).F("_Corruption", 0.4f).F("_VariationScale", 0.22f).F("_VariationAmount", 0.6f).F("_SeamPush", 0.1f);
        Rock = Surface("Rock", "#5A4B9A", "#1F1650", "#8A5AB5", "#1B2F4A", "#050A16", "#2B4C73")
            .F("_Corruption", 0.8f).F("_VariationScale", 0.3f).F("_VariationAmount", 0.55f).F("_SeamPush", 0.08f);
        Bark = Surface("Bark", "#6A4690", "#261544", "#B5628F", "#14233A", "#04070F", "#2A1F4A")
            .Emit(Cyan * 2.4f, Violet * 1.8f, 1f, 0.3f, 2f).F("_EmissionVeins", 1f).F("_Corruption", 0.6f).F("_VariationScale", 0.5f).F("_SeamPush", 0.1f);
        BarkFar = Surface("BarkFar", "#5A3F86", "#22143F", "#9A5690", "#0F1C30", "#03060D", "#241A40")
            .Emit(Light * 1.8f, Violet * 1.2f, 1f, 0.35f).F("_MaskPower", 2f).F("_Corruption", 1f).F("_VariationScale", 0.05f).F("_FogInfluence", 0.55f).F("_SeamPush", 0f);
        Silhouette = Surface("Silhouette", "#3A2D7A", "#1F1650", "#4A3A90", "#0A1426", "#03060E", "#0F1E36")
            .F("_VariationScale", 0.08f).F("_VariationAmount", 0.3f).F("_SeamPush", 0f).C("_DreamRim", Pink * 0.2f).C("_NightRim", Moon * 0.15f);
        Thorn = Surface("Thorn", "#5A4B9A", "#1F1650", "#8A5AB5", "#2A1F4A", "#07050F", "#4A1F6B")
            .Emit(Color.black, Hazard * 1.8f, 1f, 0.3f).F("_MaskPower", 6f).F("_ExistIn", 2f).F("_GrowDream", 0.15f).F("_GrowNight", 1f).F("_GrowWidth", 0.7f).F("_MorphSeconds", 0.5f);
        ThornSolid = Surface("ThornSolid", "#5A3F86", "#23153F", "#C45AA5", "#2A1F4A", "#07050F", "#4A1F6B")
            .Emit(Pink * 0.7f, Hazard * 1.8f, 1f, 0.3f).F("_MaskPower", 6f).F("_LeanToPlayer", 0.25f).F("_WindHeight", 4f);
        Petal = Surface("Petal", "#F29AD2", "#8A3F9A", "#FFD98A", "#2E2350", "#0A0716", "#4A1F6B", true)
            .Emit(Gold * 1.2f, Violet * 0.6f, 1f, 0.2f, 1.5f).F("_FoldDream", 0f).F("_FoldNight", 1.3f).F("_FoldHinge", 0.4f).F("_MorphSeconds", 0.5f)
            .F("_Translucency", 0.6f).C("_DreamRim", H("#FFE0F0") * 0.5f).F("_VariationAmount", 0.35f).F("_SeamPush", 0.2f);
        BouncePetal = Surface("BouncePetal", "#FFC27A", "#B55A3A", "#FFE9A8", "#33264F", "#0C0818", "#4A1F6B", true)
            .Emit(Gold * 1.7f, Violet * 0.5f, 1f, 0.35f, 1.5f).F("_FoldDream", 0f).F("_FoldNight", 1.1f).F("_FoldHinge", 0.4f).F("_MorphSeconds", 0.4f)
            .F("_Translucency", 0.7f).F("_VariationAmount", 0.25f);
        Grass = Surface("Grass", "#58D0C0", "#1F5F86", "#9AE8D0", "#1A3552", "#050C18", "#24405F", true)
            .Emit(Cyan * 0.9f, Color.black, 1f, 0f, 2f).F("_GrowDream", 1f).F("_GrowNight", 0.45f).F("_Wind", 0.22f).F("_WindHeight", 0.8f)
            .F("_PlayerBend", 0.6f).F("_SeamPush", 0.25f).F("_Translucency", 0.5f).F("_VariationScale", 0.15f);
        Flowers = Surface("Flowers", "#FFD98A", "#C45AA5", "#E88CC8", "#1F1A3A", "#06050F", "#4A1F6B", true)
            .Emit(Gold * 1.1f, Color.black, 1f, 0.2f, 2f).F("_GrowDream", 1f).F("_GrowNight", 0.3f).F("_Wind", 0.18f).F("_WindHeight", 0.8f)
            .F("_PlayerBend", 0.5f).F("_Translucency", 0.6f);
        Canopy = Surface("Canopy", "#8A5AD0", "#3A2580", "#E88CC8", "#16223A", "#05080F", "#2A1F4A", true)
            .Emit(Magenta * 0.3f, Color.black, 0f).F("_ExistIn", 1f).F("_Wind", 0.5f).F("_WindHeight", 6f).F("_Translucency", 0.8f).F("_VariationScale", 0.4f);
        CanopyFar = Surface("CanopyFar", "#5A45A8", "#2B2068", "#7A55B8", "#0C1830", "#040812", "#12233F", true)
            .F("_Wind", 0.4f).F("_WindHeight", 12f).F("_VariationScale", 0.1f);
        SpiritCanopy = Surface("SpiritCanopy", "#C8B0FF", "#6A4FC0", "#BFE6FF", "#16223A", "#05080F", "#2A1F4A", true)
            .Emit(Light * 0.45f, Color.black, 0f).F("_ExistIn", 1f).F("_Wind", 1.2f).F("_WindHeight", 40f).F("_Translucency", 1f)
            .F("_VariationScale", 0.06f).F("_FogInfluence", 0.5f);
        Mushroom = Surface("Mushroom", "#6FD8E8", "#25508A", "#BFE6FF", "#1A2C48", "#050A16", "#2A1F4A")
            .Emit(Cyan * 1.8f, Violet * 0.9f, 1f, 0.3f, 2.5f).F("_VariationAmount", 0.3f);
        Bone = Surface("Bone", "#A8C8DE", "#1B3250", "#7FA6C8", "#A8C8DE", "#1B3250", "#7FA6C8")
            .F("_ExistIn", 2f).F("_FogInfluence", 0.9f).F("_VariationScale", 0.05f).F("_VariationAmount", 0.4f).F("_SeamPush", 0f).C("_NightRim", Moon * 0.6f);
        Lurker = Surface("Lurker", "#0A1426", "#03060E", "#0F1E36", "#081120", "#02040A", "#0C1930")
            .F("_ExistIn", 2f).F("_VariationAmount", 0.2f).F("_SeamPush", 0f).C("_NightRim", Moon * 0.25f);
        ArenaFloor = Surface("ArenaFloor", "#3A3460", "#1C1838", "#4A4478", "#131F33", "#070C16", "#1B2B45")
            .F("_VariationScale", 0.12f).F("_VariationAmount", 0.35f).F("_SeamPush", 0f).F("_Corruption", 0.35f)
            .C("_DreamRim", Color.black).C("_NightRim", Color.black);
        foreach (int exist in new[] { 0, 1, 2 })
        {
            var p = Surface(exist == 0 ? "PanelBoth" : exist == 1 ? "PanelDream" : "PanelNight", "#6A5AAE", "#2B2068", "#8A5AB5", "#1F3350", "#060B18", "#2B4C73")
                .Emit(Cyan * 2.4f, Light * 2f, 1f, 0.35f, 1f).F("_ExistIn", exist).F("_VariationScale", 0.4f);
            if (exist == 0) PanelBoth = p; else if (exist == 1) PanelDream = p; else PanelNight = p;
        }
        WatcherStone = Surface("WatcherStone", "#8A7AC0", "#3A2D7A", "#58B0B0", "#1F3350", "#060B18", "#2B4C73")
            .Emit(Color.black, Hazard * 1.6f, 1f, 0.3f);
        Crawler = Surface("Crawler", "#6A5AAE", "#2B2068", "#58D0C0", "#241A3E", "#07050F", "#4A1F6B")
            .Emit(Color.black, Hazard * 2.2f, 1f, 0.4f);
        MothWing = Surface("MothWing", "#BFE6FF", "#6A8FD0", "#FFD98A", "#4A1826", "#14060C", "#7A2A1A", true)
            .Emit(Light * 0.7f, Hazard * 0.9f, 0f).F("_Translucency", 1f).F("_SeamPush", 0f).F("_FogInfluence", 0.5f);
        Bud = Surface("Bud", "#7A5AA8", "#33205F", "#C45AA5", "#3A1F4A", "#0F0716", "#6A2A3A", true)
            .Emit(Color.black, Hazard * 2.6f, 1f, 0.5f).F("_FoldDream", 0.75f).F("_FoldNight", 0.1f).F("_FoldHinge", 0.25f).F("_MorphSeconds", 0.5f);

        GlowCyan = Glow("GlowCyan", Cyan * 2f, Violet * 0.8f).F("_PlayerGlow", 1.5f);
        LanternGlow = Glow("LanternGlow", Gold * 3f, Moon * 1.2f).F("_Flicker", 0.5f).F("_Pulse", 0.2f).F("_Fresnel", 0.8f);
        Halo = Glow("Halo", Gold * 0.5f, Moon * 0.25f, 1, 1).F("_DepthFade", 1.5f).F("_Flicker", 0.4f);
        HaloCyan = Glow("HaloCyan", Cyan * 0.45f, Violet * 0.3f, 1, 1).F("_DepthFade", 1.5f).F("_PlayerGlow", 1f);
        Strand = Glow("Strand", H("#9FEFF8") * 1.2f, Violet * 0.4f, 2, 0).F("_GrowNight", 0.2f).F("_Wind", 0.6f).F("_WindHeight", 5f).F("_PlayerGlow", 1.5f);
        StrandBig = Glow("StrandBig", Light * 1.4f, Corrupt * 0.8f, 2, 0).F("_GrowNight", 0.3f).F("_Wind", 1.5f).F("_WindHeight", 30f).F("_FogInfluence", 0.25f).F("_MorphSeconds", 1.2f);
        TreeHalo = Glow("TreeHalo", new Color(0.75f, 0.9f, 1f) * 0.55f, new Color(0.3f, 0.45f, 0.7f) * 0.2f, 1, 1).F("_FogInfluence", 0.15f).F("_Pulse", 0.08f).F("_PulseSpeed", 0.5f);
        TreeHeart = Glow("TreeHeart", Gold * 4f, Violet * 2f).F("_FogInfluence", 0.2f).F("_Pulse", 0.4f).F("_Fresnel", 0.6f);
        Shaft = Glow("Shaft", new Color(1f, 0.9f, 0.75f) * 0.3f, new Color(0.5f, 0.7f, 0.9f) * 0.16f, 2, 0).F("_FogInfluence", 0.3f).F("_DepthFade", 2f).F("_Pulse", 0.15f).F("_PulseSpeed", 0.6f);
        Mote = Glow("Mote", Gold * 1.6f, Moon * 0.9f, 1, 0).F("_FogInfluence", 0.6f);
        Spark = Glow("Spark", Color.white * 2.5f, Color.white * 2.5f, 1, 0).F("_FogInfluence", 0f);
        Shimmer = Glow("Shimmer", Light * 1.5f, Light * 1.5f, 1, 0).F("_FogInfluence", 0f);
        WellWater = Glow("WellWater", Light * 2.2f, Moon * 1.4f, 1, 0).F("_Pulse", 0.25f).F("_PulseSpeed", 2f).F("_FogInfluence", 0.4f);
        EyeOrange = Glow("EyeOrange", Hazard * 4f, Hazard * 4f).F("_Fresnel", 0.6f).F("_Flicker", 0.3f).F("_FogInfluence", 0.3f);
        EyeViolet = Glow("EyeViolet", Violet * 3f, Violet * 3f).F("_ExistIn", 2f).F("_Fresnel", 0.6f).F("_FogInfluence", 0.2f).F("_Pulse", 0.4f);
        MothBody = Glow("MothBody", H("#FFE9B0") * 2.5f, Hazard * 3.5f).F("_Fresnel", 0.7f).F("_FogInfluence", 0.4f);
        PlayerDisc = Glow("PlayerDisc", Light * 0.35f, Ember * 0.3f, 1, 0).F("_FogInfluence", 0f);
        GlowLine = Fresh("GlowLine", "Lucid/GlowParticle").C("_Color", Color.white).F("_Trail", 1f);

        Mist = Fresh("Mist", FogShader).C("_DreamColor", H("#D878B8")).C("_NightColor", H("#2B4C73")).C("_DreamGlow", Magenta * 0.15f)
            .F("_Opacity", 0.28f).F("_NightOpacity", 1.6f).F("_Billboard", 2f).F("_NoiseScale", 0.06f).F("_DepthFade", 8f).F("_NearFade", 14f);
        MistWall = Fresh("MistWall", FogShader).C("_DreamColor", H("#B070C8")).C("_NightColor", H("#182C48"))
            .F("_Opacity", 0.75f).F("_NightOpacity", 1.25f).F("_Billboard", 2f).F("_NoiseScale", 0.03f).F("_DepthFade", 14f).F("_NearFade", 20f);
        MistSea = Fresh("MistSea", FogShader).C("_DreamColor", H("#B45AA8")).C("_NightColor", H("#12233F")).C("_DreamGlow", Magenta * 0.1f)
            .F("_Opacity", 0.9f).F("_NightOpacity", 1.1f).F("_Billboard", 0f).F("_NoiseScale", 0.012f).F("_Scroll", 0.4f).F("_DepthFade", 14f).F("_NearFade", 2f).F("_EdgeSoft", 0.6f);
        Seam = Fresh("Seam", "Lucid/World/Seam");
        Sky = Fresh("Sky", "Lucid/World/Sky");
        AssetDatabase.SaveAssets();
    }

    // ---------- Asset helpers ----------

    public static Color W(float k, float alpha = 0f) { return new Color(k, k, k, alpha); }

    public static Mesh SaveMesh(MeshData data, string name, bool smooth = true, bool uv = false)
    {
        var mesh = data.ToMesh(name, smooth, uv);
        string path = MeshDir + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            // Keep the asset (and its GUID) and replace the contents.
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    public static GameObject Part(Transform parent, string name, Mesh mesh, Material material, bool shadows = false)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        r.receiveShadows = true;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return g;
    }

    public static GameObject At(this GameObject g, Vector3 position, Vector3 euler = default(Vector3), float scale = 1f)
    {
        g.transform.localPosition = position;
        g.transform.localEulerAngles = euler;
        g.transform.localScale = Vector3.one * scale;
        return g;
    }

    public static GameObject Scaled(this GameObject g, Vector3 scale) { g.transform.localScale = scale; return g; }

    static GameObject SavePrefab(GameObject root)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + root.name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    public static GameObject Prefab(string name) { return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + name + ".prefab"); }

    static Mesh quad, flatQuad, sphere, shaft;

    static void BuildShared()
    {
        quad = SaveMesh(Quad(), "Quad", false, true);
        flatQuad = SaveMesh(FlatQuad(), "FlatQuad", false, true);
        sphere = SaveMesh(Sphere(10, 14), "Sphere");
        shaft = SaveMesh(WorldMeshes.Shaft(1f, 0.3f, 1f), "Shaft", false, true);
        // Wave front shell: a unit sphere (diameter 1) drawn from both sides.
        SaveMesh(Sphere(24, 36), "SeamShell");
    }

    public static Mesh SeamShell { get { return AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/SeamShell.asset"); } }
    public static Mesh FlatQuadMesh { get { return AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/FlatQuad.asset"); } }
    public static Mesh QuadMesh { get { return AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/Quad.asset"); } }
    public static Mesh SphereMesh { get { return AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/Sphere.asset"); } }

    static float Rnd(System.Random r, float min, float max) { return min + (float)r.NextDouble() * (max - min); }

    // Glowing veins up a trunk: a few of the sides light up in broken runs.
    static Func<float, int, float> Veins(int every, float frequency, float amount = 1f)
    {
        return (t, j) => j % every == 0 ? Mathf.Clamp01(Mathf.Sin(t * frequency + j * 1.7f) * 2f) * amount : 0f;
    }

    // ---------- Build everything ----------

    public static void BuildAll()
    {
        Ensure("Assets/Lucid"); Ensure(Root); Ensure(MatDir); Ensure(MeshDir); Ensure(PrefabDir); Ensure(SettingsDir);
        BuildMaterials();
        BuildShared();
        BuildPlatforms();
        BuildFlora();
        BuildTrees();
        BuildProps();
        BuildLandmarks();
        BuildEnemies();
        AssetDatabase.SaveAssets();
    }

    // ---------- Platforms ----------

    static void Island(string name, float radius, float depth, int seed, int grass, int roots)
    {
        int segs = radius > 20f ? 48 : radius > 10f ? 40 : radius > 5f ? 28 : 18;
        var rock = WorldMeshes.Island(radius, depth, seed, W(1f), W(0.85f), new Color(0.45f, 0.42f, 0.62f), segs, 0.1f);
        // The lip glows faintly, so the edge of every platform reads against the mist.
        rock.Tint((p, c) => { if (p.y < -0.05f && p.y > -0.2f) c.a = 0.9f; return c; });
        var rng = new System.Random(seed * 7 + 1);
        for (int i = 0; i < roots; i++)
        {
            float a = Rnd(rng, 0f, Mathf.PI * 2f), r = radius * Rnd(rng, 0.2f, 0.7f), len = depth * Rnd(rng, 0.8f, 1.6f);
            var start = new Vector3(Mathf.Cos(a) * r, -depth * 0.4f, Mathf.Sin(a) * r);
            var drift = new Vector3(Rnd(rng, -1f, 1f), 0f, Rnd(rng, -1f, 1f)) * len * 0.25f;
            var path = Curve(7, start, start + Vector3.down * len * 0.5f + drift, start + Vector3.down * len + drift * 0.4f);
            float thick = Mathf.Max(0.12f, radius * 0.05f);
            rock.Append(Tube(path, t => Mathf.Lerp(thick, thick * 0.12f, t), 6, seed + i, 0.25f, 0.5f, t => W(0.5f - 0.25f * t)));
        }
        var root = new GameObject(name);
        Part(root.transform, "Rock", SaveMesh(rock, name + "_Rock"), Ground, true);
        var collider = root.AddComponent<MeshCollider>();
        collider.sharedMesh = SaveMesh(Puck(radius * 0.93f, Mathf.Clamp(depth * 0.5f, 0.5f, 3f), radius > 10f ? 24 : 14), name + "_Collider", false);
        collider.convex = true;
        if (grass > 0)
        {
            var g = new MeshData();
            for (int i = 0; i < grass; i++)
            {
                float a = Rnd(rng, 0f, Mathf.PI * 2f), r = Mathf.Sqrt(Rnd(rng, 0.02f, 1f)) * radius * 0.84f;
                g.Append(GrassClump(6, Rnd(rng, 0.35f, 0.75f), 0.4f, seed * 31 + i, W(0.55f), W(1f), 1f),
                    new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), Quaternion.Euler(0f, Rnd(rng, 0f, 360f), 0f), 1f);
            }
            Part(root.transform, "Grass", SaveMesh(g, name + "_Grass", false), Grass);
        }
        SavePrefab(root);
    }

    // A walkable slab with a flat top at y = 0: bark shelves, root platforms, stone steps.
    static void Slab(string name, Vector3 half, int seed, Material material, bool rim)
    {
        var m = Blob(8, 16, half, seed, 0.22f, 1.3f, p => { if (p.y > 0f) p.y *= 0.06f; return p; },
            (p, d) => W(p.y > -0.02f ? 1f : 0.6f + 0.25f * d.y, rim && Mathf.Abs(p.y) < 0.08f && d.y < 0.35f && d.y > -0.2f ? 1f : 0f));
        var root = new GameObject(name);
        Part(root.transform, "Slab", SaveMesh(m, name + "_Mesh"), material, true);
        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -half.y * 0.35f, 0f);
        box.size = new Vector3(half.x * 1.7f, half.y * 0.7f, half.z * 1.7f);
        SavePrefab(root);
    }

    static GameObject ShimmerRing(Transform parent, float size)
    {
        var s = Part(parent, "Shimmer", flatQuad, Shimmer).At(new Vector3(0f, 0.12f, 0f)).Scaled(new Vector3(size, 1f, size));
        s.GetComponent<MeshRenderer>().enabled = false;
        return s;
    }

    static void BuildPlatforms()
    {
        Island("IslandLarge", 15f, 9f, 1, 90, 6);
        Island("IslandMedium", 7f, 5f, 2, 34, 4);
        Island("IslandSmall", 3.4f, 3f, 3, 12, 3);
        Island("IslandTiny", 1.9f, 2f, 4, 5, 2);
        Island("ArenaGround", 36f, 18f, 5, 0, 10);
        Slab("BarkLedge", new Vector3(2.6f, 0.9f, 2f), 11, Bark, true);
        Slab("StoneStep", new Vector3(1.7f, 0.8f, 1.7f), 12, Ground, true);
        Slab("RootDeck", new Vector3(2.2f, 0.8f, 6f), 13, Bark, true);

        // Dream petal platform: a wide blossom that only holds in Dream. In Nightmare it folds into a bud.
        {
            var petal = WorldMeshes.Petal(3.1f, 2.3f, 0.1f, 0.06f, 6, W(0.7f), W(1f), 0.35f, 0.9f);
            var m = PetalRing(7, 0.4f, 2f, 0f, petal, 3);
            m.Append(PetalRing(6, 0.4f, 9f, 26f, WorldMeshes.Petal(2f, 1.6f, 0.2f, 0.1f, 5, W(0.8f), W(1f), 0.5f, 1f), 5), new Vector3(0f, 0.06f, 0f), Quaternion.identity, 1f);
            var heart = Lathe(new[] { new Vector2(0.05f, -0.5f), new Vector2(0.5f, -0.3f), new Vector2(0.62f, 0f), new Vector2(0.4f, 0.16f), new Vector2(0f, 0.2f) }, 10, i => W(1f, i >= 2 ? 1f : 0f));
            var stem = Tube(Curve(8, new Vector3(0f, -0.4f, 0f), new Vector3(0.5f, -2.5f, 0.3f), new Vector3(-0.3f, -5f, 0.8f), new Vector3(0.4f, -7.5f, 0.2f)),
                t => Mathf.Lerp(0.22f, 0.03f, t), 6, 2, 0.1f, 1f, t => W(0.6f));
            var root = new GameObject("PetalPlatform");
            Part(root.transform, "Petals", SaveMesh(m, "PetalPlatform_Petals"), Petal);
            Part(root.transform, "Heart", SaveMesh(heart.Append(stem), "PetalPlatform_Heart"), Bark);
            var collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = SaveMesh(Puck(3.1f, 0.5f, 14), "PetalPlatform_Collider", false);
            collider.convex = true;
            var phase = root.AddComponent<PhasePlatform>();
            phase.existsIn = ExistsIn.Dream;
            phase.shimmer = ShimmerRing(root.transform, 7f).GetComponent<Renderer>();
            SavePrefab(root);
        }

        // Nightmare thorn walkway: bramble that erupts where the wave turns the world to Nightmare.
        {
            var m = Blob(8, 14, new Vector3(1.6f, 0.5f, 3.7f), 21, 0.25f, 1.5f, p => { if (p.y > 0f) p.y *= 0.12f; return p; }, (p, d) => W(p.y > -0.03f ? 1f : 0.6f));
            var rng = new System.Random(5);
            for (int side = -1; side <= 1; side += 2)
            {
                var path = Curve(12, new Vector3(side * 1.3f, -0.2f, -3.9f), new Vector3(side * 1.7f, 0f, -1.4f), new Vector3(side * 1.25f, -0.15f, 1.2f), new Vector3(side * 1.6f, -0.1f, 3.9f));
                m.Append(Tube(path, t => 0.26f, 7, 30 + side, 0.3f, 1.2f, t => W(0.8f), 1f, true, true));
                for (int i = 0; i < 8; i++)
                {
                    Vector3 at = path[1 + i + (i > 4 ? 1 : 0)];
                    m.Append(WorldMeshes.Thorn(Rnd(rng, 0.6f, 1.3f), 0.13f, 0.25f, 6, 4, W(0.8f), W(1f), 1f), at,
                        Quaternion.Euler(Rnd(rng, -25f, 25f), 0f, -side * Rnd(rng, 35f, 75f)), 1f);
                }
            }
            var root = new GameObject("ThornWalkway");
            Part(root.transform, "Bramble", SaveMesh(m, "ThornWalkway_Mesh"), Thorn);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.3f, 0f);
            box.size = new Vector3(2.9f, 0.6f, 7.2f);
            var phase = root.AddComponent<PhasePlatform>();
            phase.existsIn = ExistsIn.Nightmare;
            phase.shimmer = ShimmerRing(root.transform, 7f).GetComponent<Renderer>();
            SavePrefab(root);
        }

        // Wall-run panels: a slab of rock laced with glowing handholds. The panel faces local -X.
        foreach (int exist in new[] { 0, 1, 2 })
        {
            const float length = 14f, height = 5f;
            string name = exist == 0 ? "WallRunPanel" : exist == 1 ? "WallRunPanel_Dream" : "WallRunPanel_Nightmare";
            var m = Blob(8, 18, new Vector3(0.55f, height * 0.56f, length * 0.53f), 40 + exist, 0.12f, 1.1f, p => { p.x = p.x < 0f ? p.x * 0.5f : p.x; p.x += 0.45f; return p; }, (p, d) => W(0.85f));
            var rng = new System.Random(60 + exist);
            for (int v = 0; v < 3; v++)
            {
                float y = -1.4f + v * 1.4f;
                var pts = new List<Vector3>();
                for (int i = 0; i <= 8; i++) pts.Add(new Vector3(0.12f, y + Rnd(rng, -0.45f, 0.45f), -length * 0.5f + length * i / 8f));
                var path = Curve(28, pts.ToArray());
                m.Append(Tube(path, t => 0.12f + 0.05f * Mathf.Sin(t * 30f), 6, v, 0.2f, 2f, t => W(1f), 1f, true, true, (t, j) => 1f));
            }
            var root = new GameObject(name);
            Part(root.transform, "Panel", SaveMesh(m, name + "_Mesh"), exist == 0 ? PanelBoth : exist == 1 ? PanelDream : PanelNight);
            // A buttress of plain rock ties the panel back into the cliff. It stays in both worlds.
            if (exist == 0)
                SaveMesh(Blob(7, 12, new Vector3(2.6f, height * 0.75f, length * 0.56f), 47, 0.3f, 0.9f, null, (p, d) => W(0.6f + 0.2f * d.y)), "WallRunPanel_Buttress");
            Part(root.transform, "Buttress", AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "/WallRunPanel_Buttress.asset"), Rock, true).At(new Vector3(3f, 0f, 0f));
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0.55f, 0f, 0f);
            box.size = new Vector3(0.9f, height + 0.4f, length + 0.4f);
            // The run box sits just behind the face, so the player is held clear of the vines.
            var anchor = new GameObject("RunSurface");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.localPosition = new Vector3(0.3f, 0f, 0f);
            var run = anchor.AddComponent<WallRunSurface>();
            run.size = new Vector3(0.6f, height, length);
            run.existsIn = (ExistsIn)exist;
            if (exist != 0)
            {
                var phase = root.AddComponent<PhasePlatform>();
                phase.existsIn = (ExistsIn)exist;
                phase.grace = 0.15f;
            }
            SavePrefab(root);
        }

        // Cliff backdrop for the Weeping Wall.
        {
            var m = Cliff(70f, 44f, 56, 22, 7, 1.4f, W(1f), W(0.55f));
            var root = new GameObject("CliffBig");
            Part(root.transform, "Face", SaveMesh(m, "CliffBig_Mesh"), Rock, true);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(4.2f, 22f, 0f);
            box.size = new Vector3(6f, 44f, 70f);
            SavePrefab(root);
        }
    }
}
