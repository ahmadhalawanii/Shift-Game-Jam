using UnityEngine;
using UnityEngine.Rendering;

// Look of the redesigned world: fog, ambient, key light, moon and post-processing for both dimensions.
// The Lucid/World shaders read the per-world values as globals and blend them per pixel with the wave,
// so fog and light roll over with the front. The real sun, Unity fog and the volumes follow the global mix
// (they also light the boss, the arena floor and other URP Lit objects).
[ExecuteAlways]
[DefaultExecutionOrder(-30)]
public class LucidWorldLook : MonoBehaviour
{
    public Light sun;
    public Transform landmark;              // the spirit tree; fog glows toward it in Dream
    public Volume dreamVolume;
    public Volume nightmareVolume;
    public Volume shiftVolume;              // chromatic aberration and distortion during a swap

    [Header("Key light")]
    [ColorUsage(false, true)] public Color keyDream = new Color(0.92f, 1.08f, 1.3f);
    [ColorUsage(false, true)] public Color keyNightmare = new Color(0.52f, 0.78f, 1f);
    public Vector3 sunEulerDream = new Vector3(42f, 172f, 0f);
    public Vector3 sunEulerNightmare = new Vector3(24f, 192f, 0f);
    public float sunIntensityDream = 1.1f;
    public float sunIntensityNightmare = 0.5f;
    public Vector3 moonDirection = new Vector3(0.1f, 0.42f, 1f);

    [Header("Fog (x density, y height falloff, z start distance, w max opacity)")]
    public Color fogDream = new Color(0.56f, 0.27f, 0.6f);
    public Color fogNightmare = new Color(0.09f, 0.175f, 0.29f);
    [ColorUsage(false, true)] public Color fogGlowDream = new Color(0.24f, 0.27f, 0.4f);
    [ColorUsage(false, true)] public Color fogGlowNightmare = new Color(0.12f, 0.2f, 0.28f);
    public Vector4 fogParamsDream = new Vector4(0.0058f, 0.02f, 12f, 0.9f);
    public Vector4 fogParamsNightmare = new Vector4(0.016f, 0.03f, 4f, 0.97f);

    [Header("Ambient")]
    public Color ambientSkyDream = new Color(0.3f, 0.28f, 0.62f);
    public Color ambientGroundDream = new Color(0.16f, 0.1f, 0.34f);
    public Color ambientSkyNightmare = new Color(0.1f, 0.17f, 0.29f);
    public Color ambientGroundNightmare = new Color(0.025f, 0.045f, 0.1f);

    [Header("Editor preview")]
    public bool previewNightmare;

    float eclipse;
    float tension;

    static readonly int FogColorDreamId = Shader.PropertyToID("_LucidFogColorDream");
    static readonly int FogColorNightId = Shader.PropertyToID("_LucidFogColorNight");
    static readonly int FogGlowDreamId = Shader.PropertyToID("_LucidFogGlowDream");
    static readonly int FogGlowNightId = Shader.PropertyToID("_LucidFogGlowNight");
    static readonly int FogParamsDreamId = Shader.PropertyToID("_LucidFogParamsDream");
    static readonly int FogParamsNightId = Shader.PropertyToID("_LucidFogParamsNight");
    static readonly int KeyDirId = Shader.PropertyToID("_LucidKeyDir");
    static readonly int KeyColorDreamId = Shader.PropertyToID("_LucidKeyColorDream");
    static readonly int KeyColorNightId = Shader.PropertyToID("_LucidKeyColorNight");
    static readonly int AmbientSkyDreamId = Shader.PropertyToID("_LucidAmbientSkyDream");
    static readonly int AmbientGroundDreamId = Shader.PropertyToID("_LucidAmbientGroundDream");
    static readonly int AmbientSkyNightId = Shader.PropertyToID("_LucidAmbientSkyNight");
    static readonly int AmbientGroundNightId = Shader.PropertyToID("_LucidAmbientGroundNight");
    static readonly int MoonDirId = Shader.PropertyToID("_LucidMoonDir");
    static readonly int EclipseId = Shader.PropertyToID("_LucidEclipse");

