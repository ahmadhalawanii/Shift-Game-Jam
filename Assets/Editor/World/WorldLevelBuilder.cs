using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static BuildKit;

// Builds the redesigned level: one connected climb of six zones to the Sleeper's arena.
// Menu: Lucid > Build Lucid World. Re-running rebuilds the kit, the look assets and the scene.
// Layout notes are in LEVEL_BIBLE.md. Positions are in metres; the boss arena is fixed at (0, 0, 141).
public static class WorldLevelBuilder
{
    public const string ScenePath = WorldKit.Root + "/Scenes/LucidWorld.unity";
    static Transform parent;
    static System.Random rng;

    [MenuItem("Lucid/Build Lucid World")]
    public static void BuildAll()
    {
        if (!CanReplaceOpenScene()) return;
        Ensure("Assets/Lucid"); Ensure(WorldKit.Root); Ensure(WorldKit.Root + "/Scenes");
        WorldKit.BuildAll();
        BuildScene();
    }

    [MenuItem("Lucid/Build Lucid World (scene only)")]
    public static void BuildSceneOnly()
    {
        if (!CanReplaceOpenScene()) return;
        WorldKit.BuildMaterials();
        BuildScene();
    }

    // The build contains the new world only. Run once the new world is approved.
    [MenuItem("Lucid/Use Lucid World In Build")]
    public static void UseInBuild()
    {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("Build Settings now contain only " + ScenePath);
    }

