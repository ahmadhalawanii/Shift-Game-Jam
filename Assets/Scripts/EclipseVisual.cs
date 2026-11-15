using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The Sleeper as a great owl spirit. In Dream it sleeps with its wings folded around it like a cloak;
// in Nightmare its eyes snap open and the wings spread across the arena. Everything is built once from
// procedural meshes. This class only draws: it reads the boss state and never changes it, so the fight
// plays the same with or without it.
[DisallowMultipleComponent]
public sealed class EclipseVisual : MonoBehaviour
{
    public int GeneratedTriangleCount { get; private set; }
    public int GeneratedRendererCount { get; private set; }
    public Transform Core => crystal;

    // Head: an ellipsoid with the face towards -z. The core keeps its old height (CoreY).
    const float Hx = 3.1f, Hy = 2.8f, Hz = 2.7f, HeadY = 1.4f, EyeX = 1.25f, EyeY = 1.5f, CoreY = 2.5f;
    static readonly Vector3 BodyCenter = new Vector3(0f, -3.0f, 0.7f), BodyRadii = new Vector3(3.5f, 3.6f, 3.1f);

    struct Feather
    {
        public Transform transform;
        public Vector3 foldPosition, spreadPosition;
        public Quaternion foldRotation, spreadRotation;
        public float order, phase;
    }

    readonly List<Mesh> ownedMeshes = new List<Mesh>();
    readonly List<Material> ownedMaterials = new List<Material>();
    readonly List<Feather> wingFeathers = new List<Feather>();
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int TintColor = Shader.PropertyToID("_Color");
    static readonly int CrackColor = Shader.PropertyToID("_CrackColor");
    static readonly int SeamColor = Shader.PropertyToID("_SeamColor");
    static readonly int RimColor = Shader.PropertyToID("_RimColor");
    static readonly int FaceColor = Shader.PropertyToID("_FaceColor");
    static readonly int MixId = Shader.PropertyToID("_Mix");
    static readonly int FlashId = Shader.PropertyToID("_Flash");
    static readonly int ChargeId = Shader.PropertyToID("_Charge");
    static readonly int SelfLitId = Shader.PropertyToID("_SelfLit");
    static readonly int CracksId = Shader.PropertyToID("_Cracks");

    EclipseBoss boss;
    Transform figure, innerSeal, outerSeal, eye, eyeAnchor, halo, crystal, shardOrbit, eclipseRing;
    Transform castSigil, castInner, releaseRing;
    readonly Transform[] wingRoots = new Transform[2];
    readonly Transform[] tufts = new Transform[2];
    readonly Transform[] brows = new Transform[2];
    readonly Transform[] feet = new Transform[2];
    readonly Transform[] roots = new Transform[6];
    readonly Transform[] shards = new Transform[10];
    Material body, plume, wingDream, wingNight, stoneDream, stoneNight, bone;
    Material eyeGlow, glareGlow, crescentGlow, seals, core, ringGlow, haloGlow, moteGlow, veinDream, veinNight, castGlow, releaseGlow;
    ParticleSystem motes;
    float charge, flash, snap, hitFlash, lastHealth = -1f, spread, release, lastTarget;
    int lastPhase = 1;

    void Awake()
    {
        boss = GetComponent<EclipseBoss>();
        body = MakeBody("Sleeper / body plumage", 0.5f, 0f, 0.35f);
        plume = MakeBody("Sleeper / plumes", 0.5f, 1f, 0.4f);
        wingDream = MakeBody("Sleeper / dream wing", 0.5f, 1f, 0.4f);
        wingNight = MakeBody("Sleeper / nightmare wing", 0.5f, 1f, 0.4f);
        stoneDream = MakeBody("Sleeper / dream roots", 0.8f, 0f, 0.5f);
        stoneNight = MakeBody("Sleeper / nightmare roots", 0.8f, 0f, 0.5f);
        bone = MakeBody("Sleeper / beak and claws", 0.9f, 0f, 0.85f);
        eyeGlow = MakeFromResource("Combat/ColossusEye", "Sleeper / eyes");
        haloGlow = MakeFromResource("Combat/ColossusHalo", "Sleeper / moon halo");
        glareGlow = MakeFromResource("Combat/ColossusHalo", "Sleeper / eye glare");
        glareGlow.SetFloat("_Rays", 7f);
        glareGlow.SetFloat("_Speed", 0.35f);
        crescentGlow = MakeGlow("Sleeper / sleeping eyes");
        seals = MakeGlow("Sleeper / celestial engraving");
        core = MakeGlow("Sleeper / exposed heart");
        ringGlow = MakeGlow("Sleeper / eclipse ring");
        veinDream = MakeGlow("Sleeper / dream seams");
        veinNight = MakeGlow("Sleeper / ember seams");
        castGlow = MakeGlow("Sleeper / cast sigil");
        releaseGlow = MakeGlow("Sleeper / release ring");
        moteGlow = MakeGlow("Sleeper / motes");
        moteGlow.SetFloat("_Trail", 0f);

        figure = Pivot("Sleeper sculpt", transform);
        BuildHalo();
        BuildBody();
        BuildHead();
        BuildWings();
        BuildFeetAndRoots();
        BuildSeals();
        BuildCoreAndDebris();
        BuildMotes();
        BuildCastSigil();
        ApplyPalette(0f, 0f, false);
        Pose(0f, 0f, 0f, 0f);
        Flight = Slash = Slam = Gust = 0f;
        // The wing slash is part of the owl: it only exists while this body does.
        if (boss != null && GetComponent<OwlWingSlash>() == null) gameObject.AddComponent<OwlWingSlash>();
    }

    // Set by attacks to drive the body: Flight 0..1 (airborne), Slash -0.5..1 (wings drawn back to swept
    // forward), Slam and Gust are one-shot pulses.
    public static float Flight, Slash, Slam, Gust;
    float flightNow, slashNow, slamNow, gustNow, blinkNow = 1f, lastStroke, nextHoot;
    Transform headPivot;

    void OnEnable() { WorldState.VisualChanged += OnVisualChanged; }
    void OnDisable() { WorldState.VisualChanged -= OnVisualChanged; }

    // The swap crest: the eyes flash, the wings snap and a shockwave leaves the boss.
    void OnVisualChanged(World w)
    {
        if (boss == null || !boss.Engaged) return;
        flash = 1f;
        snap = w == World.Nightmare ? 1f : -1f;
        CombatPulse.Spawn(transform.position, 16f, w == World.Nightmare ? new Color(1f, 0.35f, 0.08f) : new Color(0.55f, 0.85f, 1f));
        if (motes != null) motes.Emit(90);
        if (w == World.Nightmare) OwlAudio.Play(OwlAudio.Sound.Screech, 0.95f); else OwlAudio.Play(OwlAudio.Sound.Hoot, 0.8f, 0.8f);
    }

    // ---------- Shape helpers ----------

    static float Bell(float d, float s) { return Mathf.Exp(-(d * d) / (s * s)); }
    static float SS(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }

    // Overlapping rows of rounded feather tips, like roof tiles. u runs around, v runs up.
    static float Scallop(float u, float v, float columns, float rows)
    {
        float fv = v * rows;
        float row = Mathf.Floor(fv);
        fv -= row;
        float fu = Mathf.Repeat(u * columns + (Mathf.Repeat(row, 2f) < 0.5f ? 0f : 0.5f), 1f) * 2f - 1f;
        return (1f - fv) * Mathf.Sqrt(Mathf.Max(0f, 1f - fu * fu));
    }

