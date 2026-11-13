using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Builds Lucid's placeholder props as prefabs from primitives (URP/Lit), the Dream sky material,
// a post-processing profile, and a test scene.
// Menu: Lucid > Build Props And Test Scene. Re-running overwrites the same assets.
public static class PropBuilder
{
    const string MatDir = "Assets/Materials/Props";
    const string PrefabDir = "Assets/Prefabs/Props";

    [MenuItem("Lucid/Build Props And Test Scene")]
    public static void BuildAll()
    {
        Ensure("Assets/Materials"); Ensure(MatDir); Ensure("Assets/Materials/Sky");
        Ensure("Assets/Prefabs"); Ensure(PrefabDir);
        Ensure("Assets/Scenes"); Ensure("Assets/Settings");
        NatureBuilder.BuildAll();
        CombatBuilder.Build();
        DreamBuilder.BuildPrefabs();
        NightmareBuilder.BuildPrefabs();
        EnsureFogFeature();
        // Props that did not fit the game were removed.
        foreach (var old in new[] { "ArcadeCabinet", "GasStationCanopy", "TelephonePole", "Echo", "FlowRing", "MemoryGate", "PressurePlate", "CloudPad", "ObsidianSlab", "ThornCluster", "MirrorDoorway" })
            AssetDatabase.DeleteAsset(PrefabDir + "/" + old + ".prefab");
        BuildLamp(); BuildShadowFigure(); BuildRoad(); BuildPillar();
        MemoryBuilder.Build();
        AssetDatabase.SaveAssets();
        BuildScene();
    }

    static void Ensure(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }

    static Color H(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }

