using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the combat prefabs into Assets/Resources/Combat (loaded at runtime by CombatAssets).
public static class CombatBuilder
{
    const string Out = "Assets/Resources/Combat";
    const string MatDir = "Assets/Materials/Combat";

    static void Ensure(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }

    static Color H(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }

    static Material Get(string name, string shaderName)
    {
        string p = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(m, p);
        }
        return m;
    }

    static Material Lit(string name, string hex, float emission)
    {
        var m = Get(name, "Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", H(hex));
        m.SetFloat("_Smoothness", 0.4f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        m.SetColor("_EmissionColor", H(hex) * emission);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static void Save(GameObject g, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(g, Out + "/" + name + ".prefab");
        Object.DestroyImmediate(g);
    }

    static TrailRenderer Trail(GameObject g, Material trailMat, float time, float width, Color c)
    {
        var tr = g.AddComponent<TrailRenderer>();
        tr.time = time;
        tr.widthCurve = AnimationCurve.Linear(0f, width, 1f, 0f);
        tr.minVertexDistance = 0.05f;
        tr.sharedMaterial = trailMat;
        tr.shadowCastingMode = ShadowCastingMode.Off;
        tr.receiveShadows = false;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = grad;
        return tr;
    }

    public static void Build()
    {
        Ensure("Assets/Resources"); Ensure(Out); Ensure("Assets/Materials"); Ensure(MatDir);

        var glow = Get("GlowParticle", "Lucid/GlowParticle");
        glow.SetFloat("_Trail", 0f);
        var trail = Get("GlowTrail", "Lucid/GlowParticle");
        trail.SetFloat("_Trail", 1f);
        var shieldMat = Get("Shield", "Lucid/ShieldFresnel");
        shieldMat.SetColor("_Color", H("#FFB3DC"));
        EditorUtility.SetDirty(glow); EditorUtility.SetDirty(trail); EditorUtility.SetDirty(shieldMat);

        // GlowBurst: one-shot particle burst. Colour and count are set at runtime.
        var burst = new GameObject("GlowBurst");
        var ps = burst.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.6f; main.loop = false; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var cg = new Gradient();
        cg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = cg;
        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        var pr = burst.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = glow;
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        Save(burst, "GlowBurst");

        // Shard: the player's Nightmare projectile.
        var shard = new GameObject("Shard");
        Prim(PrimitiveType.Sphere, "Core", shard.transform, Vector3.zero, new Vector3(0.1f, 0.1f, 0.9f), Lit("Combat_ShardCore", "#FFC24A", 5f));
        Trail(shard, trail, 0.25f, 0.16f, new Color(1f, 0.7f, 0.25f));
        shard.AddComponent<Projectile>();
        Save(shard, "Shard");

        // EnemyOrb: colour is set per world at runtime through a property block.
        var orb = new GameObject("EnemyOrb");
        Prim(PrimitiveType.Sphere, "Core", orb.transform, Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), Lit("Combat_OrbCore", "#FF73C8", 3.5f));
        Trail(orb, trail, 0.3f, 0.3f, new Color(1f, 0.45f, 0.78f));
        orb.AddComponent<EnemyBullet>();
        Save(orb, "EnemyOrb");

        // Wisp: glowing core, two slow rings, a light.
        var dreamMat = Lit("Combat_WispDream", "#FF6FC0", 0.9f);
        var nightMat = Lit("Combat_WispNight", "#FF3A18", 1.3f);
        var wisp = new GameObject("Wisp");
        var core = Prim(PrimitiveType.Sphere, "Core", wisp.transform, Vector3.zero, new Vector3(0.9f, 0.9f, 0.9f), dreamMat);
        var sw = core.AddComponent<WorldMaterialSwap>(); sw.dream = dreamMat; sw.nightmare = nightMat;
        var ringA = Prim(PrimitiveType.Cylinder, "RingA", wisp.transform, Vector3.zero, new Vector3(1.9f, 0.015f, 1.9f), dreamMat);
        ringA.AddComponent<Spin>().degreesPerSecond = new Vector3(40, 60, 0);
        var swa = ringA.AddComponent<WorldMaterialSwap>(); swa.dream = dreamMat; swa.nightmare = nightMat;
        var ringB = Prim(PrimitiveType.Cylinder, "RingB", wisp.transform, Vector3.zero, new Vector3(1.5f, 0.015f, 1.5f), dreamMat);
        ringB.AddComponent<Spin>().degreesPerSecond = new Vector3(-70, 0, 50);
        var swb = ringB.AddComponent<WorldMaterialSwap>(); swb.dream = dreamMat; swb.nightmare = nightMat;
        var lg = new GameObject("Glow");
        lg.transform.SetParent(wisp.transform, false);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.range = 9f; l.intensity = 2.5f; l.color = H("#FF8AD0");
        var we = wisp.AddComponent<WispEnemy>();
        we.health = 6f; we.radius = 0.95f;
        Save(wisp, "Wisp");

        // Shield: fresnel bubble. Scaled at runtime.
        var shield = new GameObject("Shield");
        Prim(PrimitiveType.Sphere, "Bubble", shield.transform, Vector3.zero, Vector3.one, shieldMat);
        Save(shield, "Shield");

        AssetDatabase.SaveAssets();
    }
}
