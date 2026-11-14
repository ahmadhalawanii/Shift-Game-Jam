using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static WorldMeshes;

// Flora, trees, props, landmarks and enemies of the world kit. See WorldKit.cs for materials and helpers.
public static partial class WorldKit
{
    static Material BudCore, MothHalo, EyeHalo, ThornLean;

    static void BuildExtraMaterials()
    {
        EyeHalo = Glow("EyeHalo", Color.black, Hazard * 0.7f, 1, 1).F("_ExistIn", 2f).F("_FogInfluence", 0.3f).F("_DepthFade", 1f);
        ThornLean = Surface("ThornLean", "#5A4B9A", "#1F1650", "#8A5AB5", "#2A1F4A", "#07050F", "#4A1F6B")
            .Emit(Color.black, Hazard * 1.8f, 1f, 0.3f).F("_MaskPower", 6f).F("_ExistIn", 2f).F("_GrowDream", 0.02f).F("_GrowNight", 1f).F("_GrowWidth", 0.7f)
            .F("_MorphSeconds", 0.7f).F("_LeanToPlayer", 0.9f).F("_WindHeight", 3f);
        BudCore = Glow("BudCore", Color.black, Hazard * 3.5f).F("_Fresnel", 0.6f).F("_Pulse", 0.5f).F("_PulseSpeed", 5f).F("_FogInfluence", 0.3f);
        MothHalo = Glow("MothHalo", Gold * 0.45f, Hazard * 0.6f, 1, 1).F("_FogInfluence", 0.4f).F("_DepthFade", 1f);
    }

    // ---------- Flora ----------

    static MeshData Shroom(float height, float radius, int seed)
    {
        float h = height, r = radius;
        var profile = new[]
        {
            new Vector2(0.14f * r, 0f), new Vector2(0.1f * r, 0.66f * h), new Vector2(0.85f * r, 0.64f * h),
            new Vector2(r, 0.72f * h), new Vector2(0.8f * r, 0.88f * h), new Vector2(0.35f * r, 0.98f * h), new Vector2(0f, h)
        };
        return Lathe(profile, 9, i => W(i < 2 ? 0.6f : 1f, i >= 3 ? 1f : i == 2 ? 0.6f : 0f), seed, 0.08f);
    }

