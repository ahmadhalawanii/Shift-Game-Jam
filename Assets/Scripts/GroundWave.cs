using System.Collections.Generic;
using UnityEngine;

// A world-independent floor hazard. Its swept front is resolved once per player,
// including a successful dash, so leaving dash immunity cannot cause a second hit.
public class GroundWave : MonoBehaviour
{
    const int Segments = 128;
    const float MaxRadius = 67f;
    const float WaveHeight = 0.55f;
    const float HitHalfWidth = 0.85f;
    static readonly List<GroundWave> Active = new List<GroundWave>();
    readonly Vector3[] points = new Vector3[Segments];
    LineRenderer rim, glow;
    Material material;
    Transform target;
    PlayerController controller;
    float speed, radius, previousSeparation, damage;
    bool resolved;
    float heading, halfArc;

    public static int ActiveCount => Active.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Active.Clear(); }

    public static void Spawn(Vector3 origin, Transform player, float travelSpeed, float heading = 0f, float halfArc = 180f, float damage = 0.16f, Color? tint = null)
    {
        if (EclipseEncounter.Finished || player == null || Active.Count >= 6) return;
        var wave = new GameObject("Crimson tide - dash through").AddComponent<GroundWave>();
        wave.transform.position = new Vector3(origin.x, 0.18f, origin.z);
        wave.heading = heading;
        wave.halfArc = Mathf.Clamp(halfArc, 5f, 180f);
        wave.target = player;
        wave.damage = damage;
        wave.controller = player.GetComponent<PlayerController>();
        wave.speed = Mathf.Clamp(travelSpeed, 6f, 18f);
        wave.previousSeparation = HorizontalDistance(player.position, wave.transform.position);
        Shader shader = Shader.Find("Lucid/GlowParticle");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        wave.material = new Material(shader);
        if (wave.material.HasProperty("_Trail")) wave.material.SetFloat("_Trail", 1f);
        if (wave.material.HasProperty("_Color")) wave.material.SetColor("_Color", Color.white);
        if (wave.material.HasProperty("_BaseColor")) wave.material.SetColor("_BaseColor", new Color(1f, 0.07f, 0.12f));
        Color c = tint ?? new Color(1f, 0.04f, 0.07f);
        wave.rim = wave.CreateRing("Leading edge", 0.34f, new Color(c.r * 2f, c.g * 2f, c.b * 2f, 1f));
        wave.glow = wave.CreateRing("Soft ground glow", 1.5f, new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.8f, 0.35f));
    }

    LineRenderer CreateRing(string label, float width, Color color)
    {
        var child = new GameObject(label);
        child.transform.SetParent(transform, false);
        var line = child.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = halfArc >= 180f;
        line.positionCount = Segments;
        line.widthMultiplier = width;
        line.startColor = line.endColor = color;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    void OnEnable() { Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    void Update()
    {
        if (EclipseEncounter.Finished || target == null)
        {
            Destroy(gameObject);
            return;
        }
        radius += speed * Time.deltaTime;
        if (radius > MaxRadius)
        {
            Destroy(gameObject);
            return;
        }
        for (int i = 0; i < Segments; i++)
        {
            float angle = Mathf.Lerp(heading - halfArc, heading + halfArc, i / (float)(Segments - 1)) * Mathf.Deg2Rad;
            points[i] = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
        }
        rim.SetPositions(points);
        glow.SetPositions(points);

        float separation = HorizontalDistance(target.position, transform.position) - radius;
        if (!resolved && CrossedFront(previousSeparation, separation, HitHalfWidth))
        {
            resolved = true;
            Vector3 offset = target.position - transform.position;
            float yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            if (controller != null && target.position.y < WaveHeight &&
                Mathf.Abs(Mathf.DeltaAngle(heading, yaw)) <= halfArc)
                controller.Damage(damage);
        }
        previousSeparation = separation;
    }

    public static bool CrossedFront(float previous, float current, float halfWidth)
    {
        return Mathf.Abs(current) <= halfWidth || Mathf.Abs(previous) <= halfWidth ||
            (previous > 0f && current < 0f) || (previous < 0f && current > 0f);
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x, z = a.z - b.z;
        return Mathf.Sqrt(x * x + z * z);
    }

    public static void ClearAll()
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            GroundWave wave = Active[i];
            if (wave == null) continue;
            wave.enabled = false;
            Destroy(wave.gameObject);
        }
        Active.Clear();
    }

    void OnDestroy()
    {
        Active.Remove(this);
        if (material != null) Destroy(material);
    }
}
