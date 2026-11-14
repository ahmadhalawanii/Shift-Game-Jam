using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Faceted blossom assembled once from procedural meshes.
[DisallowMultipleComponent]
public sealed class EclipseVisual : MonoBehaviour
{
    public int GeneratedTriangleCount { get; private set; }
    public int GeneratedRendererCount { get; private set; }

    readonly List<Mesh> ownedMeshes = new List<Mesh>();
    readonly List<Material> ownedMaterials = new List<Material>();
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    static readonly int TintColor = Shader.PropertyToID("_Color");
    EclipseBoss boss;
    Transform figure, innerSeal, outerSeal, eye;
    readonly Transform[] petals = new Transform[10];
    readonly Transform[] roots = new Transform[6];
    Material dreamPetal, nightmarePetal, dreamVein, nightmareVein, eyeGlow;
    Material stone, fissures, seals, core;
    float charge;

    void Awake()
    {
        boss = GetComponent<EclipseBoss>();
        stone = MakeStone("Eclipse / faceted obsidian");
        fissures = MakeGlow("Eclipse / split edges");
        seals = MakeGlow("Eclipse / celestial engraving");
        core = MakeGlow("Eclipse / exposed heart");
        figure = Pivot("Eclipse sculpt", transform);
        dreamPetal = stone;
        nightmarePetal = MakeStone("Sleeper / charcoal petals");
        dreamVein = fissures;
        nightmareVein = MakeGlow("Sleeper / ember veins");
        eyeGlow = MakeGlow("Sleeper / golden eye");
        BuildBlossom();
        BuildSeals();
        ApplyPalette(0f, 0f, false);
    }

    void LateUpdate()
    {
        if (figure == null) return;
        float t = Time.time;
        bool engaged = boss != null && boss.Engaged;
        bool transforming = boss != null && boss.InTransformation;
        bool fused = boss != null && boss.Phase == 3;
        float targetCharge = boss != null ? boss.ChargeAmount : 0f;
        charge = Mathf.MoveTowards(charge, targetCharge, Time.deltaTime * 5f);
        float mix = WorldState.Instance != null ? WorldState.Instance.Mix : 0f;
        float breath = Mathf.Sin(t * Mathf.Lerp(0.8f, 2.6f, mix));
        float opening = fused ? 0.85f : mix;
        if (transforming) opening *= Mathf.SmoothStep(0f, 1f, charge);
        figure.localPosition = Vector3.up * (breath * 0.22f - (transforming ? Mathf.Sin(charge * Mathf.PI) * 2f : 0f));
        for (int i = 0; i < petals.Length; i++)
        {
            float azimuth = i * 360f / petals.Length;
            petals[i].localRotation = Quaternion.Euler(0f, 0f, azimuth) *
                Quaternion.Euler(Mathf.Lerp(-29f, 18f, opening) + Mathf.Sin(t * 1.7f + i) * (3f + mix * 5f) - charge * 8f, 0f, 0f);
        }
        for (int i = 0; i < roots.Length; i++)
            roots[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(t * (1f + mix) + i) * (4f + mix * 10f), 0f);
        eye.localScale = new Vector3(1f, Mathf.Lerp(0.055f, 1f, opening), 1f);
        innerSeal.localRotation = Quaternion.Euler(0f, 0f, t * (engaged ? 3.5f : 1.4f));
        outerSeal.localRotation = Quaternion.Euler(0f, 0f, -t * 1.1f);
        ApplyPalette(mix, charge, fused);
    }

    void ApplyPalette(float mix, float anticipation, bool fused)
    {
        Color coral = new Color(0.66f, 0.24f, 0.34f);
        Color ash = new Color(0.095f, 0.045f, 0.07f);
        Color cyan = new Color(0.24f, 0.92f, 1f);
        Color ember = new Color(1f, 0.13f, 0.055f);
        dreamPetal.SetColor(BaseColor, Color.Lerp(coral, ash, fused ? 0f : mix));
        nightmarePetal.SetColor(BaseColor, Color.Lerp(coral, ash, fused ? 1f : mix));
        dreamPetal.SetColor(EmissionColor, Color.Lerp(coral * 0.26f, ash * 0.45f, fused ? 0f : mix));
        nightmarePetal.SetColor(EmissionColor, Color.Lerp(coral * 0.26f, ash * 0.45f, fused ? 1f : mix));
        SetGlow(dreamVein, Color.Lerp(cyan, ember, fused ? 0f : mix), 0.38f + anticipation * 0.28f);
        SetGlow(nightmareVein, Color.Lerp(cyan, ember, fused ? 1f : mix), 0.38f + anticipation * 0.28f);
        SetGlow(seals, Color.Lerp(cyan, ember, mix), 0.22f + anticipation * 0.2f);
        SetGlow(core, fused ? new Color(0.86f, 0.35f, 1f) : Color.Lerp(cyan, ember, mix), 0.54f);
        SetGlow(eyeGlow, Color.Lerp(cyan, new Color(1f, 0.72f, 0.14f), fused ? 1f : mix), 0.78f);
    }

