using System;
using UnityEngine;

// The world swap as a travelling wave. WorldState still decides which world is active; this turns each
// change into a front that expands from an origin, and publishes it to the Lucid/World shaders:
//   _WorldBlend, _ShiftOrigin, _ShiftRadius, _ShiftEdgeWidth, _ShiftFrom, _ShiftTo, _ShiftSeam.
// The same maths is available on the CPU (BlendAt, ArrivalIn) so colliders, hazards and lights
// change when the front reaches them.
[ExecuteAlways]
[DefaultExecutionOrder(-40)]
public class WorldShift : MonoBehaviour
{
    public static WorldShift Instance { get; private set; }

    // Fired when a wave starts: the world it brings, and where it starts.
    public static event Action<World, Vector3> Started;

    // Set before WorldState.Set to choose the origin of the next wave (scripted swaps, debug keys).
    public static Vector3? NextOrigin;
    // Slower, wider wave for the next swap only (boss phase changes).
    public static float NextSpeedScale = 1f;

    [Header("Wave")]
    [Min(1f)] public float crossSpeed = 42f;       // metres per second while the front crosses the play space
    [Min(0.1f)] public float crossSeconds = 1.2f;  // matches the boss swap telegraph
    [Min(0.1f)] public float runoutSeconds = 0.9f; // then the front accelerates to the horizon
    public float maxRadius = 560f;
    public float edgeWidth = 2.6f;
    [ColorUsage(false, true)] public Color seamDream = new Color(1.9f, 1.25f, 1.7f);
    [ColorUsage(false, true)] public Color seamNightmare = new Color(2.2f, 0.8f, 0.45f);
    [Range(0f, 1f)] public float shake = 0.22f;
    public float playerGlowRadius = 7f;

    [Header("Seam visuals (assigned by the world builder)")]
    public Mesh shellMesh;
    public Material shellMaterial;
    public Material sparkMaterial;

    [Header("Editor preview")]
    public bool previewNightmare;

    public bool Running { get; private set; }
    bool boundaryHeld;
    public void HoldBoundary(bool hold)
    {
        boundaryHeld = hold;
        if (hold)
        {
            Origin = EclipseEncounter.ArenaCenter + Vector3.left * 150f;
            Radius = 150f; from = 0f; to = 1f; seam = 1f; Running = true;
            EnsureVisuals(); Publish();
        }
        else { Settle(WorldState.CurrentOrDream == World.Dream ? 0f : 1f); Publish(); }
    }
    public Vector3 Origin { get; private set; }
    public float Radius { get; private set; } = 1e6f;
    public float Speed { get; private set; }
    // 1 while the front crosses the play space, fading to 0 as it runs out.
    public float SeamStrength { get { return seam; } }

    float from, to, elapsed, speedScale = 1f, seam;
    Vector3 skyDir = Vector3.up;
    Transform shell;
    ParticleSystem sparks;
    PlayerController player;