    static void BuildFlora()
    {
        BuildExtraMaterials();

        // A hero blossom: open in Dream, folded into a dark thorn bud in Nightmare.
        {
            var head = PetalRing(6, 0.4f, 14f, 0f, WorldMeshes.Petal(1.4f, 1f, 0.5f, 0.12f, 5, W(0.7f), W(1f), 0.3f, 1f), 9);
            head.Append(PetalRing(5, 0.4f, 38f, 30f, WorldMeshes.Petal(0.9f, 0.7f, 0.6f, 0.15f, 4, W(0.8f), W(1f), 0.5f, 1f), 4));
            var heart = Lathe(new[] { new Vector2(0.06f, -0.3f), new Vector2(0.45f, -0.12f), new Vector2(0.52f, 0.05f), new Vector2(0.3f, 0.2f), new Vector2(0f, 0.25f) }, 8, i => W(1f, i >= 2 ? 1f : 0f));
            heart.Append(Tube(Curve(6, new Vector3(0f, -0.2f, 0f), new Vector3(0.18f, -0.9f, 0.1f), new Vector3(0f, -1.6f, 0f)), t => Mathf.Lerp(0.07f, 0.12f, t), 5, 1, 0.1f, 1f, t => W(0.55f)));
            var root = new GameObject("Blossom");
            var h = Part(root.transform, "Petals", SaveMesh(head, "Blossom_Petals"), Petal).At(new Vector3(0f, 1.6f, 0f));
            Part(h.transform, "Heart", SaveMesh(heart, "Blossom_Heart"), Bark);
            Detail(root);
            SavePrefab(root);
        }

        // Glowing mushrooms.
        {
            var m = new MeshData();
            var rng = new System.Random(3);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.1f + Rnd(rng, -0.3f, 0.3f), d = i == 0 ? 0f : Rnd(rng, 0.5f, 1.3f);
                m.Append(Shroom(Rnd(rng, 0.4f, 1.5f), Rnd(rng, 0.25f, 0.6f), i), new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d),
                    Quaternion.Euler(Rnd(rng, -8f, 8f), Rnd(rng, 0f, 360f), Rnd(rng, -8f, 8f)), 1f);
            }
            var root = new GameObject("Mushrooms");
            Part(root.transform, "Caps", SaveMesh(m, "Mushrooms_Mesh"), Mushroom);
            Detail(root);
            SavePrefab(root);
        }

        // Small flowers that wilt in Nightmare.
        {
            var m = new MeshData();
            var rng = new System.Random(8);
            var bloom = PetalRing(5, 0.03f, 30f, 0f, WorldMeshes.Petal(0.24f, 0.18f, 0.5f, 0.1f, 3, W(0.8f), W(1f), 0f, 1f), 1);
            for (int i = 0; i < 11; i++)
            {
                float a = Rnd(rng, 0f, 6.28f), d = Mathf.Sqrt(Rnd(rng, 0f, 1f)) * 1.4f, h = Rnd(rng, 0.35f, 0.85f);
                var at = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                var lean = new Vector3(Rnd(rng, -0.15f, 0.15f), 0f, Rnd(rng, -0.15f, 0.15f));
                m.Append(Tube(new List<Vector3> { at, at + Vector3.up * h * 0.5f + lean * 0.4f, at + Vector3.up * h + lean }, t => 0.018f, 3, i, 0f, 1f, t => W(0.5f), 1f, false, false));
                m.Append(bloom, at + Vector3.up * h + lean, Quaternion.Euler(Rnd(rng, -20f, 20f), Rnd(rng, 0f, 360f), Rnd(rng, -20f, 20f)), Rnd(rng, 0.8f, 1.5f));
            }
            var root = new GameObject("FlowerPatch");
            Part(root.transform, "Flowers", SaveMesh(m, "FlowerPatch_Mesh"), Flowers);
            Detail(root);
            SavePrefab(root);
        }

        // Thorns that erupt in Nightmare and lean toward the player.
        {
            var m = new MeshData();
            var rng = new System.Random(12);
            for (int i = 0; i < 8; i++)
                m.Append(WorldMeshes.Thorn(Rnd(rng, 1f, 2.8f), Rnd(rng, 0.12f, 0.26f), Rnd(rng, 0.1f, 0.4f), 6, 5, W(0.8f), W(1f), 1f),
                    new Vector3(Rnd(rng, -0.7f, 0.7f), -0.2f, Rnd(rng, -0.7f, 0.7f)), Quaternion.Euler(0f, Rnd(rng, 0f, 360f), 0f) * Quaternion.Euler(0f, 0f, -Rnd(rng, 8f, 50f)), 1f);
            var root = new GameObject("ThornCluster");
            Part(root.transform, "Thorns", SaveMesh(m, "ThornCluster_Mesh"), ThornLean);
            Detail(root);
            SavePrefab(root);
        }
    }

    // ---------- Trees and roots ----------

    static void Tree(string name, float h, int seed, int branches)
    {
        var rng = new System.Random(seed);
        var lean = new Vector3(Rnd(rng, -1f, 1f), 0f, Rnd(rng, -1f, 1f)) * h * 0.1f;
        var trunk = Curve(12, Vector3.zero, new Vector3(lean.x * 0.2f, h * 0.3f, lean.z * 0.2f), new Vector3(lean.x, h * 0.65f, lean.z), new Vector3(lean.x * 1.5f, h, lean.z * 1.5f));
        float r0 = h * 0.055f;
        var wood = Tube(trunk, t => Mathf.Lerp(r0, r0 * 0.3f, t) + r0 * 1.2f * Mathf.Pow(Mathf.Max(0f, 1f - t / 0.14f), 2f), 9, seed, 0.25f, 0.35f,
            t => W(0.9f - 0.2f * t), 1f, false, true, Veins(3, 26f));
        var tips = new List<Vector3> { trunk[trunk.Count - 1] };
        for (int i = 0; i < branches; i++)
        {
            Vector3 start = trunk[Mathf.Clamp((int)(Rnd(rng, 0.45f, 0.9f) * 11f), 0, 11)];
            float yaw = i * 6.28f / branches + Rnd(rng, -0.5f, 0.5f), len = h * Rnd(rng, 0.22f, 0.4f);
            var dir = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            Vector3 end = start + dir * len + Vector3.up * len * Rnd(rng, 0.3f, 0.7f);
            wood.Append(Tube(Curve(7, start, start + dir * len * 0.5f + Vector3.up * len * 0.12f, end), t => Mathf.Lerp(r0 * 0.38f, r0 * 0.08f, t), 6, seed + i + 1, 0.2f, 0.5f,
                t => W(0.8f), 1f, false, true, Veins(3, 20f, 0.7f)));
            tips.Add(end);
        }
        var canopy = new MeshData();
        var roots = new List<Vector3>();
        for (int k = 0; k < tips.Count; k++)
        {
            float r = h * Rnd(rng, 0.13f, 0.2f);
            canopy.Append(Blob(6, 9, new Vector3(r, r * 0.62f, r), seed + k, 0.35f, 1.3f, null, (p, d) => W(0.75f + 0.25f * d.y)), tips[k] + Vector3.up * r * 0.2f, Quaternion.identity, 1f);
            for (int s = 0; s < 6; s++)
                roots.Add(tips[k] + new Vector3(Rnd(rng, -1f, 1f) * r * 0.8f, -r * 0.35f, Rnd(rng, -1f, 1f) * r * 0.8f));
        }
        var strands = Strands(roots, i => Rnd(rng, 1.2f, h * 0.24f), 0.07f, seed);
        var root = new GameObject(name);
        Part(root.transform, "Wood", SaveMesh(wood, name + "_Wood"), Bark, true);
        Part(root.transform, "Canopy", SaveMesh(canopy, name + "_Canopy"), Canopy);
        Part(root.transform, "Strands", SaveMesh(strands, name + "_Strands", false, true), Strand);
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.radius = r0 * 1.1f;
        capsule.height = h * 0.5f;
        capsule.center = new Vector3(0f, h * 0.25f, 0f);
        SavePrefab(root);
    }

    // Huge, simple shapes for the far layers. Fog does the rest.
    static void SilhouetteTree(string name, float h, int seed)
    {
        var rng = new System.Random(seed);
        var lean = new Vector3(Rnd(rng, -1f, 1f), 0f, Rnd(rng, -1f, 1f)) * h * 0.14f;
        var trunk = Curve(10, new Vector3(0f, -h * 0.5f, 0f), new Vector3(lean.x * 0.3f, h * 0.2f, lean.z * 0.3f), new Vector3(lean.x, h * 0.6f, lean.z), new Vector3(lean.x * 0.6f, h, lean.z * 0.6f));
        float r0 = h * 0.07f;
        var wood = Tube(trunk, t => Mathf.Lerp(r0, r0 * 0.25f, t), 7, seed, 0.3f, 0.1f, t => W(0.9f));
        var crown = new MeshData();
        for (int i = 0; i < 4; i++)
        {
            Vector3 start = trunk[5 + i];
            float yaw = i * 1.7f + Rnd(rng, 0f, 1f), len = h * Rnd(rng, 0.2f, 0.4f);
            var dir = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            Vector3 end = start + dir * len + Vector3.up * len * Rnd(rng, 0.1f, 0.5f);
            wood.Append(Tube(Curve(6, start, start + dir * len * 0.5f + Vector3.up * len * 0.3f, end), t => Mathf.Lerp(r0 * 0.4f, r0 * 0.06f, t), 5, seed + i, 0.2f, 0.2f, t => W(0.85f)));
            float r = h * Rnd(rng, 0.14f, 0.22f);
            crown.Append(Blob(5, 8, new Vector3(r, r * 0.45f, r), seed + i, 0.4f, 1.2f, null, (p, d) => W(0.8f + 0.2f * d.y)), end, Quaternion.identity, 1f);
        }
        var root = new GameObject(name);
        Part(root.transform, "Wood", SaveMesh(wood, name + "_Wood"), Silhouette);
        Part(root.transform, "Crown", SaveMesh(crown, name + "_Crown"), CanopyFar);
        SavePrefab(root);
    }

    static void RootArch(string name, float span, float height, float radius, int seed)
    {
        var rng = new System.Random(seed);
        var m = new MeshData();
        for (int k = 0; k < 2; k++)
        {
            float s = k == 0 ? 1f : 0.55f, off = k == 0 ? 0f : radius * 1.6f;
            var path = Curve(16, new Vector3(-span * 0.5f, -radius * 2f, off), new Vector3(-span * 0.36f, height * 0.78f, off + Rnd(rng, -1f, 1f)),
                new Vector3(Rnd(rng, -1f, 1f), height * (k == 0 ? 1f : 0.85f), -off * 0.5f), new Vector3(span * 0.36f, height * 0.74f, Rnd(rng, -1f, 1f) - off), new Vector3(span * 0.5f, -radius * 2f, -off));
            m.Append(Tube(path, t => radius * s * (0.75f + 0.5f * Mathf.Abs(t - 0.5f) * 2f), 9, seed + k, 0.28f, 0.3f, t => W(0.85f), 1f, false, false, Veins(3, 30f)));
        }
        var root = new GameObject(name);
        Part(root.transform, "Root", SaveMesh(m, name + "_Mesh"), Bark, true);
        SavePrefab(root);
    }

    static void BuildTrees()
    {
        Tree("TreeA", 15f, 21, 4);
        Tree("TreeB", 23f, 22, 5);
        Tree("TreeC", 10f, 23, 3);
        SilhouetteTree("SilhouetteTreeA", 60f, 31);
        SilhouetteTree("SilhouetteTreeB", 85f, 32);
        RootArch("RootArch", 16f, 9f, 0.9f, 41);
        RootArch("RootArchBig", 34f, 20f, 2.2f, 42);

        // Curtain of glowing vines, hanging from the origin along local Z. They retract in Nightmare.
        {
            var rng = new System.Random(15);
            var roots = new List<Vector3>();
            for (int i = 0; i < 46; i++) roots.Add(new Vector3(Rnd(rng, -0.8f, 0.8f), 0f, Rnd(rng, -6f, 6f)));
            var root = new GameObject("HangingVines");
            Part(root.transform, "Strands", SaveMesh(Strands(roots, i => Rnd(rng, 4f, 15f), 0.09f, 3, 6, 0.12f), "HangingVines_Mesh", false, true), Strand);
            SavePrefab(root);
        }

        // The pillar of roots the Rootclimb winds around. 46 m tall, origin at its foot.
        {
            var rng = new System.Random(19);
            var m = Tube(Curve(14, new Vector3(0f, -6f, 0f), new Vector3(0.8f, 10f, 0.5f), new Vector3(-0.8f, 24f, -0.4f), new Vector3(0.5f, 36f, 0.6f), new Vector3(0f, 46f, 0f)),
                t => 4.6f + 1.6f * Mathf.Pow(1f - t, 3f) + 0.5f * Mathf.Sin(t * 9f), 14, 2, 0.22f, 0.12f, t => W(0.85f), 1f, false, true, Veins(2, 40f));
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f, y0 = Rnd(rng, 0f, 30f);
                var pts = new List<Vector3>();
                for (int k = 0; k <= 6; k++)
                {
                    float ang = a + k * 0.55f, r = 5.2f + Rnd(rng, -0.3f, 0.5f);
                    pts.Add(new Vector3(Mathf.Cos(ang) * r, y0 + k * 2.6f, Mathf.Sin(ang) * r));
                }
                m.Append(Tube(Curve(22, pts.ToArray()), t => 0.75f - 0.4f * t, 7, 30 + i, 0.2f, 0.3f, t => W(0.75f), 1f, true, true, Veins(2, 30f, 0.8f)));
            }
            var root = new GameObject("RootPillar");
            Part(root.transform, "Roots", SaveMesh(m, "RootPillar_Mesh"), Bark, true);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 5f; capsule.height = 56f; capsule.center = new Vector3(0f, 20f, 0f);
            SavePrefab(root);
        }

        // The rising gloom of the Rootclimb: a pool of corruption that climbs in Nightmare.
        {
            var gloom = Glow("Gloom", Color.black, Corrupt * 1.2f + Hazard * 0.55f, 1, 0).F("_ExistIn", 2f).F("_Pulse", 0.3f).F("_FogInfluence", 0f);
            var root = new GameObject("RisingGloom");
            Part(root.transform, "Pool", flatQuad, gloom).At(new Vector3(0f, 1.4f, 0f)).Scaled(new Vector3(46f, 1f, 46f));
            Part(root.transform, "PoolLow", flatQuad, gloom).At(new Vector3(0f, 0.2f, 0f)).Scaled(new Vector3(60f, 1f, 60f));
            var hazard = root.AddComponent<ThornHazard>();
            hazard.activeIn = ExistsIn.Nightmare;
            hazard.size = new Vector3(30f, 3f, 30f);
            hazard.damage = 0.08f;
            hazard.riseSpeed = 1.1f;
            hazard.sinkSpeed = 7f;
            hazard.maxRise = 21f;
            hazard.sparks = Violet;
            SavePrefab(root);
        }
    }

    // ---------- Props ----------

    static GameObject Motes(string name, Vector3 box, int count, float lifetime, float size, float rise)
    {
        var go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.4f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.6f, 0.9f, 1f));
        main.maxParticles = count;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -rise;
        var emission = ps.emission;
        emission.rateOverTime = count / lifetime;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = box;
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.2f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mote;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    static LineRenderer Line(Transform parent, string name, Color color, float width)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        var l = g.AddComponent<LineRenderer>();
        l.sharedMaterial = GlowLine;
        l.positionCount = 2;
        l.useWorldSpace = true;
        l.widthMultiplier = width;
        l.startColor = color;
        l.endColor = new Color(color.r, color.g, color.b, 0f);
        l.shadowCastingMode = ShadowCastingMode.Off;
        l.receiveShadows = false;
        l.enabled = false;
        return l;
    }

    static Light PointLight(Transform parent, Vector3 at, Color dream, float dreamIntensity, Color night, float nightIntensity, float range)
    {
        var g = new GameObject("Light");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = at;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.color = dream;
        l.intensity = dreamIntensity;
        l.shadows = LightShadows.None;
        var w = g.AddComponent<WorldLight>();
        w.dream = dream; w.nightmare = night; w.dreamIntensity = dreamIntensity; w.nightmareIntensity = nightIntensity;
        return l;
    }

    static void BuildProps()
    {
        // Thorn gate: a wall of briar across the path.
        {
            var m = new MeshData();
            var rng = new System.Random(17);
            for (int i = 0; i < 11; i++)
            {
                float x0 = -5f + i, x1 = x0 + Rnd(rng, -2.5f, 2.5f), z0 = Rnd(rng, -0.5f, 0.5f);
                var path = Curve(9, new Vector3(x0, -0.3f, z0), new Vector3((x0 + x1) * 0.5f + Rnd(rng, -0.6f, 0.6f), 2.7f, -z0), new Vector3(x1, 5.2f + Rnd(rng, -0.8f, 1.2f), z0 * 0.5f));
                m.Append(Tube(path, t => Mathf.Lerp(0.32f, 0.07f, t), 6, i, 0.25f, 1f, t => W(0.85f)));
                for (int k = 1; k < 8; k++)
                    m.Append(WorldMeshes.Thorn(Rnd(rng, 0.4f, 0.95f), 0.09f, 0.2f, 5, 3, W(0.8f), W(1f), 1f), path[k],
                        Quaternion.Euler(Rnd(rng, -75f, 75f), Rnd(rng, 0f, 360f), Rnd(rng, -75f, 75f)), 1f);
            }
            var root = new GameObject("ThornGate");
            var visual = Part(root.transform, "Thorns", SaveMesh(m, "ThornGate_Mesh"), ThornSolid, true);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 3.2f, 0f);
            box.size = new Vector3(11f, 7f, 1.8f);
            var gate = root.AddComponent<ThornGate>();
            gate.visual = visual.transform;
            gate.solids = new Collider[] { box };
            SavePrefab(root);
        }

        // Corrupted bud: the heart of a gate. Pivot at its centre so shots and aim assist find it.
        {
            var m = PetalRing(6, 0.25f, 52f, 0f, WorldMeshes.Petal(1.15f, 0.85f, 0.9f, 0.2f, 5, W(0.75f), W(1f), 0.4f, 1f), 2);
            m.Append(Tube(Curve(6, new Vector3(0f, -0.1f, 0f), new Vector3(0.2f, -0.8f, 0f), new Vector3(-0.1f, -1.5f, 0.1f)), t => Mathf.Lerp(0.2f, 0.35f, t), 6, 3, 0.2f, 1f, t => W(0.6f)));
            var root = new GameObject("CorruptBud");
            var bud = root.AddComponent<CorruptBud>();
            bud.health = 6f; bud.radius = 1.1f; bud.deathColor = Hazard;
            var visual = Part(root.transform, "Visual", SaveMesh(m, "CorruptBud_Mesh"), Bud);
            Part(visual.transform, "Core", sphere, BudCore).At(new Vector3(0f, 0.35f, 0f), default(Vector3), 0.7f);
            bud.visual = visual.transform;
            SavePrefab(root);
        }

        // Healing well.
        {
            var basin = Lathe(new[] { new Vector2(2f, -0.4f), new Vector2(2.05f, 0.25f), new Vector2(1.8f, 0.7f), new Vector2(1.85f, 0.92f), new Vector2(1.5f, 0.92f), new Vector2(1.4f, 0.45f), new Vector2(0f, 0.4f) },
                18, i => W(i >= 5 ? 0.6f : 0.95f, i == 3 || i == 4 ? 1f : 0f), 4, 0.05f);
            var root = new GameObject("HealingWell");
            Part(root.transform, "Basin", SaveMesh(basin, "HealingWell_Basin"), Ground, true);
            Part(root.transform, "Water", flatQuad, WellWater).At(new Vector3(0f, 0.66f, 0f)).Scaled(new Vector3(3.6f, 1f, 3.6f));
            Part(root.transform, "Halo", quad, HaloCyan).At(new Vector3(0f, 1.6f, 0f), default(Vector3), 7f);
            PointLight(root.transform, new Vector3(0f, 1.8f, 0f), Light, 5f, Moon, 3f, 13f);
            var motes = Motes("Motes", new Vector3(3f, 0.2f, 3f), 40, 3.5f, 0.16f, 0.12f);
            motes.transform.SetParent(root.transform, false);
            motes.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 1.95f; capsule.height = 1.9f; capsule.center = new Vector3(0f, 0f, 0f);
            root.AddComponent<HealingWell>();
            SavePrefab(root);
        }

        // Bounce blossom.
        {
            var petals = PetalRing(8, 0.4f, 20f, 0f, WorldMeshes.Petal(1.5f, 1.05f, 0.45f, 0.15f, 5, W(0.75f), W(1f), 0.4f, 1f), 6);
            var pad = Lathe(new[] { new Vector2(0.12f, -0.7f), new Vector2(0.6f, -0.28f), new Vector2(0.88f, 0f), new Vector2(0.6f, 0.14f), new Vector2(0f, 0.2f) }, 12, i => W(1f, i >= 2 ? 1f : 0f));
            pad.Append(Tube(Curve(8, new Vector3(0f, -0.6f, 0f), new Vector3(0.4f, -2f, 0.2f), new Vector3(-0.2f, -3.6f, -0.3f), new Vector3(0.1f, -5.5f, 0f)), t => Mathf.Lerp(0.16f, 0.05f, t), 6, 5, 0.1f, 1f, t => W(0.6f)));
            var root = new GameObject("BounceBlossom");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            Part(visual.transform, "Petals", SaveMesh(petals, "BounceBlossom_Petals"), BouncePetal);
            Part(visual.transform, "Pad", SaveMesh(pad, "BounceBlossom_Pad"), Bark);
            Part(visual.transform, "Halo", quad, Halo).At(new Vector3(0f, 0.5f, 0f), default(Vector3), 3.5f);
            var collider = root.AddComponent<MeshCollider>();
            collider.sharedMesh = SaveMesh(Puck(0.95f, 0.6f, 10), "BounceBlossom_Collider", false);
            collider.convex = true;
            var bounce = root.AddComponent<BounceBlossom>();
            bounce.visual = visual.transform;
            SavePrefab(root);
        }

        // Lantern flower: the breadcrumb of the route. The lit version carries a real light.
        foreach (bool lit in new[] { false, true })
        {
            var stalk = Tube(Curve(8, Vector3.zero, new Vector3(0.15f, 1.5f, 0f), new Vector3(0.65f, 2.75f, 0f), new Vector3(1.05f, 2.7f, 0f), new Vector3(1.1f, 2.45f, 0f)),
                t => Mathf.Lerp(0.1f, 0.035f, t), 5, 9, 0.1f, 1f, t => W(0.6f));
            var root = new GameObject(lit ? "LanternLit" : "Lantern");
            Part(root.transform, "Stalk", SaveMesh(stalk, "Lantern_Stalk"), Bark);
            Part(root.transform, "Bulb", sphere, LanternGlow).At(new Vector3(1.1f, 2.2f, 0f)).Scaled(new Vector3(0.42f, 0.55f, 0.42f));
            Part(root.transform, "Halo", quad, Halo).At(new Vector3(1.1f, 2.2f, 0f), default(Vector3), 3f);
            if (lit) PointLight(root.transform, new Vector3(1.1f, 2.2f, 0f), Gold, 3.5f, Moon, 1.6f, 11f);
            Detail(root, 0.03f, "Halo");
            SavePrefab(root);
        }

        // Rocks.
        {
            var a = new GameObject("RockA");
            Part(a.transform, "Rock", SaveMesh(Blob(7, 10, new Vector3(1f, 0.7f, 1.25f), 51, 0.35f, 1.4f, null, (p, d) => W(0.7f + 0.3f * d.y)), "RockA_Mesh"), Rock, true);
            SavePrefab(a);
            var b = new GameObject("RockSpire");
            Part(b.transform, "Rock", SaveMesh(Blob(9, 9, new Vector3(0.9f, 2.6f, 0.9f), 52, 0.3f, 1.1f, p => { float k = Mathf.Clamp01((p.y + 2.6f) / 5.2f); p.x *= 1.3f - k; p.z *= 1.3f - k; return p; }, (p, d) => W(0.65f + 0.2f * d.y)), "RockSpire_Mesh"), Rock, true);
            SavePrefab(b);
        }

        // Mist, light shafts and motes.
        {
            var card = new GameObject("FogCard");
            Part(card.transform, "Card", quad, Mist);
            SavePrefab(card);
            var wall = new GameObject("FogWall");
            Part(wall.transform, "Card", quad, MistWall);
            SavePrefab(wall);
            var sea = new GameObject("MistSea");
            Part(sea.transform, "Plane", flatQuad, MistSea);
            SavePrefab(sea);
            var beam = new GameObject("LightShaft");
            Part(beam.transform, "Shaft", shaft, Shaft);
            SavePrefab(beam);
            var motes = Motes("Motes", new Vector3(26f, 9f, 26f), 130, 9f, 0.16f, 0.015f);
            SavePrefab(motes);
        }
    }

    // ---------- Landmarks ----------

    static void BuildLandmarks()
    {
        BuildSpiritTree();
        BuildSkull();
        BuildLurker();
        BuildArenaWall();
    }

    // The destination: a tree of light, visible from every zone. In Nightmare its crown is gone and it is veined with corruption.
    static void BuildSpiritTree()
    {
        var rng = new System.Random(77);
        var trunk = Curve(20, new Vector3(0f, -20f, 0f), new Vector3(1f, 8f, 0f), new Vector3(2.5f, 30f, 1f), new Vector3(-3f, 55f, -2f), new Vector3(1f, 80f, 0f), new Vector3(0f, 104f, 2f));
        var wood = Tube(trunk, t => Mathf.Lerp(10.5f, 3f, Mathf.Pow(t, 0.8f)) + 7f * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(t - 0.16f) / 0.16f), 2f), 16, 5, 0.22f, 0.06f,
            t => W(0.9f - 0.15f * t), 1f, false, true, Veins(2, 46f));
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f + Rnd(rng, -0.2f, 0.2f);
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var path = Curve(9, d * 6f + Vector3.up * 9f, d * 16f + Vector3.up * Rnd(rng, 2f, 7f), d * 27f + Vector3.up * Rnd(rng, -1f, 3f) + Vector3.Cross(Vector3.up, d) * Rnd(rng, -6f, 6f), d * 40f + Vector3.down * 8f);
            wood.Append(Tube(path, t => Mathf.Lerp(3.6f, 0.7f, t), 8, 20 + i, 0.25f, 0.12f, t => W(0.8f), 1f, false, true, Veins(2, 24f, 0.8f)));
        }
        var tips = new List<Vector3>();
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f + Rnd(rng, -0.3f, 0.3f), len = Rnd(rng, 30f, 46f);
            Vector3 start = trunk[12 + i % 7];
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 end = start + d * len + Vector3.up * len * Rnd(rng, 0.25f, 0.6f);
            wood.Append(Tube(Curve(9, start, start + d * len * 0.45f + Vector3.up * len * 0.1f, end), t => Mathf.Lerp(2.6f, 0.3f, t), 8, 40 + i, 0.2f, 0.12f,
                t => W(0.85f), 1f, false, true, Veins(2, 30f)));
            tips.Add(end);
        }
        tips.Add(trunk[trunk.Count - 1]);
        var crown = new MeshData();
        var roots = new List<Vector3>();
        foreach (var tip in tips)
        {
            float r = Rnd(rng, 13f, 19f);
            crown.Append(Blob(7, 11, new Vector3(r, r * 0.55f, r), (int)tip.x + 100, 0.35f, 1.2f, null, (p, d) => W(0.7f + 0.3f * d.y)), tip + Vector3.up * 3f, Quaternion.identity, 1f);
            for (int s = 0; s < 9; s++) roots.Add(tip + new Vector3(Rnd(rng, -1f, 1f) * r * 0.85f, -r * 0.3f, Rnd(rng, -1f, 1f) * r * 0.85f));
        }
        var strands = Strands(roots, i => Rnd(rng, 14f, 42f), 0.55f, 9, 6, 0.2f);
        var root = new GameObject("SpiritTree");
        Part(root.transform, "Wood", SaveMesh(wood, "SpiritTree_Wood"), BarkFar);
        Part(root.transform, "Crown", SaveMesh(crown, "SpiritTree_Crown"), SpiritCanopy);
        Part(root.transform, "Strands", SaveMesh(strands, "SpiritTree_Strands", false, true), StrandBig);
        Part(root.transform, "Halo", quad, TreeHalo).At(new Vector3(0f, 88f, 0f), default(Vector3), 210f);
        Part(root.transform, "HaloLow", quad, TreeHalo).At(new Vector3(0f, 34f, 0f), default(Vector3), 90f);
        Part(root.transform, "Heart", sphere, TreeHeart).At(new Vector3(0f, 34f, -9.5f)).Scaled(new Vector3(5f, 9f, 3f));
        SavePrefab(root);
    }

    // The skull canopy that hangs over the arena in Nightmare. Faces local -Z.
    static void BuildSkull()
    {
        Vector3 eyeL = new Vector3(-3.3f, 1.4f, -6.4f), eyeR = new Vector3(3.3f, 1.4f, -6.4f), nose = new Vector3(0f, -0.3f, -10.2f);
        Func<Vector3, Vector3> shape = p =>
        {
            float t = Mathf.Clamp01(-p.z / 11f);
            p.x *= Mathf.Lerp(1f, 0.5f, t * t);
            p.y *= Mathf.Lerp(1f, 0.62f, t);
            if (p.y < -2.5f) p.y = -2.5f + (p.y + 2.5f) * 0.25f;
            float e = Mathf.Min((p - eyeL).magnitude, (p - eyeR).magnitude);
            p -= p.normalized * Mathf.Max(0f, 3.3f - e) * 1.1f;
            return p;
        };
        var m = Blob(16, 24, new Vector3(8f, 7f, 11f), 3, 0.1f, 1.1f, shape, (p, d) =>
        {
            float e = Mathf.Min((p - eyeL).magnitude, (p - eyeR).magnitude);
            float open = Mathf.SmoothStep(2f, 3.7f, e) * Mathf.SmoothStep(0.9f, 2.1f, (p - nose).magnitude);
            return W(Mathf.Lerp(0.03f, 0.75f + 0.25f * d.y, open));
        });
        for (int side = -1; side <= 1; side += 2)
        {
            var horn = WorldMeshes.Thorn(19f, 2.1f, 0.5f, 8, 8, W(0.9f), W(0.55f));
            m.Append(horn, Matrix4x4.TRS(new Vector3(side * 5.2f, 3.8f, 3.5f), Quaternion.Euler(-38f, 0f, -side * 42f), new Vector3(side, 1f, 1f)));
        }
        for (int i = 0; i < 11; i++)
        {
            float ang = Mathf.Lerp(-75f, 75f, i / 10f) * Mathf.Deg2Rad;
            var at = new Vector3(Mathf.Sin(ang) * 3.5f, -2.3f, -8.3f - Mathf.Cos(ang) * 2.3f);
            m.Append(WorldMeshes.Thorn(i % 5 == 0 ? 3.4f : 2.1f, 0.5f, 0.1f, 6, 3, W(0.95f), W(0.8f)), at, Quaternion.Euler(180f, i * 40f, 0f), 1f);
        }
        var jaw = Blob(8, 14, new Vector3(4.3f, 1.3f, 7.6f), 6, 0.12f, 1.3f, p => { float t = Mathf.Clamp01(-p.z / 7.6f); p.x *= Mathf.Lerp(1f, 0.6f, t); return p; }, (p, d) => W(0.6f + 0.25f * d.y));
        for (int i = 0; i < 7; i++)
        {
            float ang = Mathf.Lerp(-60f, 60f, i / 6f) * Mathf.Deg2Rad;
            jaw.Append(WorldMeshes.Thorn(1.8f, 0.42f, 0.1f, 6, 3, W(0.95f), W(0.8f)), new Vector3(Mathf.Sin(ang) * 2.4f, 0.9f, -5.2f - Mathf.Cos(ang) * 1.9f), Quaternion.Euler(0f, i * 50f, 0f), 1f);
        }
        m.Append(jaw, new Vector3(0f, -6.6f, -1.5f), Quaternion.Euler(-17f, 0f, 0f), 1f);
        var root = new GameObject("Skull");
        Part(root.transform, "Bone", SaveMesh(m, "Skull_Mesh"), Bone);
        Part(root.transform, "EyeL", sphere, EyeViolet).At(eyeL + new Vector3(0.4f, 0f, 1.6f), default(Vector3), 3.3f);
        Part(root.transform, "EyeR", sphere, EyeViolet).At(eyeR + new Vector3(-0.4f, 0f, 1.6f), default(Vector3), 3.3f);
        SavePrefab(root);
    }

    // Something vast in the Nightmare mist. Only ever a silhouette. Faces local -Z.
    static void BuildLurker()
    {
        var m = Blob(8, 12, new Vector3(5f, 3.4f, 6.5f), 9, 0.15f, 1.2f);
        m.Append(Blob(8, 12, new Vector3(7.5f, 6f, 9.5f), 10, 0.18f, 1.1f), new Vector3(0f, 2.5f, 12f), Quaternion.identity, 1f);
        m.Tint((p, c) => W(0.9f));
        var rng = new System.Random(4);
        for (int side = -1; side <= 1; side += 2)
            for (int k = 0; k < 4; k++)
            {
                var dir = Quaternion.Euler(0f, side * (38f + k * 32f), 0f) * Vector3.back;
                Vector3 hip = dir * 3.5f, knee = hip + dir * Rnd(rng, 9f, 13f) + Vector3.up * Rnd(rng, 9f, 14f), foot = hip + dir * Rnd(rng, 20f, 27f) + Vector3.down * 22f;
                m.Append(Tube(Curve(10, hip, knee, foot), t => Mathf.Lerp(1f, 0.14f, t), 6, k, 0.15f, 0.2f, t => W(0.9f)));
            }
        var root = new GameObject("Lurker");
        Part(root.transform, "Body", SaveMesh(m, "Lurker_Mesh"), Lurker);
        for (int i = 0; i < 6; i++)
            Part(root.transform, "Eye" + i, sphere, EyeViolet).At(new Vector3((i % 3 - 1) * 1.3f, 0.6f + (i / 3) * 1.1f, -6.1f + Mathf.Abs(i % 3 - 1) * 0.5f), default(Vector3), i < 3 ? 0.7f : 0.45f);
        SavePrefab(root);
    }

    // Roots that ring the boss arena, with a gap at the entrance (local -Z).
    static void BuildArenaWall()
    {
        var m = new MeshData();
        var rng = new System.Random(88);
        const int count = 20;
        Vector3 previous = Vector3.zero;
        bool hasPrevious = false;
        for (int i = 0; i < count; i++)
        {
            float deg = i * 360f / count;
            if (Mathf.Abs(Mathf.DeltaAngle(deg, 180f)) < 24f) { hasPrevious = false; continue; }
            var dir = Quaternion.Euler(0f, deg, 0f) * Vector3.forward;
            var tangent = Vector3.Cross(Vector3.up, dir);
            Vector3 foot = dir * (33f + Rnd(rng, -1f, 2.5f));
            float h = Rnd(rng, 8f, 17f);
            var path = Curve(10, foot + Vector3.down * 4f, foot + Vector3.up * h * 0.5f + dir * Rnd(rng, -1f, 2f) + tangent * Rnd(rng, -3f, 3f),
                foot + Vector3.up * h - dir * Rnd(rng, 1f, 6f) + tangent * Rnd(rng, -5f, 5f));
            m.Append(Tube(path, t => Mathf.Lerp(2.3f, 0.3f, t), 8, i, 0.3f, 0.25f, t => W(0.85f - 0.2f * t), 1f, false, true, Veins(4, 14f)));
            if (hasPrevious)
                m.Append(Tube(Curve(8, previous + Vector3.up * 0.5f, (previous + foot) * 0.5f + Vector3.up * Rnd(rng, 2f, 4.5f) + dir * 1.5f, foot + Vector3.up * 0.3f), t => 1.1f - 0.4f * Mathf.Sin(t * Mathf.PI), 7, i + 50, 0.3f, 0.3f,
                    t => W(0.7f), 1f, false, false, Veins(3, 18f, 0.6f)));
            previous = foot;
            hasPrevious = true;
        }
        var root = new GameObject("ArenaRootWall");
        Part(root.transform, "Roots", SaveMesh(m, "ArenaRootWall_Mesh"), Bark, true);
        SavePrefab(root);
    }

    // ---------- Enemies ----------

    static void BuildEnemies()
    {
        // Glimmer Moth.
        {
            var wing = SaveMesh(WorldMeshes.Petal(1f, 0.75f, 0.25f, 0.05f, 4, W(0.8f), W(1f), 0f, 0f, 0.3f), "GlimmerMoth_Wing");
            var root = new GameObject("GlimmerMoth");
            var moth = root.AddComponent<GlimmerMoth>();
            moth.health = 3f; moth.radius = 0.85f; moth.deathColor = Gold;
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            Part(body.transform, "Core", sphere, MothBody).Scaled(new Vector3(0.45f, 0.45f, 0.75f));
            Part(body.transform, "Halo", quad, MothHalo).At(Vector3.zero, default(Vector3), 2.4f);
            var pivots = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                var pivot = new GameObject("Wing" + i);
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition = new Vector3(0f, 0f, i < 2 ? 0.12f : -0.18f);
                Part(pivot.transform, "Mesh", wing, MothWing).At(Vector3.zero, new Vector3(0f, side * (i < 2 ? 75f : 118f), 0f), i < 2 ? 1f : 0.72f);
                pivots[i] = pivot.transform;
            }
            moth.wings = pivots;
            moth.body = body.transform;
            SavePrefab(root);
        }

        // Bramble Crawler. Pivot at the centre of its body, about 0.75 m above the ground.
        {
            var m = Blob(8, 12, new Vector3(1.1f, 0.75f, 1.5f), 14, 0.2f, 1.5f, null, (p, d) => W(0.7f + 0.3f * d.y));
            var rng = new System.Random(6);
            for (int i = 0; i < 14; i++)
            {
                var d = new Vector3(Rnd(rng, -1f, 1f), Rnd(rng, 0.2f, 1f), Rnd(rng, -1f, 0.6f)).normalized;
                m.Append(WorldMeshes.Thorn(Rnd(rng, 0.5f, 1.1f), 0.13f, 0.2f, 5, 3, W(0.8f), W(1f), 1f), Vector3.Scale(d, new Vector3(1f, 0.7f, 1.4f)), Quaternion.FromToRotation(Vector3.up, d), 1f);
            }
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 3; k++)
                {
                    Vector3 hip = new Vector3(side * 0.9f, -0.2f, -0.8f + k * 0.8f);
                    m.Append(Tube(Curve(6, hip, hip + new Vector3(side * 0.7f, 0.25f, 0f), hip + new Vector3(side * 1.05f, -0.55f, 0.1f)), t => Mathf.Lerp(0.13f, 0.04f, t), 5, k, 0f, 1f, t => W(0.6f)));
                }
            var root = new GameObject("BrambleCrawler");
            var crawler = root.AddComponent<BrambleCrawler>();
            crawler.health = 6f; crawler.radius = 1.25f; crawler.deathColor = Hazard;
            var body = Part(root.transform, "Body", SaveMesh(m, "BrambleCrawler_Mesh"), Crawler, true);
            var eyeL = Part(body.transform, "EyeL", sphere, EyeOrange).At(new Vector3(-0.42f, 0.18f, 1.32f), default(Vector3), 0.26f);
            var eyeR = Part(body.transform, "EyeR", sphere, EyeOrange).At(new Vector3(0.42f, 0.18f, 1.32f), default(Vector3), 0.26f);
            crawler.body = body.transform;
            crawler.eyes = new Renderer[] { eyeL.GetComponent<Renderer>(), eyeR.GetComponent<Renderer>() };
            crawler.line = Line(root.transform, "Tell", Hazard, 0.3f);
            SavePrefab(root);
        }

        // Gloam Watcher. Pivot at the eye, 3.4 m above its base.
        {
            var pillar = Lathe(new[]
            {
                new Vector2(1f, 0f), new Vector2(0.75f, 0.35f), new Vector2(0.5f, 1.2f), new Vector2(0.42f, 2.4f), new Vector2(0.75f, 2.8f),
                new Vector2(0.95f, 3.4f), new Vector2(0.75f, 4f), new Vector2(0.3f, 4.5f), new Vector2(0f, 4.7f)
            }, 10, i => W(0.9f, i == 1 || i == 4 || i == 6 ? 1f : 0f), 8, 0.08f);
            var root = new GameObject("GloamWatcher");
            var watcher = root.AddComponent<GloamWatcher>();
            watcher.health = 8f; watcher.radius = 1.3f; watcher.deathColor = Hazard;
            Part(root.transform, "Pillar", SaveMesh(pillar, "GloamWatcher_Pillar"), WatcherStone, true).At(new Vector3(0f, -3.4f, 0f));
            var eye = new GameObject("Eye");
            eye.transform.SetParent(root.transform, false);
            var glow = Part(eye.transform, "Glow", sphere, EyeOrange).At(new Vector3(0f, 0f, 0.75f), default(Vector3), 0.7f);
            Part(eye.transform, "Halo", quad, EyeHalo).At(new Vector3(0f, 0f, 0.9f), default(Vector3), 2.6f);
            watcher.eye = eye.transform;
            watcher.eyeGlow = glow.GetComponent<Renderer>();
            watcher.beam = Line(root.transform, "Beam", Hazard, 0.1f);
            SavePrefab(root);
        }
    }
}