    void BuildBlossom()
    {
        for (int i = 0; i < petals.Length; i++)
        {
            Transform petal = petals[i] = Pivot("Faceted petal " + i, figure);
            bool warmHalf = i > 0 && i < petals.Length / 2;
            float size = i % 2 == 0 ? 1f : 0.79f;
            var shell = new Geometry();
            var vein = new Geometry();
            Vector3 origin = V(0f, 0.42f, -0.1f);
            Vector3 left = V(-2.5f, 4.6f, -0.8f) * size;
            Vector3 right = V(2.5f, 4.6f, -0.8f) * size;
            Vector3 ridge = V(0.2f, 4.1f, -2.0f) * size;
            Vector3 tip = V(0.5f, 9.7f, 0.4f) * size;
            Vector3 back = V(0f, 4.5f, 0.1f) * size;
            shell.Triangle(origin, left, ridge);
            shell.Triangle(origin, ridge, right);
            shell.Triangle(left, tip, ridge);
            shell.Triangle(ridge, tip, right);
            shell.Triangle(origin, back, left);
            shell.Triangle(origin, right, back);
            shell.Triangle(back, tip, left);
            shell.Triangle(back, right, tip);
            vein.Ribbon(new[] { origin + Vector3.back * 0.05f, ridge + Vector3.back * 0.09f,
                Vector3.Lerp(ridge, tip, 0.72f) + Vector3.back * 0.09f }, 0.044f);
            Attach("Petal shell", shell, petal, warmHalf ? nightmarePetal : dreamPetal);
            Attach("Petal vein", vein, petal, warmHalf ? nightmareVein : dreamVein, true);
        }

        var mask = new Geometry();
        const int sides = 12;
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            float b = (i + 1) * Mathf.PI * 2f / sides;
            mask.Triangle(V(0f, 0f, -2.9f),
                V(Mathf.Cos(b) * 2f, Mathf.Sin(b) * 1.7f, -1.1f),
                V(Mathf.Cos(a) * 2f, Mathf.Sin(a) * 1.7f, -1.1f));
        }
        Attach("Faceted faceplate", mask, figure, stone);
        eye = Pivot("Sleeper eye", figure);
        var iris = new Geometry();
        iris.Quad(V(-1.25f, 0f, -3.0f), V(0f, 0.72f, -3.12f),
            V(1.25f, 0f, -3.0f), V(0f, -0.72f, -3.12f));
        Attach("Golden iris", iris, eye, eyeGlow, true);
        var pupil = new Geometry();
        pupil.Quad(V(-0.19f, 0f, -3.18f), V(0f, 0.58f, -3.18f),
            V(0.19f, 0f, -3.18f), V(0f, -0.58f, -3.18f));
        Attach("Eye slit", pupil, eye, nightmarePetal);
        for (int i = 0; i < roots.Length; i++)
        {
            float x = (i - 2.5f) * 2.3f;
            float side = Mathf.Sign(x);
            roots[i] = Pivot("Floor tendril " + i, figure);
            var body = new Geometry();
            var edges = new Geometry();
            AddPinion(body, edges, side, V(0.6f, -0.8f, 0.4f), V(Mathf.Abs(x) * 0.5f, -3f, 0.6f),
                V(Mathf.Abs(x) * 0.8f, -5.9f, -3f), V(Mathf.Abs(x), -6.9f, -6f - i % 3), 0.43f, 0.6f, 12, true);
            Attach("Tendril", body, roots[i], side < 0f ? dreamPetal : nightmarePetal);
            Attach("Tendril seams", edges, roots[i], side < 0f ? dreamVein : nightmareVein, true);
        }
    }

    void BuildSeals()
    {
        innerSeal = Pivot("Inner celestial seal", figure);
        outerSeal = Pivot("Outer celestial seal", figure);
        // Thin interrupted engraving behind the wings, with no solid disk or tiled objects.
        var inner = new Geometry();
        for (int i = 0; i < 3; i++)
        {
            float start = i * 120f + 13f;
            inner.Arc(3.25f, 3.25f, start, start + 87f, V(0f, 0.4f, 1.6f), 0.025f, 38);
            inner.Arc(3.49f, 3.49f, start + 6f, start + 68f, V(0f, 0.4f, 1.65f), 0.013f, 28);
        }
        for (int i = 0; i < 28; i++)
        {
            float a = (i * 360f / 28f + 8f) * Mathf.Deg2Rad;
            if (i % 7 == 0) continue;
            Vector3 unit = V(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 center = V(0f, 0.4f, 1.6f);
            inner.Ribbon(new[] { center + unit * 3.59f, center + unit * (i % 3 == 0 ? 3.78f : 3.67f) }, 0.017f);
        }
        Attach("Inner interrupted engraving", inner, innerSeal, seals, true);

        var outer = new Geometry();
        outer.Arc(5.4f, 5.4f, 18f, 145f, V(0f, 0.4f, 2.1f), 0.018f, 70);
        outer.Arc(5.4f, 5.4f, 200f, 293f, V(0f, 0.4f, 2.1f), 0.018f, 52);
        for (int i = 0; i < 7; i++)
        {
            float angle = (i * 44f + 19f) * Mathf.Deg2Rad;
            Vector3 radial = V(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 tangent = V(-radial.y, radial.x, 0f);
            Vector3 p = V(0f, 0.4f, 2.1f) + radial * 5.4f;
            // Open angular sigils, not text, and not repeated solid corona blocks.
            outer.Ribbon(new[] { p - tangent * 0.13f, p + radial * 0.26f,
                p + tangent * 0.13f, p + radial * 0.12f }, 0.022f);
        }
        Attach("Outer broken seal and sigils", outer, outerSeal, seals, true);
    }

    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
    static Vector3 Mirror(Vector3 point, float side) { point.x *= side; return point; }
    static Vector3 Curve(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    static void AddPinion(Geometry solid, Geometry glow, float side, Vector3 a, Vector3 b,
        Vector3 c, Vector3 d, float width, float depth, int segments, bool lit)
    {
        a = Mirror(a, side); b = Mirror(b, side); c = Mirror(c, side); d = Mirror(d, side);
        var sections = new Vector3[segments + 1, 4];
        var seam = lit ? new Vector3[segments] : null;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 p = Curve(a, b, c, d, t);
            Vector3 tangent = (Curve(a, b, c, d, Mathf.Min(1f, t + 0.01f)) -
                Curve(a, b, c, d, Mathf.Max(0f, t - 0.01f))).normalized;
            Vector3 across = Vector3.Cross(tangent, Vector3.forward).normalized;
            // Broken, serrated edge at three authored intervals; the front ridge stays clean.
            // Single-precision sin(pi) can be slightly negative. A fractional power
            // would turn the final cross-section into NaNs instead of a narrow tip.
            float envelope = Mathf.Sin(Mathf.Lerp(0.12f, 1f, t) * Mathf.PI);
            float taper = i == segments ? 0f : Mathf.Pow(Mathf.Clamp01(envelope), 0.8f);
            float serration = i > 1 && i < segments - 1 && i % 4 == 0 ? 1.27f : 1f;
            float w = Mathf.Max(0.002f, width * taper);
            float z = Mathf.Max(0.002f, depth * taper);
            sections[i, 0] = p + across * w * serration;
            sections[i, 1] = p + Vector3.forward * z;
            sections[i, 2] = p - across * w * 0.71f;
            sections[i, 3] = p - Vector3.forward * z;
            if (lit && i < segments)
                seam[i] = sections[i, 3] - Vector3.forward * 0.014f + across * w * 0.11f;
        }
        for (int i = 0; i < segments; i++)
            for (int j = 0; j < 4; j++)
            {
                int k = (j + 1) % 4;
                // Reverse the standard strip winding because across is tangent x forward.
                solid.Quad(sections[i, j], sections[i + 1, j], sections[i + 1, k], sections[i, k]);
            }
        solid.Quad(sections[0, 3], sections[0, 2], sections[0, 1], sections[0, 0]);
        solid.Quad(sections[segments, 0], sections[segments, 1], sections[segments, 2], sections[segments, 3]);
        if (lit) glow.Ribbon(seam, Mathf.Min(0.027f, width * 0.065f));
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
        ownedMeshes.Add(mesh);
        GeneratedTriangleCount += geometry.indices.Count / 3;
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

    Material MakeStone(string label)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = label };
        material.SetFloat("_Metallic", 0.42f);
        material.SetFloat("_Smoothness", 0.38f);
        material.EnableKeyword("_EMISSION");
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
        foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
        foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
    }

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
                    int a = first + i * 3 + j, b = a + 3;
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
