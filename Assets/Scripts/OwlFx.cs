using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Feather mesh used by every boss projectile: two crossed vanes on one shaft, so it reads from any
// angle while it spins. Lies along +y and is centred on the origin so the hit sphere stays in the middle.
public static class BossFeatherMesh
{
    static Mesh mesh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { mesh = null; }

    public static Mesh Get()
    {
        if (mesh != null) return mesh;
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var indices = new List<int>();
        const int along = 18, across = 6;
        const float length = 3.0f;
        for (int vane = 0; vane < 2; vane++)
        {
            float width = vane == 0 ? 1.2f : 0.6f;
            int first = vertices.Count;
            for (int j = 0; j <= along; j++)
            {
                float s = (float)j / along;
                // Narrow quill, full vane, pointed tip, with two notches for a ragged edge.
                float profile = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(s, 0.6f))), 0.75f) * Mathf.SmoothStep(0f, 1f, s * 5f);
                for (int i = 0; i <= across; i++)
                {
                    float a = (float)i / across * 2f - 1f;
                    float notch = a > 0f ? 1f - a * (0.45f * Mathf.Exp(-Mathf.Pow((s - 0.5f) / 0.05f, 2f)) + 0.5f * Mathf.Exp(-Mathf.Pow((s - 0.78f) / 0.045f, 2f))) : 1f;
                    float x = a * width * 0.5f * profile * notch, curl = 0.18f * profile * a * a;
                    Vector3 p = vane == 0 ? new Vector3(x, (s - 0.5f) * length, curl) : new Vector3(curl, (s - 0.5f) * length, x);
                    vertices.Add(p);
                    uv.Add(new Vector2((float)i / across, s));
                }
            }
            for (int j = 0; j < along; j++)
                for (int i = 0; i < across; i++)
                {
                    int a = first + j * (across + 1) + i, b = a + 1, c = a + across + 1, d = c + 1;
                    indices.Add(a); indices.Add(c); indices.Add(b);
                    indices.Add(b); indices.Add(c); indices.Add(d);
                }
        }
        mesh = new Mesh { name = "Boss feather" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

// The owl's voice and wings. Every clip is synthesised once at runtime, so there are no audio files.
public static class OwlAudio
{
    public enum Sound { Screech, Hoot, Whoosh, Boom, Slash, Hurt }
    const int Rate = 44100;
    static readonly AudioClip[] clips = new AudioClip[6];
    static AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { source = null; for (int i = 0; i < clips.Length; i++) clips[i] = null; }

    public static void Play(Sound sound, float volume = 1f, float pitch = 1f)
    {
        if (!Application.isPlaying) return;
        if (source == null)
        {
            var go = new GameObject("Owl audio");
            source = go.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.playOnAwake = false;
        }
        int index = (int)sound;
        if (clips[index] == null) clips[index] = Build(sound);
        source.pitch = pitch;
        source.PlayOneShot(clips[index], Mathf.Clamp01(volume));
    }

    static AudioClip Build(Sound sound)
    {
        float seconds = sound == Sound.Screech ? 1.0f : sound == Sound.Hoot ? 0.5f : sound == Sound.Whoosh ? 0.55f
            : sound == Sound.Boom ? 1.2f : sound == Sound.Slash ? 0.32f : 0.22f;
        int count = Mathf.RoundToInt(seconds * Rate);
        var data = new float[count];
        var random = new System.Random(17 + (int)sound);
        float phase = 0f, low = 0f, low2 = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / Rate, k = (float)i / count;
            float noise = (float)random.NextDouble() * 2f - 1f;
            float sample = 0f;
            switch (sound)
            {
                case Sound.Screech:
                {
                    // A rising, rasping cry: pitch sweeps up then falls, with a fast flutter and breath noise.
                    float f = Mathf.Lerp(1000f, 2500f, Mathf.Sin(k * Mathf.PI * 0.75f)) * (1f + 0.05f * Mathf.Sin(t * 180f));
                    phase += f / Rate;
                    float rasp = 0.55f + 0.45f * Mathf.Sin(t * 2f * Mathf.PI * 72f);
                    float voice = Mathf.Sin(phase * 2f * Mathf.PI) + 0.5f * Mathf.Sin(phase * 4f * Mathf.PI) + 0.3f * Mathf.Sin(phase * 6f * Mathf.PI);
                    low += (noise - low) * 0.5f;
                    float envelope = Mathf.Clamp01(k * 18f) * Mathf.Pow(1f - k, 1.4f);
                    sample = (voice * rasp * 0.5f + low * 0.35f) * envelope;
                    break;
                }
                case Sound.Hoot:
                {
                    phase += Mathf.Lerp(340f, 300f, k) / Rate;
                    float envelope = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                    sample = (Mathf.Sin(phase * 2f * Mathf.PI) + 0.22f * Mathf.Sin(phase * 4f * Mathf.PI)) * envelope * envelope * 0.8f;
                    break;
                }
                case Sound.Whoosh:
                {
                    // Air over a wing: noise through a filter that opens and closes.
                    float cutoff = 0.02f + 0.22f * Mathf.Sin(k * Mathf.PI);
                    low += (noise - low) * cutoff;
                    low2 += (low - low2) * 0.5f;
                    sample = low2 * Mathf.Pow(Mathf.Sin(k * Mathf.PI), 1.5f) * 2.2f;
                    break;
                }
                case Sound.Boom:
                {
                    phase += Mathf.Lerp(78f, 32f, Mathf.Sqrt(k)) / Rate;
                    low += (noise - low) * 0.08f;
                    sample = Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-k * 4.5f) + low * Mathf.Exp(-k * 14f) * 2.2f;
                    break;
                }
                case Sound.Slash:
                {
                    phase += Mathf.Lerp(2100f, 500f, k) / Rate;
                    low += (noise - low) * 0.6f;
                    sample = ((noise - low) * 0.9f + Mathf.Sin(phase * 2f * Mathf.PI) * 0.25f) * Mathf.Pow(1f - k, 2f) * Mathf.Clamp01(k * 40f);
                    break;
                }
                default:
                {
                    phase += Mathf.Lerp(170f, 70f, k) / Rate;
                    sample = Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-k * 5f) + noise * Mathf.Exp(-k * 30f) * 0.5f;
                    break;
                }
            }
            data[i] = Mathf.Clamp(sample, -1f, 1f) * 0.8f;
        }
        var clip = AudioClip.Create("Owl " + sound, count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

// Makes a hit on the player unmissable: a red flash around the screen edges, a camera kick and a thud.
// It only watches the player's last hit time; it never changes damage or health.
public sealed class DamageFeedback : MonoBehaviour
{
    static DamageFeedback instance;
    PlayerController player;
    Texture2D edges;
    float lastHit = -1f, flash;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register()
    {
        instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance != null) return;
        instance = new GameObject("Damage feedback").AddComponent<DamageFeedback>();
    }

    public static float Flash => instance != null ? instance.flash : 0f;

    void Awake()
    {
        // Clear in the middle, solid at the edges and strongest in the corners.
        const int size = 64;
        edges = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1.3f, Mathf.Sqrt(dx * dx + dy * dy)));
                edges.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        edges.Apply();
    }

    void Update()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        if (lastHit < 0f) lastHit = player.LastHitTime;
        if (player.LastHitTime != lastHit)
        {
            lastHit = player.LastHitTime;
            flash = 1f;
            ThirdPersonCamera.Shake(0.35f);
            OwlAudio.Play(OwlAudio.Sound.Hurt, 0.9f);
        }
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 2.6f);
    }

    void OnGUI()
    {
        if (flash <= 0.001f || Event.current.type != EventType.Repaint) return;
        Color keep = GUI.color;
        float punch = flash * flash;
        GUI.color = new Color(1f, 0.05f, 0.08f, 0.85f * punch);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), edges);
        // A very short full-screen blink on the first frames of the hit.
        GUI.color = new Color(1f, 0.25f, 0.25f, 0.16f * Mathf.Clamp01((flash - 0.75f) * 4f));
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = keep;
    }

    void OnDestroy()
    {
        if (edges != null) Destroy(edges);
        if (instance == this) instance = null;
    }
}