    // 1 inside the pale heart-shaped facial disc, 0 outside; edge is the distance used for its rim.
    static float FaceDisc(float x, float y, out float edge)
    {
        float ax = Mathf.Abs(x);
        float eyes = Mathf.Sqrt((ax - EyeX) * (ax - EyeX) + (y - EyeY) * (y - EyeY)) / 1.55f;
        float chin = Mathf.Sqrt(x * x + (y - 0.35f) * (y - 0.35f)) / 1.15f;
        edge = Mathf.Min(eyes, chin);
        return SS(1.06f, 0.9f, edge);
    }

    static float HeadRelief(float x, float y)
    {
        float edge;
        float disc = FaceDisc(x, y, out edge);
        float relief = -0.2f * disc + 0.18f * Bell(edge - 1.0f, 0.13f);
        relief += 0.3f * Bell(x, 0.3f) * SS(-0.2f, 0.5f, y) * SS(3.3f, 2.3f, y); // ridge between the eyes
        return relief;
    }

    // Depth of the finished head surface at (x, y) on its front.
    static float HeadZ(float x, float y)
    {
        float q = 1f - x * x / (Hx * Hx) - (y - HeadY) * (y - HeadY) / (Hy * Hy);
        if (q <= 0f) return 0f;
        float s = Mathf.Sqrt(q);
        return -Hz * s - HeadRelief(x, y) * SS(0.1f, 0.5f, s);
    }