    void OnEnable() { Apply(); }
    void OnValidate() { if (!Application.isPlaying) Apply(); }
    void Update() { Apply(); }

    void Apply()
    {
        bool playing = Application.isPlaying;
        float mix = playing ? WorldState.MixOrZero : (previewNightmare ? 1f : 0f);
        float dt = playing ? Time.deltaTime : 0f;

        // Anticipation: while the boss telegraphs a swap, the fog pulls in and the frame tightens.
        var encounter = playing ? EclipseEncounter.Instance : null;
        var boss = encounter != null ? encounter.Boss : null;
        bool warning = boss != null && boss.Engaged && boss.SwapWarning;
        tension = playing ? Mathf.MoveTowards(tension, warning ? 1f : 0f, dt / 0.5f) : 0f;

        float wantEclipse = boss != null && boss.Engaged ? (boss.Phase == 1 ? 0.5f : boss.Phase == 2 ? 0.78f : 1f) : 0.12f;
        eclipse = playing ? Mathf.MoveTowards(eclipse, wantEclipse, dt * 0.25f) : 0.12f;

        Vector4 fd = fogParamsDream, fn = fogParamsNightmare;
        fd.x *= 1f + tension * 0.45f;
        fn.x *= 1f + tension * 0.3f;
        Shader.SetGlobalColor(FogColorDreamId, fogDream);
        Shader.SetGlobalColor(FogColorNightId, fogNightmare);
        Shader.SetGlobalColor(FogGlowDreamId, fogGlowDream);
        Shader.SetGlobalColor(FogGlowNightId, fogGlowNightmare);
        Shader.SetGlobalVector(FogParamsDreamId, fd);
        Shader.SetGlobalVector(FogParamsNightId, fn);
        Shader.SetGlobalColor(KeyColorDreamId, keyDream);
        Shader.SetGlobalColor(KeyColorNightId, keyNightmare);
        Shader.SetGlobalColor(AmbientSkyDreamId, ambientSkyDream);
        Shader.SetGlobalColor(AmbientGroundDreamId, ambientGroundDream);
        Shader.SetGlobalColor(AmbientSkyNightId, ambientSkyNightmare);
        Shader.SetGlobalColor(AmbientGroundNightId, ambientGroundNightmare);

        Vector3 moon = moonDirection.sqrMagnitude > 0.001f ? moonDirection.normalized : Vector3.up;
        Shader.SetGlobalVector(MoonDirId, moon);
        Shader.SetGlobalFloat(EclipseId, eclipse);

        // Fog glows toward the spirit tree in Dream and toward the moon in Nightmare.
        Vector3 key = moon;
        var cam = Camera.main;
        if (landmark != null && cam != null)
        {
            Vector3 toTree = (landmark.position + Vector3.up * 55f - cam.transform.position).normalized;
            key = Vector3.Slerp(toTree, moon, mix);
        }
        Shader.SetGlobalVector(KeyDirId, key);

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(sunEulerDream), Quaternion.Euler(sunEulerNightmare), mix);
            Color c = Color.Lerp(keyDream, keyNightmare, mix);
            float max = Mathf.Max(0.001f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
            sun.color = new Color(c.r / max, c.g / max, c.b / max);
            sun.intensity = Mathf.Lerp(sunIntensityDream, sunIntensityNightmare, mix);
        }

        // Unity's own fog and ambient, for objects that use stock URP shaders.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = Color.Lerp(fogDream, fogNightmare, mix);
        RenderSettings.fogDensity = Mathf.Lerp(fd.x, fn.x, mix);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(
            Color.Lerp(ambientGroundDream, ambientSkyDream, 0.7f),
            Color.Lerp(ambientGroundNightmare, ambientSkyNightmare, 0.7f), mix);

        if (dreamVolume != null) dreamVolume.weight = 1f - mix;
        if (nightmareVolume != null) nightmareVolume.weight = mix;
        if (shiftVolume != null)
        {
            float pulse = playing && WorldShift.Instance != null && WorldShift.Instance.Running ? WorldShift.Instance.SeamStrength : 0f;
            shiftVolume.weight = Mathf.Clamp01(pulse * 0.9f + tension * 0.25f);
        }
    }
}