// Wing slash: while the owl's wings are open (Nightmare, or Phase 3), stepping in close makes it
// rake the area around it. A ring on the floor warns first; dashing out, or through it, avoids the hit.
public sealed class OwlWingSlash : MonoBehaviour
{
    public const float Trigger = 9.5f, Radius = 11f, Warning = 0.65f, Cooldown = 4.2f, Damage = 14f;
    EclipseBoss boss;
    float nextAt;
    bool busy;
    LineRenderer ring, inner;

    void Awake() { boss = GetComponent<EclipseBoss>(); }

    void Update()
    {
        if (busy || boss == null || !boss.Engaged || EclipseEncounter.Finished || DreamcatcherPresentation.CoversHud) return;
        if (boss.InTransformation || boss.Teleporting || boss.Stunned || EclipseVisual.Flight > 0.05f || Time.time < nextAt) return;
        bool wingsOpen = boss.Phase == 3 || WorldState.MixOrZero > 0.7f;
        if (!wingsOpen) return;
        Vector3 to = boss.PlayerPosition - boss.transform.position; to.y = 0f;
        if (to.sqrMagnitude > Trigger * Trigger) return;
        StartCoroutine(Slash());
    }

    IEnumerator Slash()
    {
        busy = true;
        Color tint = new Color(1f, 0.25f, 0.3f);
        Vector3 center = boss.transform.position; center.y = BossHazards.FloorY;
        ring = BossHazards.Polygon(center, Radius, 64, 0f, 0.18f, tint);
        inner = BossHazards.Polygon(center, 1f, 64, 0f, 0.1f, Color.white);
        if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.Tell("WING SLASH  /  GET CLEAR", Warning + 0.5f);
        OwlAudio.Play(OwlAudio.Sound.Screech, 0.55f, 1.35f);
        for (float t = 0f; t < Warning; t += Time.deltaTime)
        {
            float k = t / Warning;
            // The wings draw back while the inner ring grows out to the edge of the strike.
            EclipseVisual.Slash = -0.5f * k;
            center = boss.transform.position; center.y = BossHazards.FloorY;
            BossHazards.SetPolygon(ring, center, Radius, 64, 0f);
            BossHazards.SetPolygon(inner, center, Mathf.Lerp(1f, Radius, k * k), 64, 0f);
            BossHazards.Tint(ring, tint * (0.6f + k), 0.18f + 0.35f * k);
            if (EclipseEncounter.Finished || boss.InTransformation) break;
            yield return null;
        }
        BossHazards.Release(ring); BossHazards.Release(inner);
        if (!EclipseEncounter.Finished && !boss.InTransformation)
        {
            EclipseVisual.Slash = 1f;
            OwlAudio.Play(OwlAudio.Sound.Slash, 1f);
            OwlAudio.Play(OwlAudio.Sound.Whoosh, 0.9f, 1.3f);
            ThirdPersonCamera.Shake(0.4f);
            CombatPulse.Spawn(center + Vector3.up * 0.3f, Radius, tint);
            Fx.Burst(center + Vector3.up * 2f, tint, 60);
            BossHazards.HitCircle(center, Radius, Damage, false);
        }
        nextAt = Time.time + Cooldown;
        yield return new WaitForSeconds(0.45f);
        EclipseVisual.Slash = 0f;
        busy = false;
    }

    void OnDisable()
    {
        if (ring != null) BossHazards.Release(ring);
        if (inner != null) BossHazards.Release(inner);
        EclipseVisual.Slash = 0f;
        busy = false;
    }
}