    static readonly int WorldBlendId = Shader.PropertyToID("_WorldBlend");
    static readonly int OriginId = Shader.PropertyToID("_ShiftOrigin");
    static readonly int RadiusId = Shader.PropertyToID("_ShiftRadius");
    static readonly int EdgeId = Shader.PropertyToID("_ShiftEdgeWidth");
    static readonly int FromId = Shader.PropertyToID("_ShiftFrom");
    static readonly int ToId = Shader.PropertyToID("_ShiftTo");
    static readonly int SeamId = Shader.PropertyToID("_ShiftSeam");
    static readonly int SpeedId = Shader.PropertyToID("_ShiftSpeed");
    static readonly int SeamDreamId = Shader.PropertyToID("_ShiftSeamColorDream");
    static readonly int SeamNightId = Shader.PropertyToID("_ShiftSeamColorNight");
    static readonly int SkyDirId = Shader.PropertyToID("_ShiftSkyDir");
    static readonly int SkyProgressId = Shader.PropertyToID("_ShiftSkyProgress");
    static readonly int PlayerPosId = Shader.PropertyToID("_LucidPlayerPos");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; Started = null; NextOrigin = null; NextSpeedScale = 1f; }

    void OnEnable()
    {
        Instance = this;
        WorldState.Changed += OnWorldChanged;
        Settle(Application.isPlaying ? (WorldState.CurrentOrDream == World.Nightmare ? 1f : 0f) : (previewNightmare ? 1f : 0f));
        Publish();
    }

    void OnDisable()
    {
        WorldState.Changed -= OnWorldChanged;
        if (Instance == this) Instance = null;
    }

    void OnValidate()
    {
        if (Application.isPlaying) return;
        Settle(previewNightmare ? 1f : 0f);
        Publish();
    }

    // No wave running: the whole world is in one state.
    void Settle(float state)
    {
        Running = false;
        from = to = state;
        Radius = 1e6f;
        seam = 0f;
        if (shell != null) shell.gameObject.SetActive(false);
    }

    void OnWorldChanged(World w)
    {
        if (!Application.isPlaying) return;
        // One front at a time: a swap during a running wave completes the old one at once.
        if (Running) Settle(to);
        to = w == World.Nightmare ? 1f : 0f;
        from = 1f - to;
        Origin = PickOrigin();
        speedScale = Mathf.Clamp(NextSpeedScale, 0.2f, 2f);
        NextOrigin = null;
        NextSpeedScale = 1f;
        Speed = crossSpeed * speedScale;
        elapsed = 0f;
        Radius = 0f;
        seam = 1f;
        Running = true;

        var cam = Camera.main;
        Vector3 toOrigin = cam != null ? Origin - cam.transform.position : Vector3.up;
        // A wave that starts at the player opens the sky from above; a distant one sweeps in from its side.
        skyDir = toOrigin.magnitude < 6f ? Vector3.up : (toOrigin.normalized + Vector3.up * 0.35f).normalized;

        ThirdPersonCamera.Shake(shake);
        EnsureVisuals();
        if (sparks != null)
        {
            sparks.transform.position = Origin;
            var main = sparks.main;
            main.startColor = to > 0.5f ? new Color(1f, 0.55f, 0.3f) : new Color(1f, 0.75f, 0.95f);
            sparks.Play();
        }
        Publish();
        Started?.Invoke(w, Origin);
    }

    Vector3 PickOrigin()
    {
        if (NextOrigin.HasValue) return NextOrigin.Value;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        Vector3 playerPos = player != null ? player.transform.position + Vector3.up : Vector3.zero;
        if (LucidInput.SwapStance.WasPressedThisFrame()) return playerPos; // the player shifted
        var encounter = EclipseEncounter.Instance;
        if (encounter != null && encounter.Boss != null && encounter.Boss.Engaged)
        {
            // The boss turned the world: the wave rolls out from under it.
            Vector3 b = encounter.Boss.transform.position;
            return new Vector3(b.x, EclipseEncounter.ArenaCenter.y + 1f, b.z);
        }
        return playerPos;
    }

    void Update()
    {
        if (boundaryHeld) { UpdateVisuals(); Publish(); return; }
        if (Application.isPlaying && Running)
        {
            elapsed += Time.deltaTime;
            float cross = crossSeconds / speedScale;
            float total = cross + runoutSeconds;
            float r = Speed * elapsed;
            if (elapsed > cross)
            {
                float k = (maxRadius - Speed * total) / (runoutSeconds * runoutSeconds);
                float over = elapsed - cross;
                r += Mathf.Max(0f, k) * over * over;
                seam = Mathf.Clamp01(1f - over / runoutSeconds);
            }
            Radius = r;
            if (elapsed >= total) Settle(to);
        }
        else if (!Application.isPlaying) Settle(previewNightmare ? 1f : 0f);
        UpdateVisuals();
        Publish();
    }

    void Publish()
    {
        Shader.SetGlobalFloat(WorldBlendId, Application.isPlaying ? WorldState.MixOrZero : (previewNightmare ? 1f : 0f));
        Shader.SetGlobalVector(OriginId, Origin);
        Shader.SetGlobalFloat(RadiusId, Radius);
        Shader.SetGlobalFloat(EdgeId, edgeWidth);
        Shader.SetGlobalFloat(FromId, from);
        Shader.SetGlobalFloat(ToId, to);
        Shader.SetGlobalFloat(SeamId, seam);
        Shader.SetGlobalFloat(SpeedId, Mathf.Max(1f, Speed > 0f ? Speed : crossSpeed));
        Shader.SetGlobalColor(SeamDreamId, seamDream);
        Shader.SetGlobalColor(SeamNightId, seamNightmare);
        Shader.SetGlobalVector(SkyDirId, skyDir);
        float skyProgress = Running ? Mathf.Clamp01(elapsed / (crossSeconds / speedScale + runoutSeconds * 0.6f)) : 1f;
        Shader.SetGlobalFloat(SkyProgressId, skyProgress);

        if (Application.isPlaying)
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                Vector3 p = player.transform.position + Vector3.up;
                Shader.SetGlobalVector(PlayerPosId, new Vector4(p.x, p.y, p.z, playerGlowRadius));
            }
        }
        else Shader.SetGlobalVector(PlayerPosId, new Vector4(0f, -9999f, 0f, playerGlowRadius));
    }

    // ---------- Queries ----------

    // Local world state at a point: 0 Dream, 1 Nightmare. Matches the shaders (without the wobble).
    public static float BlendAt(Vector3 position)
    {
        var s = Instance;
        if (s == null) return WorldState.CurrentOrDream == World.Nightmare ? 1f : 0f;
        if (!s.Running) return s.to;
        float x = Vector3.Distance(position, s.Origin) - s.Radius;
        float inside = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-s.edgeWidth, 0f, x));
        return Mathf.Lerp(s.from, s.to, inside);
    }

    // Same, eased over `seconds` behind the front (matches the vertex morphs).
    public static float BlendSoftAt(Vector3 position, float seconds)
    {
        var s = Instance;
        if (s == null) return WorldState.CurrentOrDream == World.Nightmare ? 1f : 0f;
        if (!s.Running) return s.to;
        float width = Mathf.Max(s.Speed * seconds, 0.5f);
        float x = Vector3.Distance(position, s.Origin) - s.Radius;
        float inside = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-width, 0f, x));
        inside = inside * inside * (3f - 2f * inside);
        return Mathf.Lerp(s.from, s.to, inside);
    }

    public static World WorldAt(Vector3 position) { return BlendAt(position) > 0.5f ? World.Nightmare : World.Dream; }

    // Seconds until the running front reaches a point. 0 when it has passed or no wave is running.
    public static float ArrivalIn(Vector3 position)
    {
        var s = Instance;
        if (s == null || !s.Running) return 0f;
        float gap = Vector3.Distance(position, s.Origin) - s.Radius;
        return gap <= 0f ? 0f : gap / Mathf.Max(1f, s.Speed);
    }

    // The world the running wave is bringing (or the settled world).
    public static World Target { get { return Instance != null ? (Instance.to > 0.5f ? World.Nightmare : World.Dream) : WorldState.CurrentOrDream; } }

    // ---------- Seam visuals ----------

    void EnsureVisuals()
    {
        if (shell == null && shellMesh != null && shellMaterial != null)
        {
            var go = new GameObject("Shift seam shell");
            go.hideFlags = HideFlags.DontSave;
            go.AddComponent<MeshFilter>().sharedMesh = shellMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = shellMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            shell = go.transform;
        }
        if (sparks == null && sparkMaterial != null)
        {
            var go = new GameObject("Shift seam sparks");
            go.hideFlags = HideFlags.DontSave;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // circle lies flat
            sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
            main.gravityModifier = -0.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 900;
            var emission = sparks.emission;
            emission.rateOverTime = 0f;
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radiusThickness = 0f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            var col = sparks.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = sparkMaterial;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    void UpdateVisuals()
    {
        if (!Application.isPlaying) return;
        bool show = Running && (boundaryHeld || Radius < 140f);
        if (shell != null)
        {
            shell.gameObject.SetActive(show);
            if (show)
            {
                shell.position = Origin;
                shell.localScale = Vector3.one * Mathf.Max(0.01f, Radius - edgeWidth * 0.5f) * 2f;
            }
        }
        if (sparks != null)
        {
            var emission = sparks.emission;
            bool emit = Running && Radius < 70f;
            emission.rateOverTime = emit ? Mathf.Clamp(Radius * 14f, 60f, 520f) : 0f;
            if (emit)
            {
                var shape = sparks.shape;
                shape.radius = Mathf.Max(0.1f, Radius);
            }
            else if (!Running && sparks.isPlaying && sparks.particleCount == 0) sparks.Stop();
        }
    }

    void OnDestroy()
    {
        if (shell != null) DestroyImmediate(shell.gameObject);
        if (sparks != null) DestroyImmediate(sparks.gameObject);
    }
}