    // Never throw away unsaved work in whatever scene is open.
    static bool CanReplaceOpenScene()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var s = EditorSceneManager.GetSceneAt(i);
            if (s.isDirty && s.path != ScenePath)
            {
                Debug.LogError("Build Lucid World: the open scene '" + s.name + "' has unsaved changes. Save or discard them first.");
                return false;
            }
        }
        return true;
    }

    // ---------- Placement helpers ----------

    static GameObject Put(string prefab, Vector3 position, float yaw = 0f, float scale = 1f)
    {
        return Put(prefab, position, new Vector3(0f, yaw, 0f), Vector3.one * scale);
    }

    static GameObject Put(string prefab, Vector3 position, Vector3 euler, Vector3 scale)
    {
        var asset = WorldKit.Prefab(prefab);
        if (asset == null) throw new System.InvalidOperationException("Missing kit prefab: " + prefab);
        var g = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        g.transform.position = position;
        g.transform.eulerAngles = euler;
        g.transform.localScale = scale;
        return g;
    }

    static Transform Group(string name)
    {
        parent = new GameObject(name).transform;
        return parent;
    }

    static float R(float min, float max) { return min + (float)rng.NextDouble() * (max - min); }

    // Flora on top of an island: big things toward the rim so the middle stays readable.
    static void Dress(Vector3 centre, float radius, int blossoms, int mushrooms, int flowers, int thorns)
    {
        for (int i = 0; i < blossoms; i++) Put("Blossom", OnDisc(centre, radius, 0.55f, 0.85f), R(0f, 360f), R(0.7f, 1.5f));
        for (int i = 0; i < mushrooms; i++) Put("Mushrooms", OnDisc(centre, radius, 0.4f, 0.85f), R(0f, 360f), R(0.7f, 1.4f));
        for (int i = 0; i < flowers; i++) Put("FlowerPatch", OnDisc(centre, radius, 0.15f, 0.8f), R(0f, 360f), R(0.8f, 1.3f));
        for (int i = 0; i < thorns; i++) Put("ThornCluster", OnDisc(centre, radius, 0.6f, 0.9f), R(0f, 360f), R(0.6f, 1.3f));
    }

    static Vector3 OnDisc(Vector3 centre, float radius, float min, float max)
    {
        float a = R(0f, Mathf.PI * 2f), d = radius * R(min, max);
        return centre + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
    }

    static void Checkpoint(int zone, Vector3 position, float radius)
    {
        var g = new GameObject("Checkpoint " + zone);
        g.transform.SetParent(parent, false);
        g.transform.position = position;
        var c = g.AddComponent<LucidCheckpoint>();
        c.zone = zone;
        c.radius = radius;
    }

    static void Fog(Vector3 position, float width, float height, bool wall = false)
    {
        Put(wall ? "FogWall" : "FogCard", position, Vector3.zero, new Vector3(width, height, 1f));
    }

    static void ShaftOfLight(Vector3 top, float length, float width, float tilt = 12f)
    {
        Put("LightShaft", top, new Vector3(tilt, R(0f, 360f), R(-8f, 8f)), new Vector3(width, length, width));
    }

    // ---------- Look assets: sky, post-processing, pipeline ----------

    static VolumeProfile Profile(string name)
    {
        string path = WorldKit.SettingsDir + "/" + name + ".asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }
        foreach (var c in profile.components.ToArray())
        {
            AssetDatabase.RemoveObjectFromAsset(c);
            Object.DestroyImmediate(c, true);
        }
        profile.components.Clear();
        return profile;
    }

    static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        var c = profile.Add<T>(true);
        c.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(c, profile);
        return c;
    }

    static VolumeProfile WorldProfile(bool nightmare)
    {
        var p = Profile(nightmare ? "NightmareProfile" : "DreamProfile");
        var bloom = Add<Bloom>(p);
        bloom.threshold.Override(nightmare ? 0.82f : 0.9f);
        bloom.intensity.Override(nightmare ? 1.25f : 1.55f);
        bloom.scatter.Override(nightmare ? 0.74f : 0.8f);
        bloom.tint.Override(nightmare ? H("#CFE4FF") : H("#FFE9F6"));
        bloom.highQualityFiltering.Override(true);
        var vignette = Add<Vignette>(p);
        vignette.intensity.Override(nightmare ? 0.42f : 0.26f);
        vignette.smoothness.Override(nightmare ? 0.45f : 0.55f);
        vignette.color.Override(nightmare ? H("#02040A") : H("#1B1B5E"));
        var grade = Add<ColorAdjustments>(p);
        grade.postExposure.Override(nightmare ? -0.1f : 0f);
        grade.contrast.Override(nightmare ? 20f : 14f);
        grade.saturation.Override(nightmare ? -14f : 14f);
        grade.colorFilter.Override(nightmare ? H("#D6E8FF") : H("#FFF2FA"));
        var split = Add<SplitToning>(p);
        split.shadows.Override(nightmare ? H("#1E3F66") : H("#5A3FA0"));
        split.highlights.Override(nightmare ? H("#BFD8EE") : H("#FFE2B8"));
        split.balance.Override(nightmare ? -10f : 10f);
        var tone = Add<Tonemapping>(p);
        tone.mode.Override(TonemappingMode.Neutral);
        var dof = Add<DepthOfField>(p);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(nightmare ? 40f : 60f);
        dof.gaussianEnd.Override(nightmare ? 130f : 190f);
        dof.gaussianMaxRadius.Override(nightmare ? 1.1f : 0.9f);
        dof.highQualitySampling.Override(false);
        EditorUtility.SetDirty(p);
        return p;
    }

    // Only on while a wave is crossing the screen.
    static VolumeProfile ShiftProfile()
    {
        var p = Profile("ShiftProfile");
        var ca = Add<ChromaticAberration>(p);
        ca.intensity.Override(0.9f);
        var lens = Add<LensDistortion>(p);
        lens.intensity.Override(-0.22f);
        lens.scale.Override(1.04f);
        EditorUtility.SetDirty(p);
        return p;
    }

    static Volume MakeVolume(string name, VolumeProfile profile, float weight, float priority)
    {
        var v = new GameObject(name).AddComponent<Volume>();
        v.transform.SetParent(parent, false);
        v.isGlobal = true;
        v.sharedProfile = profile;
        v.weight = weight;
        v.priority = priority;
        return v;
    }

    static void ConfigurePipeline()
    {
        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Lucid_URP.asset");
        if (rp == null) return;
        rp.supportsCameraDepthTexture = true;   // fog cards and glow fade against the scene
        rp.supportsCameraOpaqueTexture = true;  // the wave seam bends the scene behind it
        EditorUtility.SetDirty(rp);
    }

    // ---------- Scene ----------

    static void BuildScene()
    {
        ConfigurePipeline();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        rng = new System.Random(2026);

        // --- Core objects ---
        Group("World");
        var sunLight = Object.FindFirstObjectByType<Light>();
        sunLight.name = "Key Light";
        sunLight.type = LightType.Directional;
        sunLight.shadows = LightShadows.Soft;
        sunLight.shadowStrength = 0.7f;
        RenderSettings.sun = sunLight;
        RenderSettings.skybox = WorldKit.Sky;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

        var dreamVolume = MakeVolume("Dream Volume", WorldProfile(false), 1f, 0f);
        var nightVolume = MakeVolume("Nightmare Volume", WorldProfile(true), 0f, 0f);
        var shiftVolume = MakeVolume("Shift Volume", ShiftProfile(), 0f, 10f);

        var player = BuildPlayer(new Vector3(0f, -33.9f, -158f));
        var tree = BuildArena();

        var manager = new GameObject("WorldManager");
        var state = manager.AddComponent<WorldState>();
        state.transitionTime = 1.2f;      // matches the wave crossing the play space
        state.transitionShake = 0f;       // the wave gives its own shake
        var shift = manager.AddComponent<WorldShift>();
        shift.shellMesh = WorldKit.SeamShell;
        shift.shellMaterial = WorldKit.Seam;
        shift.sparkMaterial = WorldKit.Spark;
        var look = manager.AddComponent<LucidWorldLook>();
        look.sun = sunLight;
        look.landmark = tree;
        look.dreamVolume = dreamVolume;
        look.nightmareVolume = nightVolume;
        look.shiftVolume = shiftVolume;
        var dressing = manager.AddComponent<ArenaDressing>();
        dressing.floor = WorldKit.ArenaFloor;
        dressing.stones = WorldKit.Rock;
        manager.AddComponent<AudioSource>();
        manager.AddComponent<WorldAmbience>();
        var flow = manager.AddComponent<LevelFlow>();
        flow.player = player;

        var zones = new List<LevelFlow.Zone>();
        BuildGlade(zones);
        BuildBridge(zones);
        BuildWall(zones);
        BuildGloam(zones);
        BuildRootclimb(zones);
        BuildApproach(zones);
        flow.zones = zones.ToArray();
        BuildBackdrop();

        var hud = new GameObject("LevelHUD").AddComponent<LevelHUD>();
        hud.player = player.transform;
        hud.avatar = player.GetComponentInChildren<MemoryAvatar>();
        hud.combat = player.GetComponent<PlayerCombat>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Lucid world built: " + ScenePath);
    }

    static void Zone(List<LevelFlow.Zone> zones, string name, Vector3 spawn, string hint)
    {
        zones.Add(new LevelFlow.Zone { name = name, spawn = spawn, yaw = 0f, hint = hint });
    }

    static PlayerController BuildPlayer(Vector3 position)
    {
        var player = new GameObject("Player");
        player.transform.position = position;
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f; cc.radius = 0.5f; cc.center = new Vector3(0f, 1f, 0f);
        cc.stepOffset = 0.4f;
        var pc = player.AddComponent<PlayerController>();
        player.AddComponent<PlayerCombat>();
        var visualAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/Lucid.prefab");
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualAsset);
        visual.transform.SetParent(player.transform, false);
        // The player is light: no cast shadow, no dark shape trailing behind.
        foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;

        var fx = new GameObject("Traversal Fx");
        fx.transform.SetParent(player.transform, false);
        fx.transform.localPosition = new Vector3(0f, 1f, 0f);
        var trail = fx.AddComponent<TrailRenderer>();
        trail.sharedMaterial = WorldKit.GlowLine;
        trail.time = 0.45f;
        trail.widthMultiplier = 0.7f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        trail.minVertexDistance = 0.15f;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.emitting = false;
        var motes = new GameObject("Hover Motes");
        motes.transform.SetParent(fx.transform, false);
        motes.transform.localPosition = new Vector3(0f, -0.8f, 0f);
        var ps = motes.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.7f; main.startSpeed = 0.6f; main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0.25f; main.maxParticles = 80;
        var emission = ps.emission;
        emission.rateOverTime = 60f;
        emission.enabled = false;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.5f; shape.rotation = new Vector3(90f, 0f, 0f);
        var pr = motes.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = WorldKit.Mote;
        pr.shadowCastingMode = ShadowCastingMode.Off;
        var tfx = fx.AddComponent<TraversalFx>();
        tfx.player = pc; tfx.trail = trail; tfx.hoverMotes = ps;

        var cam = Camera.main;
        cam.fieldOfView = 65f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.2f;
        cam.farClipPlane = 1200f;
        cam.allowHDR = true;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var tpc = cam.gameObject.AddComponent<ThirdPersonCamera>();
        tpc.target = player.transform;
        pc.cameraTransform = cam.transform;
        cam.transform.position = position + new Vector3(0f, 3.4f, -8f);
        cam.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
        return pc;
    }

    // ---------- Boss arena: the clearing at the foot of the spirit tree ----------

    static Transform BuildArena()
    {
        Group("Boss Arena");
        Vector3 c = EclipseEncounter.ArenaCenter;
        Put("ArenaGround", c);
        Put("ArenaRootWall", c);
        var tree = Put("SpiritTree", c + new Vector3(0f, 0f, 76f));
        // Nightmare: a skull canopy hangs over the far side of the clearing.
        Put("Skull", c + new Vector3(0f, 31f, 47f), new Vector3(-24f, 0f, 0f), Vector3.one * 2.5f);
        // Fog closes the edges, so the arena reads as a lit clearing in the mist.
        for (int i = 0; i < 10; i++)
        {
            float a = i * 36f + 18f;
            if (Mathf.Abs(Mathf.DeltaAngle(a, 180f)) < 30f) continue;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Fog(c + d * 46f + Vector3.up * 9f, 64f, 30f, true);
        }
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f + 22.5f;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Put("LanternLit", c + d * 30.5f, a + 90f, 1.6f);
            // Flora sits between the lanterns, outside the ring, and never in the entrance.
            float b = a + 22.5f;
            if (Mathf.Abs(Mathf.DeltaAngle(b, 180f)) < 20f) continue;
            Vector3 e = Quaternion.Euler(0f, b, 0f) * Vector3.forward;
            Put("ThornCluster", c + e * 32.5f, R(0f, 360f), 1.5f);
            Put("Blossom", c + e * 31.5f + Vector3.down * 0.3f, R(0f, 360f), 2.4f);
        }
        ShaftOfLight(c + new Vector3(-14f, 60f, 30f), 66f, 14f, 18f);
        ShaftOfLight(c + new Vector3(16f, 60f, 22f), 64f, 11f, 14f);
        ShaftOfLight(c + new Vector3(0f, 62f, 40f), 66f, 18f, 20f);
        Put("Motes", c + Vector3.up * 6f, Vector3.zero, new Vector3(2.2f, 1.2f, 2.2f));
        return tree.transform;
    }

    // ---------- Zone 1: Threshold Glade ----------

    static void BuildGlade(List<LevelFlow.Zone> zones)
    {
        Group("Zone 1 Threshold Glade");
        var c = new Vector3(0f, -34f, -150f);
        Zone(zones, "Threshold Glade", new Vector3(0f, -33.9f, -158f),
            "Space jumps; hold it to hover in Dream. Press Q to shift the world, then strike the glowing bud (left-click) to open the thorns.");
        Put("IslandLarge", c);
        Put("TreeB", c + new Vector3(-9.5f, 0f, 3f), 40f);
        Put("TreeA", c + new Vector3(10f, 0f, -4f), 200f);
        Put("TreeC", c + new Vector3(-6f, 0f, -10.5f), 90f);
        Put("LanternLit", c + new Vector3(3.2f, 0f, 3f), 200f);
        Put("Lantern", c + new Vector3(-3.4f, 0f, 9f), -20f);
        Put("RockA", c + new Vector3(6.5f, 0.2f, 7f), 30f, 1.4f);
        Put("RockSpire", c + new Vector3(-7.5f, 1.5f, 10.5f), 0f, 1.2f);
        Put("RockSpire", c + new Vector3(7.8f, 1.2f, 11f), 70f, 1f);
        Dress(c, 15f, 5, 4, 8, 3);
        Put("StoneStep", c + new Vector3(0f, 0.7f, -1f), 20f);
        Put("StoneStep", c + new Vector3(1.5f, 1.4f, 3.2f), 60f, 0.9f);

        // The way on is closed by thorns. Their heart only opens in Nightmare.
        var gate = Put("ThornGate", c + new Vector3(0f, 0f, 12.6f)).GetComponent<ThornGate>();
        var bud = Put("CorruptBud", c + new Vector3(0f, 2.4f, 11.6f)).GetComponent<CorruptBud>();
        bud.gates = new[] { gate };
        Put("RootArch", c + new Vector3(0f, 0f, 13.4f), 0f, 1.1f);
        Put("HangingVines", c + new Vector3(-8f, 13f, 2f), 30f);
        ShaftOfLight(c + new Vector3(-4f, 30f, 4f), 31f, 7f);
        ShaftOfLight(c + new Vector3(5f, 30f, -3f), 31f, 5f);
        Put("Motes", c + Vector3.up * 4f);
        Fog(c + new Vector3(-26f, 2f, 10f), 44f, 18f);
        Fog(c + new Vector3(27f, 0f, 2f), 40f, 16f);
    }

    // ---------- Zone 2: The Hollow Bridge ----------

    static void BuildBridge(List<LevelFlow.Zone> zones)
    {
        Group("Zone 2 Hollow Bridge");
        Zone(zones, "The Hollow Bridge", new Vector3(0f, -33.9f, -129f),
            "Petals only hold in Dream. Thorns only hold in Nightmare. Press Q in the air to land on the other world. Shift dashes across long gaps.");
        Put("IslandSmall", new Vector3(0f, -34f, -129f));
        Checkpoint(1, new Vector3(0f, -33f, -129f), 3.6f);
        Put("Lantern", new Vector3(-2.2f, -34f, -128f), 60f);
        Put("PetalPlatform", new Vector3(-2f, -33.7f, -119.5f), 0f);
        Put("IslandTiny", new Vector3(1f, -33.4f, -112.5f));
        Put("Mushrooms", new Vector3(1.6f, -33.4f, -112f), 0f, 0.7f);
        Put("ThornWalkway", new Vector3(-1f, -33f, -105f), 8f);
        Put("IslandSmall", new Vector3(0f, -32.6f, -96.5f), 120f);
        Put("LanternLit", new Vector3(2.2f, -32.6f, -96f), 200f);
        Put("FlowerPatch", new Vector3(-1.5f, -32.6f, -97.5f));
        Put("PetalPlatform", new Vector3(3f, -32.2f, -85.8f), 50f);
        Put("ThornWalkway", new Vector3(-1f, -31.6f, -75f), -12f);
        Put("IslandMedium", new Vector3(0f, -30.8f, -61f));
        Dress(new Vector3(0f, -30.8f, -61f), 7f, 2, 2, 4, 2);
        Put("TreeC", new Vector3(-4.6f, -30.8f, -62f), 10f);

        Put("RootArchBig", new Vector3(0f, -44f, -101f), 12f, 1.2f);
        Put("RootArchBig", new Vector3(3f, -50f, -80f), -20f, 1.5f);
        Put("HangingVines", new Vector3(0f, -19.5f, -101f), 100f, 1.2f);
        Put("HangingVines", new Vector3(4f, -19f, -82f), 70f, 1.3f);
        Put("Motes", new Vector3(0f, -29f, -100f), Vector3.zero, new Vector3(1.2f, 1f, 2f));
        Fog(new Vector3(-24f, -36f, -100f), 60f, 16f);
        Fog(new Vector3(22f, -38f, -88f), 56f, 16f);
        Fog(new Vector3(0f, -41f, -70f), 70f, 12f);
        // Nightmare: something vast stands in the mist beside the bridge.
        Put("Lurker", new Vector3(-74f, -24f, -92f), new Vector3(0f, -90f, 0f), Vector3.one * 2.4f);
    }

    // ---------- Zone 3: The Weeping Wall ----------

    static void BuildWall(List<LevelFlow.Zone> zones)
    {
        Group("Zone 3 Weeping Wall");
        Zone(zones, "The Weeping Wall", new Vector3(0f, -30.7f, -62f),
            "Jump at a wall with glowing handholds to run along it; Space leaps off. Handholds exist in one world: press Q mid-run when they change.");
        Checkpoint(2, new Vector3(0f, -30f, -61f), 6f);
        Put("CliffBig", new Vector3(7.4f, -54f, -22f));
        Put("CliffBig", new Vector3(10.5f, -20f, -26f), new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1.1f));

        // First run: a wall that holds in both worlds, with ledges below to catch a miss.
        Put("WallRunPanel", new Vector3(6f, -28.3f, -44f));
        Put("BarkLedge", new Vector3(2.4f, -31.8f, -47.5f), 20f, 0.8f);
        Put("BarkLedge", new Vector3(2.2f, -31.2f, -39.5f), -15f, 0.8f);
        Put("IslandMedium", new Vector3(2.5f, -30f, -30.5f), 40f);
        Put("LanternLit", new Vector3(0.5f, -30f, -31f), 150f);
        Put("FlowerPatch", new Vector3(1f, -30f, -27.5f));
        Put("Mushrooms", new Vector3(-1.5f, -30f, -32f), 0f, 0.8f);
        // Second run: Dream handholds, then Nightmare handholds. Shift where they overlap.
        Put("WallRunPanel_Dream", new Vector3(6f, -27f, -19f));
        Put("WallRunPanel_Nightmare", new Vector3(6f, -26.3f, -10.5f));
        // The run ends over a wide landing, one step below the hollow.
        Put("IslandMedium", new Vector3(3f, -28f, 3f), 200f);
        Put("Lantern", new Vector3(0.5f, -28f, 1f), 100f);

        for (int i = 0; i < 5; i++) Put("HangingVines", new Vector3(8.2f + R(-0.5f, 1.5f), -12f - i * 1.5f, -50f + i * 12f), R(-8f, 8f), R(1f, 1.5f));
        for (int i = 0; i < 4; i++) ShaftOfLight(new Vector3(4f + R(-2f, 1f), -4f, -48f + i * 13f), 30f, R(3f, 6f), 8f);
        Put("ThornCluster", new Vector3(7.4f, -29.5f, -34f), 0f, 1.6f);
        Put("ThornCluster", new Vector3(7.6f, -24f, -10f), 0f, 2f);
        Put("Mushrooms", new Vector3(7.2f, -25.6f, -24f), 0f, 1.6f);
        Put("Motes", new Vector3(2f, -26f, -28f), Vector3.zero, new Vector3(0.8f, 1.2f, 2.2f));
        Fog(new Vector3(-22f, -32f, -30f), 64f, 18f);
        Fog(new Vector3(-6f, -40f, -22f), 60f, 12f);
    }

    // ---------- Zone 4: The Gloam Hollow (combat gate) ----------

    static void BuildGloam(List<LevelFlow.Zone> zones)
    {
        Group("Zone 4 Gloam Hollow");
        var c = new Vector3(0f, -27f, 19f);
        Zone(zones, "The Gloam Hollow", new Vector3(0f, -26.9f, 7.5f),
            "Moths: in Dream, hold right-click near them to absorb them. Crawlers charge along the line they show. Watchers can only be hurt in Nightmare.");
        Checkpoint(3, new Vector3(0f, -26f, 8f), 4.5f);
        Put("IslandLarge", c, 140f);
        Dress(c, 15f, 3, 3, 5, 6);
        for (int i = 0; i < 7; i++)
        {
            float a = 35f + i * 46f;
            if (Mathf.Abs(Mathf.DeltaAngle(a, 0f)) < 30f || Mathf.Abs(Mathf.DeltaAngle(a, 180f)) < 30f) continue;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Put("RockSpire", c + d * 13.6f + Vector3.up * R(0.8f, 2f), R(0f, 360f), R(1.2f, 2f));
        }
        Put("TreeC", c + new Vector3(-11f, 0f, -4f), 0f, 1.2f);
        Put("TreeA", c + new Vector3(11.5f, 0f, 5f), 120f);
        Put("LanternLit", c + new Vector3(-5f, 0f, -9.5f), 30f);
        Put("LanternLit", c + new Vector3(5f, 0f, 10.5f), 210f);

        var exit = Put("ThornGate", c + new Vector3(0f, 0f, 12.4f)).GetComponent<ThornGate>();
        Put("RootArch", c + new Vector3(0f, 0f, 13.2f), 0f, 1.1f);

        var gateObject = new GameObject("Combat Gate");
        gateObject.transform.SetParent(parent, false);
        gateObject.transform.position = c;
        var gate = gateObject.AddComponent<CombatGate>();
        gate.radius = 9.5f;
        gate.exit = exit;
        var keep = parent;
        var waves = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            waves[i] = new GameObject("Wave " + (i + 1));
            waves[i].transform.SetParent(gateObject.transform, false);
        }
        parent = waves[0].transform;
        Put("GlimmerMoth", c + new Vector3(-6f, 2.6f, 4f));
        Put("GlimmerMoth", c + new Vector3(6f, 2.8f, 5f));
        Put("GlimmerMoth", c + new Vector3(0f, 3.2f, 9f));
        parent = waves[1].transform;
        Put("BrambleCrawler", c + new Vector3(-7f, 0.75f, 3f), 120f);
        Put("BrambleCrawler", c + new Vector3(7f, 0.75f, -2f), -120f);
        Put("GloamWatcher", c + new Vector3(0f, 3.4f, 9.5f), 180f);
        parent = waves[2].transform;
        Put("GloamWatcher", c + new Vector3(-8.5f, 3.4f, 5f), 150f);
        Put("GlimmerMoth", c + new Vector3(7f, 3f, 6f));
        Put("GlimmerMoth", c + new Vector3(3f, 3.4f, -6f));
        Put("BrambleCrawler", c + new Vector3(5f, 0.75f, 7f), 200f);
        parent = keep;
        gate.waves = waves;

        Put("Motes", c + Vector3.up * 4f);
        Fog(c + new Vector3(-30f, 2f, 4f), 50f, 20f);
        Fog(c + new Vector3(30f, 2f, -2f), 50f, 20f);
    }

    // ---------- Zone 5: Rootclimb ----------

    static void BuildRootclimb(List<LevelFlow.Zone> zones)
    {
        Group("Zone 5 Rootclimb");
        var c = new Vector3(0f, 0f, 48f);
        const float ring = 9.2f;
        Zone(zones, "Rootclimb", new Vector3(0f, -25.5f, 38.6f),
            "Climb the roots. Blossoms throw you upward in Dream. In Nightmare the gloom rises from below: do not linger.");
        Checkpoint(4, new Vector3(0f, -25f, 38.6f), 3.5f);
        Put("RootPillar", new Vector3(c.x, -48f, c.z));

        // Thirteen steps, one and a half turns around the pillar. Angle 180 = south (the entry), 0 = north (the exit).
        string[] kinds = { "BarkLedge", "StoneStep", "PetalPlatform", "BarkLedge", "BounceBlossom", "BarkLedge", "ThornWalkway", "StoneStep",
            "BounceBlossom", "BarkLedge", "PetalPlatform", "ThornWalkway", "RootDeck" };
        float[] heights = { -25.6f, -24.5f, -23.4f, -22.3f, -21.4f, -15.9f, -14.8f, -13.7f, -12.8f, -7.3f, -6.2f, -5.1f, -4f };
        for (int i = 0; i < kinds.Length; i++)
        {
            float a = 180f - i * 45f;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Vector3 p = c + d * ring + Vector3.up * heights[i];
            switch (kinds[i])
            {
                case "RootDeck":
                    Put("RootDeck", new Vector3(0f, heights[i], c.z + ring + 0.3f + 4.5f), 0f);
                    break;
                case "BounceBlossom":
                    var blossom = Put("BounceBlossom", p, a, 1.35f).GetComponent<BounceBlossom>();
                    blossom.radius = 1.9f;
                    Put("StoneStep", p + Vector3.down * 0.9f, a, 0.75f);
                    break;
                case "PetalPlatform":
                    Put("PetalPlatform", p, a, 0.85f);
                    break;
                default:
                    Put(kinds[i], p, a - 90f, kinds[i] == "ThornWalkway" ? 0.8f : 1f);
                    if (kinds[i] == "BarkLedge" && i % 2 == 1) Put("Lantern", p + d * 1.2f, a + 180f, 0.9f);
                    if (kinds[i] == "BarkLedge" && i % 2 == 0) Put("Mushrooms", p + d * 1.1f, a, 0.8f);
                    break;
            }
        }
        Put("RisingGloom", new Vector3(c.x, -31f, c.z));
        Put("HangingVines", new Vector3(-9f, 2f, 44f), 20f, 1.4f);
        Put("HangingVines", new Vector3(9f, 3f, 52f), 110f, 1.4f);
        for (int i = 0; i < 3; i++) ShaftOfLight(new Vector3(R(-9f, 9f), 6f, c.z + R(-9f, 9f)), 34f, R(4f, 7f), 6f);
        Put("Motes", new Vector3(0f, -16f, c.z), Vector3.zero, new Vector3(1f, 2.4f, 1f));
        Fog(new Vector3(-28f, -18f, c.z), 50f, 30f);
        Fog(new Vector3(28f, -16f, c.z + 6f), 50f, 30f);
    }

    // ---------- Zone 6: The Sleeper's Approach ----------

    static void BuildApproach(List<LevelFlow.Zone> zones)
    {
        Group("Zone 6 Sleeper's Approach");
        var well = new Vector3(0f, -3.2f, 72f);
        Zone(zones, "The Sleeper's Approach", new Vector3(0f, -3.1f, 68f),
            "Rest at the well. The Sleeper waits beneath the great tree.");
        Checkpoint(5, new Vector3(0f, -2.5f, 69f), 5f);
        Put("IslandMedium", well, 60f);
        Put("HealingWell", well + new Vector3(-2.6f, 0f, 2f));
        Dress(well, 7f, 3, 2, 5, 1);
        Put("TreeA", well + new Vector3(4.6f, 0f, 2.5f), 250f, 0.9f);
        Put("LanternLit", well + new Vector3(2.5f, 0f, -4.5f), 300f);
        Put("RootDeck", new Vector3(0f, -2.2f, 86f), 0f);
        Put("RootDeck", new Vector3(0f, -1f, 98.5f), 0f);
        Put("Lantern", new Vector3(-1.8f, -2.2f, 84f), 80f);
        Put("Lantern", new Vector3(1.8f, -1f, 97f), 260f);
        // The reveal: an arch of roots frames the tree and the arena below it.
        Put("RootArchBig", new Vector3(0f, -6f, 101f), 0f, 0.9f);
        Put("HangingVines", new Vector3(0f, 11f, 101f), 90f, 1.1f);
        Fog(new Vector3(-30f, -4f, 88f), 50f, 20f);
        Fog(new Vector3(30f, -4f, 92f), 50f, 20f);
        Put("Motes", new Vector3(0f, 2f, 88f), Vector3.zero, new Vector3(1f, 1f, 1.6f));

        Zone(zones, "The Sleeper", new Vector3(0f, 0.15f, 108.5f),
            "Step into the clearing to wake the Sleeper.");
        Checkpoint(6, new Vector3(0f, 0.6f, 108.5f), 4f);
    }

    // Distant copies of kit pieces take the far-bark material: no fine veins, more fog.
    static void Far(GameObject g)
    {
        foreach (var r in g.GetComponentsInChildren<MeshRenderer>())
            if (r.sharedMaterial == WorldKit.Bark) r.sharedMaterial = WorldKit.Silhouette;
    }

    // ---------- Depth layers: side islands, far trees, the mist sea ----------

    static void BuildBackdrop()
    {
        Group("Backdrop");
        Put("MistSea", new Vector3(0f, -52f, 20f), Vector3.zero, new Vector3(1500f, 1f, 1500f));
        Put("MistSea", new Vector3(0f, -66f, 20f), Vector3.zero, new Vector3(1500f, 1f, 1500f));

        // Mid layer: small islands with a tree, off the path on both sides.
        for (int i = 0; i < 11; i++)
        {
            float z = -165f + i * 27f + R(-6f, 6f);
            float side = i % 2 == 0 ? -1f : 1f;
            // Rise with the route so the layer stays behind the play space.
            float y = Mathf.Lerp(-36f, -6f, Mathf.InverseLerp(-165f, 105f, z)) + R(-7f, 4f);
            var at = new Vector3(side * R(24f, 44f), y, z);
            if (side > 0f && z > -70f && z < 20f) at.x += 26f; // behind the cliff
            Put(i % 3 == 0 ? "IslandMedium" : "IslandSmall", at, R(0f, 360f), R(1f, 1.5f));
            Put(i % 3 == 0 ? "TreeB" : i % 3 == 1 ? "TreeA" : "TreeC", at, R(0f, 360f), R(0.9f, 1.3f));
            if (i % 2 == 0) Put("Blossom", at + new Vector3(2f, 0f, 1f), R(0f, 360f), 1.6f);
            Fog(at + new Vector3(side * 10f, -3f, 6f), 46f, 16f);
        }

        // Far layer: giant trunks rising out of the mist.
        for (int i = 0; i < 16; i++)
        {
            float z = -210f + i * 30f + R(-10f, 10f);
            float side = i % 2 == 0 ? -1f : 1f;
            var at = new Vector3(side * R(70f, 170f), R(-64f, -44f), z);
            Put(i % 3 == 0 ? "SilhouetteTreeB" : "SilhouetteTreeA", at, R(0f, 360f), R(1f, 1.9f));
        }
        // Behind the start, so turning around still shows a world.
        for (int i = 0; i < 5; i++)
            Put("SilhouetteTreeA", new Vector3(-120f + i * 60f + R(-15f, 15f), R(-64f, -48f), -230f - R(0f, 60f)), R(0f, 360f), R(1.2f, 2f));
        // Great roots arcing through the distance.
        Far(Put("RootArchBig", new Vector3(-95f, -50f, -40f), 70f, 4.5f));
        Far(Put("RootArchBig", new Vector3(110f, -56f, 60f), 110f, 5f));
        Far(Put("RootArchBig", new Vector3(-120f, -40f, 110f), 40f, 4f));
        for (int i = 0; i < 6; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Fog(new Vector3(side * R(60f, 95f), R(-34f, -8f), -170f + i * 52f), 150f, 46f, true);
        }
    }
}
