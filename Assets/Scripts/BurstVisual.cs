using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A bounded one-shot nova: inward ribbons, a compact impact, branching energy and a fading wake.
// Its visible front and gameplay radius share one clock; it holds no player callbacks after release.
public sealed class BurstVisual : MonoBehaviour
{
    const float TravelSeconds = 0.8f;
    const float TailSeconds = 0.5f;
    const int Spokes = 18;
    static readonly Color Violet = new Color(0.43f, 0.12f, 1f, 1f);
    static readonly Color Lilac = new Color(0.77f, 0.46f, 1f, 1f);
    static readonly Color Core = new Color(0.82f, 0.7f, 1f, 1f);

    readonly List<Vector3> vertices = new List<Vector3>(4096);
    readonly List<Color> colors = new List<Color>(4096);
    readonly List<Vector2> uvs = new List<Vector2>(4096);
    readonly List<int> indices = new List<int>(6144);
    readonly List<Enemy> targets = new List<Enemy>(32);
    readonly HashSet<Enemy> struck = new HashSet<Enemy>();
    Transform anchor;
    Transform view;
    Mesh mesh;
    Material material;
    float maximumRadius;
    float damage;
    float charge;
    float age;
    bool released;
    bool reachedEdge;
    Vector3 viewDirection = Vector3.forward;

    public static BurstVisual Begin(Transform player, float radius, float damage)
    {
        var effect = new GameObject("Memory nova").AddComponent<BurstVisual>();
        effect.anchor = player;
        effect.maximumRadius = Mathf.Max(0.1f, radius);
        effect.damage = damage;
        effect.transform.position = player.position + Vector3.up;
        effect.view = Camera.main != null ? Camera.main.transform : null;
        effect.BuildRenderer();
        return effect;
    }