    // One feather: root at the origin, pointing along +y, flat in the xy plane, face towards -z.
    // The trailing edge has two notches so the silhouette stays ragged.
    static void AddFeather(Smooth g, Matrix4x4 m, float length, float width, int seed)
    {
        Func<float, float> profile = s => Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(s, 0.62f))), 0.7f) * 0.95f + 0.05f * (1f - s);
        float n1 = 0.55f + 0.06f * (seed % 3), n2 = 0.8f - 0.04f * (seed % 2);
        Func<float, float, bool, Vector4> point = (u, s, back) =>
        {
            float across = u * 2f - 1f, w = width * 0.5f * profile(s);
            float notch = across > 0f ? 1f - across * (0.5f * Bell(s - n1, 0.05f) + 0.55f * Bell(s - n2, 0.045f)) : 1f;
            float z = -0.1f * length * Mathf.Sin(Mathf.PI * s * 0.9f) + 0.5f * w * across * across;
            if (back) z += 0.04f + 0.1f * (1f - across * across) * profile(s);
            Vector3 p = m.MultiplyPoint3x4(new Vector3(w * across * notch, length * s, z));
            return new Vector4(p.x, p.y, p.z, 1f);
        };
        Vector3 front = m.MultiplyVector(Vector3.back);
        g.Grid(6, 16, (u, s) => point(u, s, false), false, p => front);
        g.Grid(6, 16, (u, s) => point(u, s, true), false, p => -front);
    }

    // ---------- Body and head ----------

    void BuildBody()
    {
        var g = new Smooth();
        g.Grid(150, 110, (u, v) =>
        {
            float theta = (u * 2f - 1f) * Mathf.PI, phi = (v - 0.5f) * Mathf.PI;
            float cp = Mathf.Cos(phi);
            Vector3 dir = V(Mathf.Sin(theta) * cp, Mathf.Sin(phi), -Mathf.Cos(theta) * cp);
            float scale = Scallop(u, v, 24f, 13f);
            Vector3 p = BodyCenter + Vector3.Scale(BodyRadii, dir) + dir * (0.2f * scale);
            return new Vector4(p.x, p.y, p.z, 0.55f + 0.5f * scale);
        }, true, p => p - BodyCenter, null, (u, v) =>
        {
            // A paler breast on the front of the body.
            float theta = (u * 2f - 1f) * Mathf.PI, phi = (v - 0.5f) * Mathf.PI;
            float s = Mathf.Cos(theta) * Mathf.Cos(phi);
            return 1f - 0.5f * SS(0.5f, 0.95f, s) * SS(-0.95f, -0.2f, Mathf.Sin(phi)) * SS(0.9f, 0.3f, Mathf.Sin(phi));
        });
        AttachSmooth("Feathered body", g, figure, body);

        // A ruff of short feathers around the neck.
        var ruff = new Smooth();
        for (int i = 0; i < 18; i++)
        {
            float a = i * 20f;
            Vector3 at = V(Mathf.Cos(a * Mathf.Deg2Rad) * 2.7f, -0.35f, 0.6f - Mathf.Sin(a * Mathf.Deg2Rad) * 2.7f);
            Quaternion turn = Quaternion.Euler(0f, a - 90f, 0f) * Quaternion.Euler(34f, 0f, 180f + (i % 2 == 0 ? 6f : -6f));
            AddFeather(ruff, Matrix4x4.TRS(at, turn, Vector3.one), i % 2 == 0 ? 2.5f : 2.0f, 1.25f, i);
        }
        AttachSmooth("Neck ruff", ruff, figure, plume);
    }

    void BuildHead()
    {
        headPivot = Pivot("Head", figure);
        Vector3 center = V(0f, HeadY, 0f);
        var g = new Smooth();
        g.Grid(170, 130, (u, v) =>
        {
            float theta = (u * 2f - 1f) * Mathf.PI, phi = (v - 0.5f) * Mathf.PI;
            float cp = Mathf.Cos(phi), s = Mathf.Cos(theta) * cp;
            Vector3 dir = V(Mathf.Sin(theta) * cp, Mathf.Sin(phi), -Mathf.Cos(theta) * cp);
            float x = Hx * dir.x, y = HeadY + Hy * dir.y;
            float front = SS(0.1f, 0.5f, s), edge;
            float disc = FaceDisc(x, y, out edge) * front;
            float relief = HeadRelief(x, y) * front;
            float scale = Scallop(u, v, 26f, 12f) * (1f - disc) * Mathf.Clamp01(cp * 1.6f);
            Vector3 p = V(x, y, Hz * dir.z - relief) + dir * (0.15f * scale);
            float shade = Mathf.Lerp(0.6f + 0.45f * scale, Mathf.Clamp01(0.8f + 0.9f * relief), disc);
            return new Vector4(p.x, p.y, p.z, shade);
        }, true, p => p - center, null, (u, v) =>
        {
            float theta = (u * 2f - 1f) * Mathf.PI, phi = (v - 0.5f) * Mathf.PI;
            float cp = Mathf.Cos(phi), s = Mathf.Cos(theta) * cp, edge;
            return 1f - FaceDisc(Hx * Mathf.Sin(theta) * cp, HeadY + Hy * Mathf.Sin(phi), out edge) * SS(0.1f, 0.5f, s);
        });
        AttachSmooth("Owl head", g, headPivot, body);

        // Eyes: two discs set into the face. Closed in Dream, they open by scaling up.
        eyeAnchor = Pivot("Eye anchor", headPivot);
        eyeAnchor.localPosition = V(0f, EyeY, 0f);
        eye = Pivot("Sleeper eyes", eyeAnchor);
        var discs = new Smooth();
        var glare = new Geometry();
        var asleep = new Geometry();
        for (int side = -1; side <= 1; side += 2)
        {
            float cx = side * EyeX;
            discs.Grid(30, 6, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                float x = cx + Mathf.Cos(a) * v * 0.98f, y = Mathf.Sin(a) * v * 0.98f;
                return new Vector4(x, y, HeadZ(x, EyeY + y) - 0.07f - 0.14f * (1f - v * v), 1f);
            }, true, p => Vector3.back, (u, v) =>
            {
                float a = u * Mathf.PI * 2f;
                return new Vector2(0.5f + Mathf.Cos(a) * v * 0.5f, 0.5f + Mathf.Sin(a) * v * 0.5f);
            });
            GlowQuad(glare, V(cx, 0f, HeadZ(cx, EyeY) - 0.5f), 2.5f);
            // The closed eye: a soft downward crescent.
            var lid = new Vector3[15];
            for (int i = 0; i < lid.Length; i++)
            {
                float a = Mathf.Lerp(200f, 340f, (float)i / (lid.Length - 1)) * Mathf.Deg2Rad;
                float x = cx + Mathf.Cos(a) * 0.86f, y = EyeY + 0.42f + Mathf.Sin(a) * 0.86f;
                lid[i] = V(x, y, HeadZ(x, y) - 0.08f);
            }
            asleep.Ribbon(lid, 0.085f);
        }
        AttachSmooth("Burning eyes", discs, eye, eyeGlow, true);
        Attach("Eye glare", glare, eyeAnchor, glareGlow, true);
        Attach("Sleeping eyes", asleep, headPivot, crescentGlow, true);

        // Hooked beak.
        float bz = HeadZ(0f, 1.0f);
        Vector3 b0 = V(0f, 1.05f, bz + 0.25f), b1 = V(0f, 1.0f, bz - 0.75f), b2 = V(0f, 0.45f, bz - 1.05f), b3 = V(0f, -0.2f, bz - 0.5f);
        var beak = new Smooth();
        beak.Tube(t => Curve(b0, b1, b2, b3, t), t => Mathf.Lerp(0.55f, 0.03f, Mathf.Pow(t, 1.15f)), 14, 10, false, true);
        AttachSmooth("Beak", beak, headPivot, bone);

        // Ear tufts and brows are separate so they can react to the world.
        for (int h = 0; h < 2; h++)
        {
            float side = h == 0 ? -1f : 1f;
            tufts[h] = Pivot(h == 0 ? "Left ear tuft" : "Right ear tuft", headPivot);
            tufts[h].localPosition = V(side * 1.7f, 3.5f, 0.3f);
            var tuft = new Smooth();
            float[] lengths = { 4.8f, 3.8f, 2.9f };
            for (int k = 0; k < 3; k++)
                AddFeather(tuft, Matrix4x4.TRS(V(side * k * 0.25f, -k * 0.2f, -k * 0.12f), Quaternion.Euler(0f, 0f, -side * k * 15f), Vector3.one), lengths[k], 1.4f, k + h);
            AttachSmooth("Tuft feathers", tuft, tufts[h], plume);

            brows[h] = Pivot(h == 0 ? "Left brow" : "Right brow", headPivot);
            var brow = new Smooth();
            AddFeather(brow, Matrix4x4.identity, 2.5f, 0.85f, h);
            AttachSmooth("Brow feather", brow, brows[h], plume);
        }
    }

    // ---------- Wings ----------

    void BuildWings()
    {
        int[] counts = { 18, 13, 10 };
        float[] lengthScale = { 1f, 0.55f, 0.3f }, depth = { 0f, -0.32f, -0.6f };
        Vector3 shoulder = V(2.6f, 1.4f, 1.2f), elbow = V(5.8f, 4.6f, 1.6f), wrist = V(10.4f, 4.4f, 1.4f);
        for (int w = 0; w < 2; w++)
        {
            float side = w == 0 ? -1f : 1f;
            wingRoots[w] = Pivot(w == 0 ? "Left wing" : "Right wing", figure);
            wingRoots[w].localPosition = V(side * shoulder.x, shoulder.y, shoulder.z);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < counts[row]; i++)
                {
                    float t = (float)i / (counts[row] - 1);
                    // Spread: feathers hang from the arm near the body and fan out at the wingtip.
                    Vector3 arm = (1f - t) * (1f - t) * shoulder + 2f * (1f - t) * t * elbow + t * t * wrist;
                    float angle = t < 0.5f ? Mathf.Lerp(-100f, -80f, t / 0.5f) : Mathf.Lerp(-80f, 58f, Mathf.Pow((t - 0.5f) / 0.5f, 0.9f));
                    float length = Mathf.Lerp(5.2f, 8.4f, SS(0f, 0.7f, t)) * (t > 0.85f ? Mathf.Lerp(1f, 0.8f, (t - 0.85f) / 0.15f) : 1f) * lengthScale[row] * (1f + 0.08f * Mathf.Sin(i * 2.4f));
                    Vector3 spreadAt = arm + V(0f, 0f, depth[row] - 0.03f * i);
                    Quaternion spreadTurn = Quaternion.Euler(-6f, 0f, angle - 90f);
                    // Folded: the same feathers wrap around the body like a cloak that closes at the front.
                    float around = Mathf.Lerp(4f, 86f, t), ring = 3.35f + 0.28f * row;
                    Vector3 foldAt = V(Mathf.Cos(around * Mathf.Deg2Rad) * ring, 0.1f + 0.25f * row - 1.15f * t, 0.7f - Mathf.Sin(around * Mathf.Deg2Rad) * ring);
                    Quaternion foldTurn = Quaternion.Euler(0f, around - 90f, 0f) * Quaternion.Euler(13f + 7f * row, 0f, 180f + Mathf.Lerp(-7f, 7f, t) + 4f * Mathf.Sin(i * 1.9f));
                    if (side < 0f)
                    {
                        spreadAt.x = -spreadAt.x; foldAt.x = -foldAt.x;
                        spreadTurn = new Quaternion(spreadTurn.x, -spreadTurn.y, -spreadTurn.z, spreadTurn.w);
                        foldTurn = new Quaternion(foldTurn.x, -foldTurn.y, -foldTurn.z, foldTurn.w);
                    }
                    var mesh = new Smooth();
                    AddFeather(mesh, Matrix4x4.identity, length, Mathf.Lerp(2.0f, 1.45f, t) * (row == 0 ? 1f : 0.95f), i + row * 3);
                    Transform pivot = Pivot("Feather " + row + "." + i, wingRoots[w]);
                    AttachSmooth("Vane", mesh, pivot, w == 0 ? wingDream : wingNight);
                    wingFeathers.Add(new Feather
                    {
                        transform = pivot, order = t, phase = i * 1.7f + row,
                        foldPosition = foldAt - wingRoots[w].localPosition, spreadPosition = spreadAt - wingRoots[w].localPosition,
                        foldRotation = foldTurn, spreadRotation = spreadTurn
                    });
                }
        }
    }

    // ---------- Talons, roots, seals, core ----------

    void BuildFeetAndRoots()
    {
        for (int h = 0; h < 2; h++)
        {
            feet[h] = Pivot(h == 0 ? "Left talons" : "Right talons", figure);
            var leg = new Smooth();
            var claws = new Smooth();
            leg.Tube(t => V(0f, Mathf.Lerp(0.3f, 2.0f, t), Mathf.Lerp(0.1f, 1.1f, t)), t => Mathf.Lerp(0.5f, 0.75f, t) * (1f + 0.1f * Mathf.Sin(t * 26f)), 8, 12, false, false);
            for (int toe = -1; toe <= 1; toe++)
            {
                float dx = toe * 0.85f;
                Vector3 p0 = V(0f, 0.3f, 0.1f), p1 = V(dx * 0.3f, 0.5f, -0.7f), p2 = V(dx * 0.7f, 0.34f, -1.5f), p3 = V(dx, 0.08f, -2.0f);
                leg.Tube(t => Curve(p0, p1, p2, p3, t), t => Mathf.Lerp(0.38f, 0.24f, t) * (1f + 0.12f * Bell(t - 0.5f, 0.08f)), 12, 9, false, true);
                Vector3 c1 = p3 + V(0f, 0.2f, -0.5f), c2 = p3 + V(0f, -0.05f, -1.05f), c3 = p3 + V(0f, -0.65f, -1.3f);
                claws.Tube(t => Curve(p3 + V(0f, 0.05f, 0.15f), c1, c2, c3, t), t => Mathf.Lerp(0.23f, 0.02f, Mathf.Pow(t, 1.2f)), 12, 8, false, true);
            }
            AttachSmooth("Leg and toes", leg, feet[h], body);
            AttachSmooth("Claws", claws, feet[h], bone);
        }

        // Shadow roots creep out from under the owl and dive into the floor in front of it.
        for (int i = 0; i < roots.Length; i++)
        {
            float x = (i - 2.5f) * 2.3f;
            float side = Mathf.Sign(x), ax = Mathf.Abs(x);
            roots[i] = Pivot("Floor root " + i, figure);
            Vector3 a = V(side * 1.2f, -5.3f, 0.9f), b = V(side * (2.2f + ax * 0.5f), -4.2f - i % 2 * 0.4f, -1.6f);
            Vector3 c = V(side * (2.4f + ax * 0.8f), -4.6f, -4.6f), d = V(side * (1.5f + ax), -6.9f, -7.5f - i % 3);
            var tube = new Smooth();
            int seed = i;
            tube.Tube(t => Curve(a, b, c, d, t) + V(Mathf.Sin(t * 11f + seed) * 0.16f, 0f, Mathf.Cos(t * 9f + seed) * 0.16f),
                t => Mathf.Lerp(0.5f, 0.05f, Mathf.Pow(t, 0.8f)) * (1f + 0.18f * Mathf.Sin(t * 31f + seed)), 22, 9, true, true);
            AttachSmooth("Root", tube, roots[i], side < 0f ? stoneDream : stoneNight);
            var seam = new Geometry();
            var line = new Vector3[12];
            for (int k = 0; k < line.Length; k++)
            {
                float t = (float)k / (line.Length - 1);
                line[k] = Curve(a, b, c, d, t) + V(0f, 0f, -0.55f * Mathf.Lerp(1f, 0.1f, t));
            }
            seam.Ribbon(line, 0.035f);
            Attach("Root seam", seam, roots[i], side < 0f ? veinDream : veinNight, true);
        }
    }

    void BuildSeals()
    {
        innerSeal = Pivot("Inner celestial seal", figure);
        outerSeal = Pivot("Outer celestial seal", figure);
        innerSeal.localPosition = outerSeal.localPosition = V(0f, 1.1f, 2.4f);
        var inner = new Geometry();
        for (int i = 0; i < 3; i++)
        {
            float start = i * 120f + 13f;
            inner.Arc(4.9f, 4.9f, start, start + 87f, V(0f, 0.4f, 2.2f), 0.04f, 38);
            inner.Arc(5.2f, 5.2f, start + 6f, start + 68f, V(0f, 0.4f, 2.25f), 0.022f, 28);
        }
        for (int i = 0; i < 36; i++)
        {
            float a = (i * 10f + 8f) * Mathf.Deg2Rad;
            if (i % 9 == 0) continue;
            Vector3 unit = V(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 center = V(0f, 0.4f, 2.2f);
            inner.Ribbon(new[] { center + unit * 5.35f, center + unit * (i % 3 == 0 ? 5.75f : 5.5f) }, 0.028f);
        }
        Attach("Inner interrupted engraving", inner, innerSeal, seals, true);

        var outer = new Geometry();
        outer.Arc(7.3f, 7.3f, 18f, 145f, V(0f, 0.4f, 2.6f), 0.03f, 70);
        outer.Arc(7.3f, 7.3f, 200f, 293f, V(0f, 0.4f, 2.6f), 0.03f, 52);
        for (int i = 0; i < 7; i++)
        {
            float angle = (i * 44f + 19f) * Mathf.Deg2Rad;
            Vector3 radial = V(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 tangent = V(-radial.y, radial.x, 0f);
            Vector3 p = V(0f, 0.4f, 2.6f) + radial * 7.3f;
            outer.Ribbon(new[] { p - tangent * 0.2f, p + radial * 0.4f, p + tangent * 0.2f, p + radial * 0.18f }, 0.035f);
        }
        Attach("Outer broken seal and sigils", outer, outerSeal, seals, true);
    }

    void BuildCoreAndDebris()
    {
        // The core is a crest gem on the brow, between the ear tufts.
        crystal = Pivot("Crystal core", headPivot);
        crystal.localPosition = V(0f, CoreY, HeadZ(0f, CoreY) - 0.55f);
        var ico = new Geometry();
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] v =
        {
            V(-1, t, 0), V(1, t, 0), V(-1, -t, 0), V(1, -t, 0), V(0, -1, t), V(0, 1, t),
            V(0, -1, -t), V(0, 1, -t), V(t, 0, -1), V(t, 0, 1), V(-t, 0, -1), V(-t, 0, 1)
        };
        int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                    3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        for (int i = 0; i < f.Length; i += 3) ico.Triangle(v[f[i]] * 0.26f, v[f[i + 1]] * 0.26f, v[f[i + 2]] * 0.26f);
        Attach("Icosahedron", ico, crystal, core, true);

        // Loose feathers circle behind the owl.
        shardOrbit = Pivot("Feather orbit", figure);
        shardOrbit.localPosition = V(0f, 0.6f, 0f);
        for (int i = 0; i < shards.Length; i++)
        {
            shards[i] = Pivot("Loose feather " + i, shardOrbit);
            float a = i * Mathf.PI * 2f / shards.Length;
            shards[i].localPosition = V(Mathf.Cos(a) * (11.5f + i % 3), Mathf.Sin(a) * (11.5f + i % 3), 2.5f + i % 3);
            var loose = new Smooth();
            AddFeather(loose, Matrix4x4.TRS(V(0f, -0.9f, 0f), Quaternion.identity, Vector3.one), 1.8f + (i % 3) * 0.4f, 0.7f, i);
            AttachSmooth("Feather", loose, shards[i], plume);
        }

        // A huge eclipse ring hangs in the sky behind the arena and opens at each phase.
        var ringRoot = new GameObject("Sky eclipse ring");
        eclipseRing = ringRoot.transform;
        eclipseRing.position = EclipseEncounter.ArenaCenter + V(0f, 24f, 38f);
        var sky = new Geometry();
        sky.Arc(16f, 16f, 0f, 360f, Vector3.zero, 0.9f, 96);
        sky.Arc(18.5f, 18.5f, 10f, 170f, Vector3.zero, 0.25f, 48);
        sky.Arc(18.5f, 18.5f, 190f, 350f, Vector3.zero, 0.25f, 48);
        Attach("Eclipse ring", sky, eclipseRing, ringGlow, true);
        eclipseRing.localScale = Vector3.zero;
    }

    // ---------- Halo, motes, cast sigil ----------

    static void GlowQuad(Geometry g, Vector3 center, float half)
    {
        g.Quad(center + V(-half, -half, 0f), center + V(-half, half, 0f), center + V(half, half, 0f), center + V(half, -half, 0f));
        int n = g.uv.Count;
        g.uv[n - 6] = new Vector2(0, 0); g.uv[n - 5] = new Vector2(0, 1); g.uv[n - 4] = new Vector2(1, 1);
        g.uv[n - 3] = new Vector2(0, 0); g.uv[n - 2] = new Vector2(1, 1); g.uv[n - 1] = new Vector2(1, 0);
    }

    void BuildHalo()
    {
        // A moon of light behind the head, so the owl always reads as a backlit silhouette.
        halo = Pivot("Moon halo", figure);
        halo.localPosition = V(0f, 1.8f, 5.6f);
        var quad = new Geometry();
        GlowQuad(quad, Vector3.zero, 21f);
        Attach("Halo rays", quad, halo, haloGlow, true);
    }

    void BuildMotes()
    {
        var go = Pivot("Drifting motes", figure).gameObject;
        motes = go.AddComponent<ParticleSystem>();
        motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = motes.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.5f);
        main.maxParticles = 360;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = motes.emission;
        emission.rateOverTime = 46f;
        var shape = motes.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 10.5f;
        shape.radiusThickness = 0.7f;
        var velocity = motes.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        var noise = motes.noise;
        noise.enabled = true;
        noise.strength = 0.7f;
        noise.frequency = 0.35f;
        var fade = motes.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = moteGlow;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        motes.Play();
    }

    // A mandala that draws itself in front of the owl while an attack charges, and a ring that
    // bursts outward when the attack fires. Both only read the boss's existing charge value.
    void BuildCastSigil()
    {
        castSigil = Pivot("Cast sigil", figure);
        castSigil.localPosition = V(0f, -1.6f, -6.8f);
        castInner = Pivot("Cast sigil inner", castSigil);
        var outer = new Geometry();
        outer.Arc(3.0f, 3.0f, 0f, 360f, Vector3.zero, 0.07f, 72);
        for (int i = 0; i < 8; i++) outer.Arc(3.45f, 3.45f, i * 45f + 6f, i * 45f + 34f, Vector3.zero, 0.05f, 10);
        for (int i = 0; i < 24; i++)
        {
            float a = i * 15f * Mathf.Deg2Rad;
            Vector3 unit = V(Mathf.Cos(a), Mathf.Sin(a), 0f);
            outer.Ribbon(new[] { unit * 3.05f, unit * (i % 3 == 0 ? 3.38f : 3.2f) }, 0.035f);
        }
        Attach("Sigil outer", outer, castSigil, castGlow, true);
        var inner = new Geometry();
        inner.Arc(1.7f, 1.7f, 0f, 360f, Vector3.zero, 0.05f, 48);
        for (int star = 0; star < 2; star++)
        {
            var tri = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float a = (star * 60f + i * 120f + 90f) * Mathf.Deg2Rad;
                tri[i] = V(Mathf.Cos(a), Mathf.Sin(a), 0f) * 2.9f;
            }
            for (int i = 0; i < 3; i++) inner.Ribbon(new[] { tri[i], tri[i + 1] }, 0.045f);
        }
        for (int i = 0; i < 6; i++)
        {
            float a = (i * 60f + 30f) * Mathf.Deg2Rad;
            inner.Arc(0.32f, 0.32f, 0f, 360f, V(Mathf.Cos(a), Mathf.Sin(a), 0f) * 2.3f, 0.03f, 14);
        }
        Attach("Sigil inner", inner, castInner, castGlow, true);
        castSigil.localScale = Vector3.zero;

        releaseRing = Pivot("Release ring", figure);
        releaseRing.localPosition = V(0f, -1.6f, -6.8f);
        var ring = new Geometry();
        ring.Arc(3f, 3f, 0f, 360f, Vector3.zero, 0.16f, 72);
        ring.Arc(2.6f, 2.6f, 0f, 360f, Vector3.zero, 0.05f, 72);
        Attach("Release ring", ring, releaseRing, releaseGlow, true);
        releaseRing.localScale = Vector3.zero;
    }

    // ---------- Animation ----------

    void LateUpdate()
    {
        if (figure == null) return;
        float t = Time.time, dt = Time.deltaTime;
        bool engaged = boss != null && boss.Engaged;
        bool transforming = boss != null && boss.InTransformation;
        bool fused = boss != null && boss.Phase == 3;
        float targetCharge = boss != null ? boss.ChargeAmount : 0f;
        charge = Mathf.MoveTowards(charge, targetCharge, dt * 5f);
        float mix = WorldState.Instance != null ? WorldState.Instance.Mix : 0f;
        float breath = Mathf.Sin(t * Mathf.Lerp(0.8f, 2.6f, mix));
        float opening = fused ? 0.85f : mix;
        if (transforming) opening *= Mathf.SmoothStep(0f, 1f, charge);

        // Attack-driven motion: flight (dive), wing slash, landing squash and gust beats. Attacks set the
        // static targets; the values here follow them so the motion is always smooth.
        flightNow = Mathf.MoveTowards(flightNow, Mathf.Clamp01(Flight), dt * 4f);
        slashNow = Mathf.MoveTowards(slashNow, Slash, dt * (Slash > slashNow ? 16f : 4f));
        if (Slam > 0f) { slamNow = Mathf.Max(slamNow, Slam); Slam = 0f; if (motes != null) motes.Emit(120); }
        slamNow = Mathf.MoveTowards(slamNow, 0f, dt * 2.2f);
        if (Gust > 0f) { gustNow = Mathf.Max(gustNow, Gust); Gust = 0f; if (motes != null) motes.Emit(40); }
        gustNow = Mathf.MoveTowards(gustNow, 0f, dt * 2.6f);

        // It rides its own wingbeats while the wings are open, and squashes into the floor when it lands.
        float hover = Mathf.Sin(t * 2.1f - 0.6f) * 0.4f * spread + Mathf.Sin(t * 11f) * 0.25f * flightNow;
        figure.localPosition = Vector3.up * (breath * 0.22f + hover - slamNow * 0.9f - (transforming ? Mathf.Sin(charge * Mathf.PI) * 2f : 0f));
        figure.localScale = new Vector3(1f + slamNow * 0.12f, 1f - slamNow * 0.16f + breath * 0.008f, 1f + slamNow * 0.12f);
        figure.localRotation = Quaternion.Euler(flightNow * 14f + slashNow * 6f, 0f, Mathf.Sin(t * 0.7f) * 1.2f * (1f - flightNow));

        // A hit lights the whole body for a moment. Flashes fade on unscaled time, so they still
        // clear while a cinematic holds the game clock.
        if (boss != null)
        {
            if (lastHealth >= 0f && boss.health < lastHealth - 0.001f) hitFlash = 1f;
            lastHealth = boss.health;
        }
        float realDt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        hitFlash = Mathf.MoveTowards(hitFlash, 0f, realDt * 5f);
        flash = Mathf.MoveTowards(flash, 0f, realDt * 2.5f);
        snap = Mathf.MoveTowards(snap, 0f, realDt * 3f);

        spread = Mathf.MoveTowards(spread, opening, dt * 1.5f);
        Pose(Mathf.Max(spread, flightNow), mix, t, breath);

        // Awake, it blinks every few seconds and its eyes widen as an attack charges.
        float blink = Mathf.Repeat(t + 1.3f, 4.1f) < 0.13f || Mathf.Repeat(t, 9.7f) < 0.1f ? 0.08f : 1f;
        blinkNow = Mathf.MoveTowards(blinkNow, blink, dt * 14f);
        eye.localScale = new Vector3(1f + charge * 0.06f, Mathf.Lerp(0f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Max(opening, flightNow))) * blinkNow * (1f + flash * 0.12f + charge * 0.1f), 1f);

        // Sounds that belong to the motion: a wingbeat on every down-stroke, and soft hoots while it sleeps.
        if (engaged && !EclipseEncounter.Finished)
        {
            float stroke = Mathf.Sin(t * 2.1f);
            if (stroke < 0f && lastStroke >= 0f && spread > 0.6f) OwlAudio.Play(OwlAudio.Sound.Whoosh, 0.28f, 0.75f);
            lastStroke = stroke;
            if (spread < 0.2f && t >= nextHoot)
            {
                OwlAudio.Play(OwlAudio.Sound.Hoot, 0.45f, UnityEngine.Random.Range(0.9f, 1.05f));
                nextHoot = t + UnityEngine.Random.Range(5f, 9f);
            }
        }
        // In Nightmare the eyes follow the player.
        var pc = BossHazards.Player;
        if (pc != null && mix > 0.05f)
        {
            Vector3 local = figure.InverseTransformPoint(pc.transform.position + Vector3.up);
            Vector2 look = Vector2.ClampMagnitude(new Vector2(local.x, local.y - EyeY) * 0.02f, 0.14f) * mix;
            eye.localPosition = new Vector3(look.x, look.y, 0f);
        }
        else eye.localPosition = Vector3.zero;

        // Head: it nods in its sleep. Awake, it turns to watch the player and cocks to one side now and then.
        float watch = 0f;
        if (pc != null)
        {
            Vector3 seen = figure.InverseTransformPoint(pc.transform.position);
            watch = Mathf.Clamp(Mathf.Atan2(seen.x, -seen.z) * Mathf.Rad2Deg, -18f, 18f) * spread;
        }
        float cock = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Sin(t * 0.55f) * 3f - 2f)) * 13f * spread * Mathf.Sign(Mathf.Sin(t * 0.21f));
        Quaternion asleepPose = Quaternion.Euler(5f + Mathf.Sin(t * 0.8f) * 3f, 0f, Mathf.Sin(t * 0.45f) * 2.5f);
        Quaternion awakePose = Quaternion.Euler(-3f + Mathf.Sin(t * 3.1f) * 1.5f - flightNow * 12f - charge * 5f, -watch, cock);
        headPivot.localRotation = Quaternion.Slerp(asleepPose, awakePose, Mathf.Max(spread, flightNow));
        headPivot.localScale = Vector3.one * (1f + charge * 0.03f);

        innerSeal.localRotation = Quaternion.Euler(0f, 0f, t * (engaged ? 3.5f : 1.4f));
        outerSeal.localRotation = Quaternion.Euler(0f, 0f, -t * 1.1f);
        crystal.localRotation = Quaternion.Euler(t * 37f, t * 53f, t * 21f);
        crystal.localScale = Vector3.one * (1f + charge * 0.5f + flash * 0.6f + (boss != null && boss.Stunned ? Mathf.Sin(t * 30f) * 0.25f : 0f));
        shardOrbit.localRotation = Quaternion.Euler(0f, 0f, t * (8f + mix * 18f));
        for (int i = 0; i < shards.Length; i++) shards[i].localRotation = Quaternion.Euler(t * (40f + i * 7f), t * 25f, i * 36f);
        for (int i = 0; i < roots.Length; i++)
            roots[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(t * (1f + mix) + i) * (4f + mix * 10f), 0f);

        if (boss != null && boss.Phase != lastPhase)
        {
            lastPhase = boss.Phase; flash = 1f;
            if (motes != null) motes.Emit(140);
            OwlAudio.Play(OwlAudio.Sound.Screech, 1f, 0.72f);
        }
        float ringOpen = boss == null || !engaged ? 0f : boss.InTransformation ? boss.ChargeAmount : boss.Phase > 1 ? 1f : 0f;
        eclipseRing.localScale = Vector3.Lerp(eclipseRing.localScale, Vector3.one * ringOpen * (boss != null && boss.Phase == 3 ? 1.25f : 1f), dt * 2f);
        eclipseRing.rotation = Quaternion.Euler(0f, 0f, t * 4f);
        halo.localScale = Vector3.one * (1f + charge * 0.18f + flash * 0.25f + breath * 0.02f);

        // Cast sigil grows with the charge; when the charge is spent a ring bursts out of it.
        if (lastTarget > 0.55f && targetCharge < 0.05f && engaged && !transforming)
        {
            release = 1f;
            if (motes != null) motes.Emit(55);
            OwlAudio.Play(OwlAudio.Sound.Whoosh, 0.5f, 1.15f);
        }
        lastTarget = targetCharge;
        release = Mathf.MoveTowards(release, 0f, dt * 2.2f);
        castSigil.localScale = Vector3.one * Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0f, 1f, charge)) * (charge > 0.01f ? 1f : 0f);
        castSigil.localRotation = Quaternion.Euler(0f, 0f, t * 40f);
        castInner.localRotation = Quaternion.Euler(0f, 0f, -t * 110f);
        releaseRing.localScale = Vector3.one * (release > 0.001f ? Mathf.Lerp(1f, 4.6f, 1f - release * release) : 0f);
        Color castColor = fused ? new Color(0.85f, 0.4f, 1f) : Color.Lerp(new Color(0.4f, 0.95f, 1f), new Color(1f, 0.32f, 0.08f), mix);
        SetGlow(castGlow, castColor, Mathf.Clamp01(charge * 1.1f) * 0.85f);
        SetGlow(releaseGlow, Color.Lerp(castColor, Color.white, 0.35f), release * release * 0.9f);

        if (motes != null)
        {
            var main = motes.main;
            Color dreamMote = new Color(0.6f, 0.9f, 1f, 0.6f), nightMote = new Color(1f, 0.34f, 0.08f, 0.85f);
            main.startColor = fused ? new Color(0.85f, 0.45f, 1f, 0.75f) : Color.Lerp(dreamMote, nightMote, mix);
            var emission = motes.emission;
            emission.rateOverTime = Mathf.Lerp(40f, 95f, mix) + charge * 90f;
            main.simulationSpeed = Mathf.Lerp(0.7f, 1.7f, mix) + charge;
        }
        ApplyPalette(mix, charge, fused, opening);
    }

    // Wings, tufts, brows and talons. amount: 0 = asleep and folded, 1 = awake and spread.
    void Pose(float amount, float mix, float t, float breath)
    {
        float flare = Mathf.Clamp01(amount + charge * 0.12f + snap * 0.1f);
        for (int i = 0; i < wingFeathers.Count; i++)
        {
            Feather f = wingFeathers[i];
            // The feathers nearest the body open first, so the wing unrolls outward.
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flare * 1.5f - 0.5f * f.order));
            float flutter = Mathf.Sin(t * (1.6f + mix * 2.2f + flightNow * 9f) + f.phase) * (1.2f + mix * 3f + charge * 4f + flightNow * 7f + gustNow * 9f);
            f.transform.localPosition = Vector3.Lerp(f.foldPosition, f.spreadPosition, k);
            f.transform.localRotation = Quaternion.Slerp(f.foldRotation, f.spreadRotation, k) * Quaternion.Euler(flutter, 0f, flutter * 0.4f * f.order);
        }
        for (int w = 0; w < 2; w++)
        {
            float side = w == 0 ? -1f : 1f;
            // A slow beat while the wings are open; they sweep forward as an attack charges.
            // Flight beats hard and fast, a gust is one big stroke, and the slash sweeps both wings forward.
            float beat = Mathf.Sin(t * 2.1f) * 7f * amount + charge * 7f * amount
                + Mathf.Sin(t * 11f) * 26f * flightNow + Mathf.Sin(Mathf.Clamp01(gustNow) * Mathf.PI) * 30f;
            wingRoots[w].localRotation = Quaternion.Euler(0f, side * (charge * 10f * amount + slashNow * 72f), side * beat);
            wingRoots[w].localScale = Vector3.one * (1f + breath * 0.012f);

            tufts[w].localRotation = Quaternion.Slerp(Quaternion.Euler(22f, 0f, -side * 44f), Quaternion.Euler(-10f, 0f, -side * 14f), amount)
                * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f + w) * 2.5f);

            // Brows lie flat while asleep and pull down into a glare when awake.
            float bx = 0.28f, by = Mathf.Lerp(2.62f, 2.02f, amount);
            brows[w].localPosition = V(side * bx, by, HeadZ(bx, Mathf.Min(by, 2.4f)) - 0.32f);
            brows[w].localRotation = Quaternion.Euler(0f, -side * 30f, 0f) * Quaternion.Euler(0f, 0f, -side * Mathf.Lerp(98f, 62f, amount));

            Vector3 perch = V(side * 1.55f, -5.6f, -2.2f);
            Vector3 strike = V(side * 2.7f, -3.7f + Mathf.Sin(t * 2.3f + w) * 0.3f, -4.4f - charge * 1.3f);
            feet[w].localPosition = Vector3.Lerp(perch, strike, Mathf.SmoothStep(0f, 1f, amount));
            feet[w].localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(34f, -side * 14f, 0f), amount);
        }
    }

    void ApplyPalette(float mix, float anticipation, bool fused, float opening = 0f)
    {
        Color twilight = new Color(0.12f, 0.15f, 0.36f), abyss = new Color(0.04f, 0.035f, 0.09f);
        Color moonFace = new Color(0.78f, 0.86f, 1f), ashFace = new Color(0.4f, 0.3f, 0.44f);
        Color ivory = new Color(0.95f, 0.88f, 0.66f), iron = new Color(0.2f, 0.16f, 0.2f);
        Color glow = new Color(0.45f, 0.86f, 1f), ember = new Color(1f, 0.26f, 0.05f), violet = new Color(0.82f, 0.32f, 1f);
        float dreamSide = fused ? 0f : mix, nightSide = fused ? 1f : mix, whole = fused ? 0.62f : mix;
        Color crack = fused ? violet : ember;
        float flashNow = Mathf.Clamp01(hitFlash + flash * 0.35f);

        SetBody(body, Color.Lerp(twilight, abyss, whole), whole, glow, crack, Color.Lerp(moonFace, ashFace, whole), 0f, anticipation, flashNow);
        SetBody(plume, Color.Lerp(twilight, abyss, whole), whole, glow, crack * 0.6f, moonFace, 0f, anticipation, flashNow);
        SetBody(wingDream, Color.Lerp(twilight, abyss, dreamSide), dreamSide, glow, crack * 0.6f, moonFace, 0f, anticipation, flashNow);
        SetBody(wingNight, Color.Lerp(twilight, abyss, nightSide), nightSide, glow, crack * 0.6f, moonFace, 0f, anticipation, flashNow);
        SetBody(stoneDream, Color.Lerp(twilight * 0.7f, abyss, dreamSide), dreamSide, glow, crack, moonFace, 1f, anticipation, flashNow);
        SetBody(stoneNight, Color.Lerp(twilight * 0.7f, abyss, nightSide), nightSide, glow, crack, moonFace, 1f, anticipation, flashNow);
        SetBody(bone, Color.Lerp(ivory, iron, whole), whole, glow, crack, moonFace, 0.6f * whole, anticipation, flashNow);

        Color eyeColor = fused ? Color.Lerp(violet, Color.white, 0.25f) : Color.Lerp(glow, new Color(1f, 0.5f, 0.08f), mix);
        SetGlow(eyeGlow, Color.Lerp(eyeColor, Color.white, flash * 0.7f), 1f + flash * 0.6f + anticipation * 0.5f);
        SetGlow(glareGlow, eyeColor, Mathf.Clamp01(opening * 0.26f + anticipation * 0.25f + flash * 0.3f) * Mathf.SmoothStep(0f, 1f, opening));
        SetGlow(crescentGlow, Color.Lerp(glow, Color.white, 0.4f), Mathf.Clamp01(1f - opening * 2.2f) * (0.8f + 0.2f * Mathf.Sin(Time.time * 1.4f)));
        SetGlow(veinDream, Color.Lerp(glow, ember, dreamSide), 0.38f + anticipation * 0.28f);
        SetGlow(veinNight, Color.Lerp(glow, ember, nightSide), 0.38f + anticipation * 0.28f);
        SetGlow(seals, Color.Lerp(glow, ember, mix), 0.3f + anticipation * 0.3f);
        SetGlow(core, fused ? new Color(0.86f, 0.35f, 1f) : Color.Lerp(glow, ember, mix), 0.8f);
        SetGlow(ringGlow, fused ? new Color(0.85f, 0.3f, 1f) : Color.Lerp(new Color(0.7f, 0.9f, 1f), new Color(1f, 0.3f, 0.1f), mix), 0.55f);
        SetGlow(haloGlow, fused ? new Color(0.8f, 0.45f, 1f) : Color.Lerp(new Color(0.72f, 0.9f, 1f), new Color(1f, 0.3f, 0.12f), mix),
            Mathf.Clamp01(Mathf.Lerp(0.75f, 0.7f, mix) + anticipation * 0.3f + flash * 0.3f));
    }

    void SetBody(Material material, Color baseColor, float mix, Color seam, Color crack, Color faceColor, float cracks,
        float anticipation, float flashNow)
    {
        material.SetColor(BaseColor, baseColor);
        material.SetColor(SeamColor, seam);
        material.SetColor(CrackColor, crack);
        material.SetColor(FaceColor, faceColor);
        // A strong cool rim in Dream and a hot one in Nightmare, so the dark owl always has a lit edge.
        material.SetColor(RimColor, Color.Lerp(new Color(0.5f, 0.78f, 1f, 1.1f), new Color(1f, 0.22f, 0.42f, 0.95f), mix));
        material.SetFloat(MixId, mix);
        material.SetFloat(CracksId, cracks);
        material.SetFloat(ChargeId, anticipation);
        material.SetFloat(FlashId, flashNow);
        material.SetFloat(SelfLitId, Mathf.Lerp(0.2f, 0.04f, mix));
    }

    // ---------- Helpers ----------

    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
    static Vector3 Curve(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    Transform Pivot(string label, Transform parent)
    {
        var go = new GameObject(label);
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    void Attach(string label, Geometry geometry, Transform parent, Material material, bool additive = false)
    {
        var mesh = new Mesh { name = label };
        mesh.SetVertices(geometry.vertices);
        mesh.SetTriangles(geometry.indices, 0);
        mesh.SetUVs(0, geometry.uv);
        mesh.SetColors(geometry.colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Finish(label, mesh, geometry.indices.Count / 3, parent, material, additive);
    }

    void AttachSmooth(string label, Smooth geometry, Transform parent, Material material, bool additive = false)
    {
        var mesh = new Mesh { name = label };
        if (geometry.vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(geometry.vertices);
        mesh.SetTriangles(geometry.indices, 0);
        mesh.SetUVs(0, geometry.uv);
        mesh.SetColors(geometry.colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Finish(label, mesh, geometry.indices.Count / 3, parent, material, additive);
    }

    void Finish(string label, Mesh mesh, int triangles, Transform parent, Material material, bool additive)
    {
        ownedMeshes.Add(mesh);
        GeneratedTriangleCount += triangles;
        GeneratedRendererCount++;
        Transform child = Pivot(label, parent);
        child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = additive ? ShadowCastingMode.Off : ShadowCastingMode.On;
        renderer.receiveShadows = !additive;
        renderer.lightProbeUsage = additive ? LightProbeUsage.Off : LightProbeUsage.BlendProbes;
        renderer.reflectionProbeUsage = additive ? ReflectionProbeUsage.Off : ReflectionProbeUsage.BlendProbes;
    }

    Material MakeBody(string label, float crackScale, float petal, float gloss)
    {
        var shader = Resources.Load<Shader>("Combat/Colossus");
        Material material;
        if (shader != null)
        {
            material = new Material(shader) { name = label };
            material.SetFloat("_CrackScale", crackScale);
            material.SetFloat("_Petal", petal);
            material.SetFloat("_Gloss", gloss);
        }
        else
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = label };
        }
        ownedMaterials.Add(material);
        return material;
    }

    Material MakeFromResource(string path, string label)
    {
        var shader = Resources.Load<Shader>(path);
        var material = new Material(shader != null ? shader : Shader.Find("Lucid/GlowParticle")) { name = label };
        ownedMaterials.Add(material);
        return material;
    }

    Material MakeGlow(string label)
    {
        var material = new Material(Shader.Find("Lucid/GlowParticle")) { name = label };
        material.SetFloat("_Trail", 1f);
        ownedMaterials.Add(material);
        return material;
    }

    static void SetGlow(Material material, Color color, float alpha)
    {
        color.a = alpha;
        material.SetColor(TintColor, color);
    }

    void OnDestroy()
    {
        // The generated subtree is owned by this component even when removed individually.
        if (figure != null) Destroy(figure.gameObject);
        if (eclipseRing != null) Destroy(eclipseRing.gameObject);
        foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
        foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
    }

    // Smooth-shaded geometry: vertices are shared, so normals blend across the surface.
    // Vertex colour: rgb = cavity shade, a = 1 for the base colour and 0 for the masked (face) colour.
    sealed class Smooth
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<int> indices = new List<int>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<Color32> colors = new List<Color32>();

        int Add(Vector3 point, Vector2 tex, float shade, float mask = 1f)
        {
            byte s = (byte)Mathf.RoundToInt(Mathf.Clamp01(shade) * 255f);
            vertices.Add(point); uv.Add(tex); colors.Add(new Color32(s, s, s, (byte)Mathf.RoundToInt(Mathf.Clamp01(mask) * 255f)));
            return vertices.Count - 1;
        }

        // Winding is chosen so the face looks along 'outward'.
        void Tri(int a, int b, int c, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(n, outward) < 0f) { int k = b; b = c; c = k; }
            indices.Add(a); indices.Add(b); indices.Add(c);
        }

        // f returns the position in xyz and a cavity shade in w, for u and v in 0..1.
        public void Grid(int nu, int nv, Func<float, float, Vector4> f, bool wrapU, Func<Vector3, Vector3> outward,
            Func<float, float, Vector2> texture = null, Func<float, float, float> mask = null)
        {
            int first = vertices.Count, columns = wrapU ? nu : nu + 1;
            for (int j = 0; j <= nv; j++)
                for (int i = 0; i < columns; i++)
                {
                    float u = (float)i / nu, v = (float)j / nv;
                    Vector4 p = f(u, v);
                    Add(p, texture != null ? texture(u, v) : new Vector2(u, v), p.w, mask != null ? mask(u, v) : 1f);
                }
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    int i1 = wrapU ? (i + 1) % nu : i + 1;
                    int a = first + j * columns + i, b = first + j * columns + i1;
                    int c = first + (j + 1) * columns + i1, d = first + (j + 1) * columns + i;
                    Vector3 hint = outward((vertices[a] + vertices[b] + vertices[c] + vertices[d]) * 0.25f);
                    Tri(a, b, c, hint); Tri(a, c, d, hint);
                }
        }

        public void Tube(Func<float, Vector3> center, Func<float, float> radius, int segments, int sides, bool capStart, bool capEnd)
        {
            int first = vertices.Count;
            var centers = new Vector3[segments + 1];
            Vector3 normal = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 c = centers[i] = center(t);
                Vector3 tangent = (center(Mathf.Min(1f, t + 0.01f)) - center(Mathf.Max(0f, t - 0.01f))).normalized;
                if (i == 0)
                {
                    Vector3 reference = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
                    normal = Vector3.Cross(tangent, reference).normalized;
                }
                else normal = (normal - tangent * Vector3.Dot(normal, tangent)).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal);
                float r = Mathf.Max(0.002f, radius(t));
                for (int j = 0; j < sides; j++)
                {
                    float a = j * Mathf.PI * 2f / sides;
                    Add(c + (normal * Mathf.Cos(a) + binormal * Mathf.Sin(a)) * r, new Vector2((float)j / sides, t), 1f);
                }
            }
            for (int i = 0; i < segments; i++)
                for (int j = 0; j < sides; j++)
                {
                    int j1 = (j + 1) % sides;
                    int a = first + i * sides + j, b = first + i * sides + j1;
                    int c = first + (i + 1) * sides + j1, d = first + (i + 1) * sides + j;
                    Vector3 hint = (vertices[a] + vertices[b] + vertices[c] + vertices[d]) * 0.25f - (centers[i] + centers[i + 1]) * 0.5f;
                    Tri(a, b, c, hint); Tri(a, c, d, hint);
                }
            if (capStart) Cap(first, sides, centers[0], centers[0] - centers[1]);
            if (capEnd) Cap(first + segments * sides, sides, centers[segments], centers[segments] - centers[segments - 1]);
        }

        void Cap(int ring, int sides, Vector3 center, Vector3 outward)
        {
            int middle = Add(center, new Vector2(0.5f, 0.5f), 1f);
            for (int j = 0; j < sides; j++) Tri(middle, ring + j, ring + (j + 1) % sides, outward);
        }
    }

    // Flat-shaded geometry for facets and glow ribbons.
    sealed class Geometry
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<int> indices = new List<int>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<Color32> colors = new List<Color32>();

        int Vertex(Vector3 point, Vector2 tex)
        {
            int index = vertices.Count;
            vertices.Add(point); uv.Add(tex); colors.Add(new Color32(255, 255, 255, 255));
            return index;
        }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            indices.Add(Vertex(a, new Vector2(0f, 0f)));
            indices.Add(Vertex(b, new Vector2(1f, 0f)));
            indices.Add(Vertex(c, new Vector2(0.5f, 1f)));
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Triangle(a, b, c); Triangle(a, c, d);
        }

        public void Ribbon(Vector3[] path, float halfWidth)
        {
            if (path.Length < 2) return;
            int first = vertices.Count;
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 tangent = path[Mathf.Min(path.Length - 1, i + 1)] - path[Mathf.Max(0, i - 1)];
                Vector3 across = Vector3.Cross(tangent.normalized, Vector3.forward).normalized;
                float end = i == 0 || i == path.Length - 1 ? 0.2f : 1f;
                float u = (float)i / (path.Length - 1);
                // Three vertices across: this shader is transparent at v=0/1 and bright at .5.
                Vertex(path[i] - across * halfWidth * end, new Vector2(u, 0f));
                Vertex(path[i], new Vector2(u, 0.5f));
                Vertex(path[i] + across * halfWidth * end, new Vector2(u, 1f));
            }
            for (int i = 0; i < path.Length - 1; i++)
                for (int j = 0; j < 2; j++)
                {
                    int a = first + i * 3 + j, b = first + i * 3 + j + 3;
                    indices.Add(a); indices.Add(b); indices.Add(b + 1);
                    indices.Add(a); indices.Add(b + 1); indices.Add(a + 1);
                }
        }

        public void Arc(float radiusX, float radiusY, float startDegrees, float endDegrees,
            Vector3 center, float halfWidth, int segments)
        {
            var points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(startDegrees, endDegrees, (float)i / segments) * Mathf.Deg2Rad;
                points[i] = center + new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, 0f);
            }
            Ribbon(points, halfWidth);
        }
    }
}