    static Material Mat(string name, string hex, float emission = 0f, float smooth = 0.3f, string shader = "Universal Render Pipeline/Lit")
    {
        string p = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(m, p);
        }
        m.SetColor("_BaseColor", H(hex));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            m.SetColor("_EmissionColor", H(hex) * emission);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default(Vector3), bool collider = true)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.transform.localEulerAngles = euler;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static void Save(GameObject root, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + name + ".prefab");
        Object.DestroyImmediate(root);
    }

    // Soft pink fog lamp. Arm points toward local +X.
    static void BuildLamp()
    {
        var metal = Mat("Prop_LampMetal", "#8FA0B8", 0f, 0.5f);
        var bulb = Mat("Prop_LampBulb", "#FF7FC4", 3f);
        var root = new GameObject("StreetLamp");
        Prim(PrimitiveType.Cylinder, "Pole", root.transform, new Vector3(0, 3, 0), new Vector3(0.14f, 3, 0.14f), metal);
        Prim(PrimitiveType.Cube, "Arm", root.transform, new Vector3(0.9f, 6.0f, 0), new Vector3(2.0f, 0.1f, 0.1f), metal, new Vector3(0, 0, 6));
        Prim(PrimitiveType.Sphere, "Bulb", root.transform, new Vector3(1.8f, 5.95f, 0), new Vector3(0.45f, 0.3f, 0.45f), bulb, default(Vector3), false);
        var lg = new GameObject("Glow");
        lg.transform.SetParent(root.transform, false);
        lg.transform.localPosition = new Vector3(1.8f, 5.6f, 0);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.color = H("#FF7FC4"); l.intensity = 4f; l.range = 14f;
        Save(root, "StreetLamp");
    }

    // Faceless dark silhouette. Unlit black on purpose.
    static void BuildShadowFigure()
    {
        var black = Mat("Prop_ShadowBlack", "#030206", 0f, 0f, "Universal Render Pipeline/Unlit");
        var root = new GameObject("ShadowFigure");
        Prim(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0, 1.15f, 0), new Vector3(0.6f, 1.15f, 0.45f), black, default(Vector3), false);
        Prim(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0, 2.55f, 0), new Vector3(0.42f, 0.48f, 0.42f), black, default(Vector3), false);
        Prim(PrimitiveType.Capsule, "ArmL", root.transform, new Vector3(-0.42f, 1.35f, 0), new Vector3(0.14f, 0.85f, 0.14f), black, new Vector3(0, 0, 4), false);
        Prim(PrimitiveType.Capsule, "ArmR", root.transform, new Vector3(0.42f, 1.35f, 0), new Vector3(0.14f, 0.85f, 0.14f), black, new Vector3(0, 0, -4), false);
        Save(root, "ShadowFigure");
    }

    // 8 wide, 20 long along Z. Chain segments by 20 units.
    static void BuildRoad()
    {
        var asphalt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Nature/Nature_Asphalt.mat");
        if (asphalt == null) asphalt = Mat("Prop_Asphalt", "#4A4660", 0f, 0.35f);
        var line = Mat("Prop_RoadLine", "#F5F0E6");
        var root = new GameObject("RoadSegment");
        var asphaltCube = Prim(PrimitiveType.Cube, "Asphalt", root.transform, new Vector3(0, 0.025f, 0), new Vector3(8, 0.05f, 20), asphalt);
        var wet = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Nature/Nature_AsphaltWet.mat");
        if (wet != null) { var rs = asphaltCube.AddComponent<WorldMaterialSwap>(); rs.dream = asphalt; rs.nightmare = wet; }
        Prim(PrimitiveType.Cube, "EdgeL", root.transform, new Vector3(-3.7f, 0.06f, 0), new Vector3(0.12f, 0.02f, 20), line, default(Vector3), false);
        Prim(PrimitiveType.Cube, "EdgeR", root.transform, new Vector3(3.7f, 0.06f, 0), new Vector3(0.12f, 0.02f, 20), line, default(Vector3), false);
        for (int i = 0; i < 5; i++)
            Prim(PrimitiveType.Cube, "Dash" + i, root.transform, new Vector3(0, 0.06f, -8 + i * 4), new Vector3(0.15f, 0.02f, 2), line, default(Vector3), false);
        Save(root, "RoadSegment");
    }

    static void BuildPillar()
    {
        var stone = Mat("Prop_StoneLight", "#C9BFD6", 0f, 0.1f);
        var root = new GameObject("RuinPillar");
        Prim(PrimitiveType.Cube, "Base", root.transform, new Vector3(0, 0.3f, 0), new Vector3(2.2f, 0.6f, 2.2f), stone);
        Prim(PrimitiveType.Cylinder, "Shaft", root.transform, new Vector3(0, 3.6f, 0), new Vector3(1.4f, 3f, 1.4f), stone);
        Prim(PrimitiveType.Cube, "BrokenTop", root.transform, new Vector3(0.25f, 6.9f, 0), new Vector3(1.2f, 0.7f, 1.1f), stone, new Vector3(8, 20, 14));
        Prim(PrimitiveType.Cube, "Fallen", root.transform, new Vector3(2.6f, 0.45f, 1.4f), new Vector3(1.2f, 0.9f, 1.2f), stone, new Vector3(0, 35, 12));
        Save(root, "RuinPillar");
    }

    static GameObject Place(string prefab, Vector3 pos, Vector3 euler)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + prefab + ".prefab");
        var g = (GameObject)PrefabUtility.InstantiatePrefab(p);
        g.transform.position = pos;
        g.transform.eulerAngles = euler;
        return g;
    }

    static Material BuildSky()
    {
        string p = "Assets/Materials/Sky/DreamSky.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = new Material(Shader.Find("Lucid/DreamSky"));
            AssetDatabase.CreateAsset(m, p);
        }
        // Saturated dusk: deep blue, violet, hot pink, peach horizon.
        m.SetColor("_Top", H("#16258F"));
        m.SetColor("_Violet", H("#8A4FE0"));
        m.SetColor("_Pink", H("#FF5FA8"));
        m.SetColor("_Horizon", H("#FFB08A"));
        EditorUtility.SetDirty(m);
        return m;
    }

    static VolumeProfile BuildVolume(bool nightmare)
    {
        string p = nightmare ? "Assets/Settings/NightmareVolume.asset" : "Assets/Settings/DreamVolume.asset";
        AssetDatabase.DeleteAsset(p);
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(prof, p);

        var bloom = prof.Add<Bloom>(true);
        var ca = prof.Add<ChromaticAberration>(true);
        var fg = prof.Add<FilmGrain>(true);
        var vg = prof.Add<Vignette>(true);
        var ca2 = prof.Add<ColorAdjustments>(true);
        var dof = prof.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(nightmare ? 45f : 40f);
        dof.gaussianEnd.Override(nightmare ? 110f : 120f);
        dof.gaussianMaxRadius.Override(nightmare ? 1.0f : 1.4f);
        if (!nightmare)
        {
            bloom.threshold.Override(0.9f); bloom.intensity.Override(0.9f); bloom.scatter.Override(0.75f);
            ca.intensity.Override(0.35f);
            fg.type.Override(FilmGrainLookup.Thin2); fg.intensity.Override(0.5f);
            vg.intensity.Override(0.32f); vg.smoothness.Override(0.5f);
            ca2.saturation.Override(12f); ca2.postExposure.Override(0.25f);
            ca2.colorFilter.Override(H("#FFE6F4"));
        }
        else
        {
            bloom.threshold.Override(0.85f); bloom.intensity.Override(1.1f); bloom.scatter.Override(0.8f);
            ca.intensity.Override(0.9f);
            fg.type.Override(FilmGrainLookup.Large01); fg.intensity.Override(0.9f);
            vg.intensity.Override(0.55f); vg.smoothness.Override(0.45f);
            ca2.saturation.Override(-15f); ca2.postExposure.Override(-0.7f); ca2.contrast.Override(30f);
            ca2.colorFilter.Override(H("#FFB59A"));
        }

        var vf = prof.Add<VolumetricFogVolumeComponent>(true);
        vf.enabled.Override(true);
        vf.distance.Override(nightmare ? 120f : 130f);
        vf.baseHeight.Override(0f); vf.maximumHeight.Override(nightmare ? 18f : 28f);
        vf.density.Override(nightmare ? 0.38f : 0.32f);
        vf.attenuationDistance.Override(nightmare ? 50f : 90f);
        vf.enableMainLightContribution.Override(true);
        vf.anisotropy.Override(0.65f); vf.scattering.Override(nightmare ? 0.5f : 0.7f);
        vf.tint.Override(nightmare ? H("#7A1A0E") : H("#FFB6DA"));
        foreach (var o in new VolumeComponent[] { bloom, ca, fg, vg, ca2, vf, dof })
        {
            o.name = o.GetType().Name;
            AssetDatabase.AddObjectToAsset(o, prof);
        }
        AssetDatabase.SaveAssets();
        return prof;
    }

    static void EnsureFogFeature()
    {
        var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Lucid_Renderer.asset");
        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Lucid_URP.asset");
        if (rp != null) { rp.supportsCameraDepthTexture = true; EditorUtility.SetDirty(rp); }
        if (rd == null) return;
        foreach (var f in rd.rendererFeatures) if (f is VolumetricFogRendererFeature) return;
        var feat = ScriptableObject.CreateInstance<VolumetricFogRendererFeature>();
        feat.name = "VolumetricFog";
        AssetDatabase.AddObjectToAsset(feat, rd);
        rd.rendererFeatures.Add(feat);
        EditorUtility.SetDirty(rd);
        AssetDatabase.SaveAssets();
    }

    static Volume MakeVolume(string name, VolumeProfile prof, float weight)
    {
        var v = new GameObject(name).AddComponent<Volume>();
        v.isGlobal = true;
        v.sharedProfile = prof;
        v.weight = weight;
        return v;
    }

    static void WorldOnly(GameObject g, WorldVisibility w)
    {
        var o = g.AddComponent<WorldObject>();
        o.visibleIn = w;
    }

    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var grassD = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Nature/Nature_GrassGround.mat");
        var grassN = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Nature/Nature_BurnedGround.mat");
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(40, 1, 40);
        ground.GetComponent<Renderer>().sharedMaterial = grassD;
        var gs = ground.AddComponent<WorldMaterialSwap>();
        gs.dream = grassD; gs.nightmare = grassN;

        BuildGardenRoute();
        NatureBuilder.Populate();
        WorldOnly(Place("ShadowFigure", new Vector3(-26, 0, 70), new Vector3(0, 160, 0)), WorldVisibility.Both);
        Place("RuinPillar", new Vector3(-16, 0, 34), Vector3.zero);
        Place("RuinPillar", new Vector3(18, 0, 62), new Vector3(0, 40, 0));

        // Player: empty root with a character controller, the Lucid visual as a child.
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 0.05f, -6);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f; cc.radius = 0.5f; cc.center = new Vector3(0, 1f, 0);
        var pc = player.AddComponent<PlayerController>();
        player.AddComponent<PlayerCombat>();
        var visual = Place("Lucid", player.transform.position, Vector3.zero);
        visual.transform.SetParent(player.transform, true);
        LevelCombat.Build(player.transform);

        var cam = Camera.main;
        if (cam != null)
        {
            cam.fieldOfView = 60;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 500;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var tpc = cam.gameObject.AddComponent<ThirdPersonCamera>();
            tpc.target = player.transform;
            pc.cameraTransform = cam.transform;
            cam.transform.position = new Vector3(0, 3.4f, -14);
            cam.transform.rotation = Quaternion.Euler(14, 0, 0);
        }
        var sun = Object.FindFirstObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            sun.transform.rotation = Quaternion.Euler(8, 180, 0);
            sun.color = H("#FFB68A");
            sun.intensity = 1.3f;
        }

        var sky = BuildSky();
        RenderSettings.skybox = sky;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = H("#F2A6CC");
        RenderSettings.fogStartDistance = 20;
        RenderSettings.fogEndDistance = 150;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = H("#B093D0");

        var dreamVol = MakeVolume("DreamVolume", BuildVolume(false), 1f);
        var nightVol = MakeVolume("NightmareVolume", BuildVolume(true), 0f);

        var wm = new GameObject("WorldManager");
        var look = wm.AddComponent<WorldLook>();
        look.sky = sky; look.sun = sun;
        look.dreamVolume = dreamVol; look.nightmareVolume = nightVol;
        var state = wm.AddComponent<WorldState>();
        state.look = look;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Level1.unity");
    }

    // Shared, flush collision plane keeps dashes smooth through both versions of the garden.
    static void BuildGardenRoute()
    {
        var root = new GameObject("The Remembered Garden").transform;
        var pearl = Mat("Garden_Pearl", "#D1DED5", 0f, 0.55f);
        var basalt = Mat("Garden_Basalt", "#17131F", 0f, 0.2f);
        var gold = Mat("Garden_MoonGold", "#B5F1DA", 0.6f);
        var ember = Mat("Garden_Ember", "#D44720", 0.8f);
        for (int i = 0; i < 34; i++)
        {
            float z = -9f + i * 3.8f;
            float x = Mathf.Sin(i * 0.23f) * 2.5f;
            var slab = Prim(PrimitiveType.Cube, "Memory stepping stone", root,
                new Vector3(x, 0.035f, z), new Vector3(9f + Mathf.Sin(i) * 1.2f, 0.06f, 3.5f),
                pearl, new Vector3(0, Mathf.Sin(i * 0.7f) * 9f, 0), false);
            var swap = slab.AddComponent<WorldMaterialSwap>(); swap.dream = pearl; swap.nightmare = basalt;
            for (int side = -1; side <= 1; side += 2)
            {
                var seam = Prim(PrimitiveType.Cube, "Luminous mineral seam", root,
                    new Vector3(x + side * 4.5f, 0.075f, z), new Vector3(0.08f, 0.025f, 2.6f),
                    gold, new Vector3(0, side * 14f, 0), false);
                var color = seam.AddComponent<WorldMaterialSwap>(); color.dream = gold; color.nightmare = ember;
                if (i % 3 == 0)
                {
                    var shard = Prim(PrimitiveType.Cube, "Nightmare fault shard", root,
                        new Vector3(side * (9f + i % 4), 0.7f, z), new Vector3(1.4f, 3f, 1.2f),
                        basalt, new Vector3(side * 24f, i * 31f, 18f), false);
                    WorldOnly(shard, WorldVisibility.NightmareOnly);
                }
            }
        }
        for (int i = 0; i < 3; i++)
        {
            var terrace = Prim(PrimitiveType.Cylinder, "Sanctuary terrace", root,
                new Vector3(0, 0.025f, 26f + i * 36f), new Vector3(27f, 0.02f, 27f), pearl, collider: false);
            var swap = terrace.AddComponent<WorldMaterialSwap>(); swap.dream = pearl; swap.nightmare = basalt;
        }
    }
}