    void BuildRenderer()
    {
        // This shader is also referenced by the GlowBurst Resources prefab, so it survives builds.
        var shader = Shader.Find("Lucid/GlowParticle");
        if (shader == null)
        {
            Debug.LogWarning("Memory nova could not find Lucid/GlowParticle; gameplay remains active.", this);
            return;
        }
        material = new Material(shader) { name = "Memory nova (runtime)" };
        material.SetFloat("_Trail", 1f);
        material.SetColor("_Color", Color.white);
        mesh = new Mesh { name = "Memory nova ribbons" };
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    public void SetCharge(float value) { if (!released) charge = Mathf.Clamp01(value); }

    public void Release()
    {
        if (released) return;
        if (anchor != null) transform.position = anchor.position + Vector3.up * 1.2f + anchor.right * 0.48f + anchor.forward * 0.7f;
        anchor = null;
        released = true;
        age = 0f;
        // Sparse secondary motes leave the mesh silhouettes and the combat lane readable.
        Fx.Burst(transform.position, new Color(0.62f, 0.3f, 1f), 24);
    }

    public void Cancel()
    {
        anchor = null;
        enabled = false;
        Destroy(gameObject);
    }

    void Update()
    {
        if (!released)
        {
            if (anchor == null) { Cancel(); return; }
            transform.position = anchor.position + Vector3.up * 1.2f + anchor.right * 0.48f + anchor.forward * 0.7f;
        }
        else
        {
            age += Time.deltaTime;
            if (!reachedEdge)
            {
                float front = FrontRadius(age);
                ApplyFront(front);
                reachedEdge = age >= TravelSeconds;
            }
            if (age >= TravelSeconds + TailSeconds) { Destroy(gameObject); return; }
        }

        if (mesh == null) return;
        if (view != null) viewDirection = (transform.position - view.position).normalized;
        vertices.Clear(); colors.Clear(); uvs.Clear(); indices.Clear();
        if (released) DrawNova(); else DrawCharge();
        mesh.Clear(false);
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(indices, 0, true);
    }

    float FrontRadius(float seconds)
    {
        return maximumRadius * Mathf.Pow(Mathf.Clamp01(seconds / TravelSeconds), 0.8f);
    }

    void ApplyFront(float radius)
    {
        if (EclipseEncounter.Finished) return;
        Vector3 center = transform.position;
        EnemyBullet.Clear(center, radius);
        // Damage may remove enemies (or finish the encounter), so iterate a reusable snapshot.
        targets.Clear();
        targets.AddRange(Enemy.All);
        float squaredRadius = radius * radius;
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null || !enemy.isActiveAndEnabled || struck.Contains(enemy)) continue;
            if ((enemy.transform.position - center).sqrMagnitude > squaredRadius) continue;
            struck.Add(enemy);
            enemy.TakeBurstDamage(damage, enemy.transform.position);
        }
    }

    void DrawCharge()
    {
        // Spiral strands are drawn inward, with distinct breaks instead of a solid glowing ball.
        float tightness = charge * charge;
        float shell = Mathf.Lerp(1.55f, 0.68f, tightness);
        for (int strand = 0; strand < 12; strand++)
        {
            float phase = strand * Mathf.PI * 2f / 12f;
            Vector3 previous = Vector3.zero;
            for (int step = 0; step <= 7; step++)
            {
                float t = step / 7f;
                float a = phase + (1f - t) * 0.95f + charge * 2.8f;
                float r = shell * Mathf.Lerp(0.3f, 1f, t);
                Vector3 p = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(phase * 2f + t * 2.2f) * r * 0.42f, Mathf.Sin(a) * r);
                if (step > 0)
                {
                    float alpha = (0.2f + charge * 0.42f) * (1f - t * 0.65f);
                    Ribbon(previous, p, 0.11f + tightness * 0.08f, Tint(Violet, alpha));
                    Ribbon(previous, p, 0.024f, Tint(Lilac, alpha));
                }
                previous = p;
            }
        }
        DrawCore(0.3f + tightness * 0.55f, charge * 0.6f, charge * 1.8f);
    }

    void DrawNova()
    {
        float progress = Mathf.Clamp01(age / TravelSeconds);
        float front = FrontRadius(age);
        float tail = 1f - Mathf.SmoothStep(0f, 1f, (age - TravelSeconds) / TailSeconds);
        float strength = Mathf.Lerp(0.9f, 0.5f, progress) * tail;

        // A broken, low band marks the exact horizontal reach without masking incoming attacks.
        const int segments = 144;
        for (int i = 0; i < segments; i++)
        {
            if (i % 12 == 0 || i % 12 == 1) continue;
            float a = i * Mathf.PI * 2f / segments;
            float b = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 p = new Vector3(Mathf.Cos(a) * front, -0.7f, Mathf.Sin(a) * front);
            Vector3 q = new Vector3(Mathf.Cos(b) * front, -0.7f, Mathf.Sin(b) * front);
            Ribbon(p, q, Mathf.Lerp(0.3f, 0.68f, progress), Tint(Violet, strength * 0.37f), true);
            Ribbon(p, q, 0.055f, Tint(Lilac, strength * 0.65f), true);
        }

        // Fractured tendrils travel with the front. Every third rises into a jagged crest;
        // shorter forked branches and narrow cores give the silhouette volume and direction.
        for (int spoke = 0; spoke < Spokes; spoke++)
        {
            float phase = spoke * Mathf.PI * 2f / Spokes + 0.08f * Mathf.Sin(spoke * 2.4f);
            Vector3 outward = new Vector3(Mathf.Cos(phase), 0f, Mathf.Sin(phase));
            Vector3 side = new Vector3(-outward.z, 0f, outward.x);
            Vector3 previous = BranchPoint(spoke, 0, front, outward, side);
            for (int step = 1; step <= 8; step++)
            {
                Vector3 p = BranchPoint(spoke, step, front, outward, side);
                float intensity = strength * (0.25f + step * 0.09f);
                Ribbon(previous, p, 0.24f + step * 0.025f, Tint(Violet, intensity * 0.48f));
                Ribbon(previous, p, 0.038f + step * 0.003f, Tint(Lilac, intensity * 0.8f));
                if (step == 5 || step == 7)
                {
                    float sign = ((spoke + step) % 2 == 0) ? 1f : -1f;
                    Vector3 fork = p - outward * Mathf.Min(2f, front * 0.08f)
                        + side * sign * Mathf.Min(2.5f, front * 0.1f) + Vector3.up * (spoke % 3 == 0 ? 0.7f : -0.3f);
                    Ribbon(p, fork, 0.11f, Tint(Violet, intensity * 0.7f));
                    Ribbon(p, fork, 0.025f, Tint(Lilac, intensity * 0.6f));
                }
                previous = p;
            }
        }

        // No full-screen fill: the bright impact is confined to a few slim vertical shards.
        float impact = Mathf.Pow(Mathf.Clamp01(1f - age / 0.26f), 1.5f);
        if (impact > 0f) DrawCore(0.8f + age * 8f, impact, age * 5f);
        DrawSparks(front, strength);
    }

    Vector3 BranchPoint(int spoke, int step, float front, Vector3 outward, Vector3 side)
    {
        float t = step / 8f;
        float radial = Mathf.Lerp(0.55f, 1f, t);
        float envelope = Mathf.Sin(t * Mathf.PI);
        float jagged = Mathf.Sin(spoke * 7.73f + step * 13.37f);
        float width = Mathf.Min(2.5f, front * 0.1f);
        float crest = spoke % 3 == 0 ? 4.2f : 1.5f;
        return outward * (front * radial)
            + side * (jagged * width * envelope)
            + Vector3.up * (-0.7f + Mathf.Min(1f, front * 0.15f) * envelope * (crest + jagged * 0.7f));
    }

    void DrawCore(float scale, float strength, float rotation)
    {
        for (int ray = 0; ray < 9; ray++)
        {
            float a = ray * Mathf.PI * 2f / 9f + rotation;
            Vector3 side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 root = side * (0.12f * scale) - Vector3.up * 0.55f;
            Vector3 joint = side * (0.55f * scale) + Vector3.up * (0.5f * scale);
            Vector3 tip = side * (0.9f * scale) + Vector3.up * ((ray % 3 == 0 ? 3f : 1.6f) * scale);
            Ribbon(root, joint, 0.42f * scale, Tint(Violet, strength * 0.5f));
            Ribbon(joint, tip, 0.25f * scale, Tint(Violet, strength * 0.35f));
            Ribbon(root, joint, 0.075f * scale, Tint(Core, strength * 0.65f));
            Ribbon(joint, tip, 0.035f * scale, Tint(Lilac, strength * 0.6f));
        }
    }

    void DrawSparks(float front, float strength)
    {
        for (int i = 0; i < 48; i++)
        {
            float phase = i * 2.399963f;
            float offset = (i % 7) / 7f;
            float localAge = age - 0.055f * (i % 4);
            if (localAge <= 0f) continue;
            float sparkAlpha = strength * Mathf.Clamp01(1f - localAge / (0.6f + offset * 0.5f));
            if (sparkAlpha <= 0f) continue;
            Vector3 direction = new Vector3(Mathf.Cos(phase), 0f, Mathf.Sin(phase));
            float r = front * (0.45f + offset * 0.48f);
            float height = -0.55f + localAge * (3f + offset * 7f) - 4f * localAge * localAge;
            Vector3 p = direction * r + Vector3.up * height;
            Vector3 velocity = direction * (0.16f + offset * 0.32f) + Vector3.up * (0.12f - localAge * 0.22f);
            Ribbon(p - velocity, p, 0.045f, Tint(Lilac, sparkAlpha * 0.85f));
        }
    }

    static Color Tint(Color color, float alpha) { color.a = Mathf.Clamp01(alpha); return color; }

    void Ribbon(Vector3 start, Vector3 end, float width, Color color, bool ground = false)
    {
        Vector3 direction = end - start;
        if (direction.sqrMagnitude < 0.000001f || color.a <= 0.001f) return;
        Vector3 normal = ground ? Vector3.up : viewDirection;
        Vector3 side = Vector3.Cross(direction, normal);
        if (side.sqrMagnitude < 0.000001f) side = Vector3.Cross(direction, Vector3.right);
        side = side.normalized * (width * 0.5f);
        int v = vertices.Count;
        vertices.Add(start - side); vertices.Add(start + side);
        vertices.Add(end - side); vertices.Add(end + side);
        colors.Add(color); colors.Add(color); colors.Add(color); colors.Add(color);
        uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f));
        uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f));
        indices.Add(v); indices.Add(v + 1); indices.Add(v + 2);
        indices.Add(v + 2); indices.Add(v + 1); indices.Add(v + 3);
    }

    void OnDestroy()
    {
        anchor = null;
        view = null;
        targets.Clear();
        struck.Clear();
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
