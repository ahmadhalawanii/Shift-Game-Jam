using System.Collections.Generic;
using UnityEngine;

// Pooled floor and beam shapes, lingering damage zones and floor-plane hit tests for boss attacks.
// One shared additive material; every shape is a LineRenderer coloured through vertex colour.
public sealed class BossHazards : MonoBehaviour
{
    struct Zone { public Vector3 center; public float radius, until, damage, nextTick; public LineRenderer ring; }

    static BossHazards instance;
    static Material material;
    static readonly Stack<LineRenderer> pool = new Stack<LineRenderer>();
    static readonly List<LineRenderer> live = new List<LineRenderer>();
    static readonly List<Zone> zones = new List<Zone>();
    static PlayerController player;
    public static bool ShowHitboxes;
    public const float FloorY = 0.14f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; material = null; pool.Clear(); live.Clear(); zones.Clear(); player = null; ShowHitboxes = false; }

    static BossHazards I
    {
        get
        {
            if (instance == null) instance = new GameObject("Boss hazards").AddComponent<BossHazards>();
            return instance;
        }
    }

    public static PlayerController Player
    {
        get { if (player == null) player = FindFirstObjectByType<PlayerController>(); return player; }
    }

    static LineRenderer Take(bool flat)
    {
        var host = I;
        LineRenderer line = null;
        while (pool.Count > 0 && line == null) line = pool.Pop();
        if (line == null)
        {
            if (material == null)
            {
                // Visual only: the energy shader takes the same colour, alpha and width as the glow shader.
                var energy = Resources.Load<Shader>("Combat/HazardEnergy");
                material = new Material(energy != null ? energy : Shader.Find("Lucid/GlowParticle"));
                material.SetFloat("_Trail", 1f);
                material.SetColor("_Color", Color.white);
            }
            var go = new GameObject("Boss hazard shape");
            go.transform.SetParent(host.transform, false);
            line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 0;
        }
        line.gameObject.SetActive(true);
        line.alignment = flat ? LineAlignment.TransformZ : LineAlignment.View;
        line.transform.rotation = flat ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
        live.Add(line);
        return line;
    }

    public static void Release(LineRenderer line)
    {
        if (line == null || !line.gameObject.activeSelf) return;
        line.gameObject.SetActive(false);
        live.Remove(line);
        pool.Push(line);
    }

    // A regular polygon on the floor. sides >= 32 reads as a circle.
    public static LineRenderer Polygon(Vector3 center, float radius, int sides, float rotationDegrees, float width, Color color)
    {
        var line = Take(true);
        SetPolygon(line, center, radius, sides, rotationDegrees);
        line.widthMultiplier = width;
        line.startColor = line.endColor = color;
        return line;
    }

    public static void SetPolygon(LineRenderer line, Vector3 center, float radius, int sides, float rotationDegrees)
    {
        sides = Mathf.Clamp(sides, 3, 64);
        float rot = rotationDegrees * Mathf.Deg2Rad;
        line.loop = true;
        line.positionCount = sides;
        for (int i = 0; i < sides; i++)
        {
            float a = rot + i * Mathf.PI * 2f / sides;
            line.SetPosition(i, new Vector3(center.x + Mathf.Sin(a) * radius, FloorY, center.z + Mathf.Cos(a) * radius));
        }
    }

    // A straight strip. flat = painted on the floor; otherwise a camera-facing beam.
    public static LineRenderer Line(Vector3 a, Vector3 b, float width, Color color, bool flat = true)
    {
        var line = Take(flat);
        line.loop = false;
        line.positionCount = 2;
        SetLine(line, a, b, flat);
        line.widthMultiplier = width;
        line.startColor = line.endColor = color;
        return line;
    }

    public static void SetLine(LineRenderer line, Vector3 a, Vector3 b, bool flat = true)
    {
        if (flat) { a.y = FloorY; b.y = FloorY; }
        line.SetPosition(0, a);
        line.SetPosition(1, b);
    }

    public static void Tint(LineRenderer line, Color color, float width)
    {
        if (line == null) return;
        line.startColor = line.endColor = color;
        line.widthMultiplier = width;
    }

    // A lingering damage area: damage (out of 100) every 0.5 s while the player stands in it.
    public static void AddZone(Vector3 center, float radius, float seconds, float damage, Color color)
    {
        I.enabled = true;
        zones.Add(new Zone
        {
            center = center, radius = radius, until = Time.time + seconds, damage = damage, nextTick = Time.time + 0.25f,
            ring = Polygon(center, radius, 40, 0f, 0.5f, color)
        });
    }

    public static void ClearAll()
    {
        for (int i = live.Count - 1; i >= 0; i--) Release(live[i]);
        zones.Clear();
    }

    void Update()
    {
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            var z = zones[i];
            if (Time.time >= z.until || EclipseEncounter.Finished) { Release(z.ring); zones.RemoveAt(i); continue; }
            float fade = Mathf.Clamp01((z.until - Time.time) / 0.6f);
            Color c = z.ring.startColor; c.a = 0.35f + 0.4f * fade * (0.6f + 0.4f * Mathf.Sin(Time.time * 9f + i));
            z.ring.startColor = z.ring.endColor = c;
            if (Time.time >= z.nextTick)
            {
                z.nextTick = Time.time + 0.5f;
                zones[i] = z;
                HitCircle(z.center, z.radius, z.damage, false);
                // Lethal damage ends the encounter and clears this list synchronously.
                if (EclipseEncounter.Finished) break;
            }
        }
        if (ShowHitboxes) DrawHitboxes();
    }

    static bool Grounded(PlayerController p) => p.transform.position.y < 0.7f;

    static float FlatDistanceSquared(Vector3 a, Vector3 b) { float x = a.x - b.x, z = a.z - b.z; return x * x + z * z; }

    // Floor-plane tests. Damage is out of 100 and maps to the player's memory fraction. jumpable: a hop clears it.
    public static bool HitCircle(Vector3 center, float radius, float damage, bool jumpable)
    {
        var p = Player;
        if (p == null || p.Recalling || (jumpable && !Grounded(p))) return false;
        if (FlatDistanceSquared(p.transform.position, center) > radius * radius) return false;
        return Hurt(p, damage, center);
    }

    public static bool HitSegment(Vector3 a, Vector3 b, float halfWidth, float damage)
    {
        var p = Player;
        if (p == null || p.Recalling) return false;
        Vector3 c = p.transform.position;
        float d2 = CombatGeometry.SegmentDistanceSquared(a.x, 0f, a.z, b.x, 0f, b.z, c.x, 0f, c.z);
        if (d2 > halfWidth * halfWidth) return false;
        return Hurt(p, damage, a);
    }

    public static bool HitTriangle(Vector3 a, Vector3 b, Vector3 c, float damage)
    {
        var p = Player;
        if (p == null || p.Recalling) return false;
        Vector3 q = p.transform.position;
        float d1 = Cross(q, a, b), d2 = Cross(q, b, c), d3 = Cross(q, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        if (neg && pos) return false;
        return Hurt(p, damage, (a + b + c) / 3f);
    }

    static float Cross(Vector3 p, Vector3 a, Vector3 b) => (p.x - b.x) * (a.z - b.z) - (a.x - b.x) * (p.z - b.z);

    static float wardHitAt = -99f;
    static bool Hurt(PlayerController p, float damage, Vector3 source)
    {
        var combat = p.GetComponent<PlayerCombat>();
        if (WorldState.CurrentOrDream == World.Dream && combat != null && combat.Dream != null && combat.Dream.WardUp)
        {
            if (Time.time >= wardHitAt) { combat.Dream.Block(BalanceSheet.Load().wardDrainBig); wardHitAt = Time.time + .5f; }
            return false;
        }
        float before = p.LastHitTime;
        p.Damage(damage / 100f);
        if (p.LastHitTime != before) LucidCombatHUD.DamageSource = source;
        return p.LastHitTime != before;
    }

    static void DrawHitboxes()
    {
        var p = Player;
        if (p != null) Circle(p.transform.position + Vector3.up, 0.65f, Color.green);
        for (int i = 0; i < zones.Count; i++) Circle(zones[i].center, zones[i].radius, Color.red);
        EnemyBullet.DrawHitboxes();
    }

    public static void Circle(Vector3 c, float r, Color color)
    {
        Vector3 prev = c + new Vector3(r, 0f, 0f);
        for (int i = 1; i <= 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            Vector3 next = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Debug.DrawLine(prev, next, color);
            prev = next;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        pool.Clear(); live.Clear(); zones.Clear();
        if (material != null) Destroy(material);
        material = null;
    }
}
