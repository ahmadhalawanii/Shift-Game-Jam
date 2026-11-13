using UnityEngine;
using UnityEngine.Rendering;

// Blends the global look between Dream and Nightmare: sky, fog, ambient, sun, post-processing.
// Tune the public fields per level to give each level its own palette.
public class WorldLook : MonoBehaviour
{
    [Header("Sky (uses Lucid/DreamSky)")]
    public Material sky;
    public Vector3 voidDirection = new Vector3(0.28f, 0.34f, 0.9f);
    public float voidSize = 0.16f;

    [Header("Light")]
    public Light sun;
    public Color sunDream = new Color(1f, 0.71f, 0.54f);
    public Color sunNightmare = new Color(1f, 0.3f, 0.12f);
    public float sunIntensityDream = 1.1f;
    public float sunIntensityNightmare = 0.5f;

    [Header("Fog and ambient")]
    public Color fogDream = new Color(0.95f, 0.65f, 0.8f);
    public Color fogNightmare = new Color(0.18f, 0.04f, 0.03f);
    public float fogEndDream = 150f;
    public float fogEndNightmare = 70f;
    public Color ambientDream = new Color(0.69f, 0.58f, 0.82f);
    public Color ambientNightmare = new Color(0.16f, 0.05f, 0.05f);

    [Header("Post-processing")]
    public Volume dreamVolume;
    public Volume nightmareVolume;

    Material runtimeSky;
    float lastEnv = -1f;

    void Awake()
    {
        if (sky != null)
        {
            runtimeSky = new Material(sky); // runtime copy so the asset is never modified
            RenderSettings.skybox = runtimeSky;
            runtimeSky.SetVector("_VoidDir", new Vector4(voidDirection.x, voidDirection.y, voidDirection.z, 0));
            runtimeSky.SetFloat("_VoidSize", voidSize);
        }
    }

    public void Apply(float mix)
    {
        if (runtimeSky != null) runtimeSky.SetFloat("_Mix", mix);
        if (Mathf.Abs(mix - lastEnv) > 0.2f || (mix == 0f && lastEnv != 0f) || (mix == 1f && lastEnv != 1f)) { lastEnv = mix; DynamicGI.UpdateEnvironment(); }
        RenderSettings.fogColor = Color.Lerp(fogDream, fogNightmare, mix);
        RenderSettings.fogEndDistance = Mathf.Lerp(fogEndDream, fogEndNightmare, mix);
        RenderSettings.ambientLight = Color.Lerp(ambientDream, ambientNightmare, mix);
        if (sun != null)
        {
            sun.color = Color.Lerp(sunDream, sunNightmare, mix);
            sun.intensity = Mathf.Lerp(sunIntensityDream, sunIntensityNightmare, mix);
        }
        if (dreamVolume != null) dreamVolume.weight = 1f - mix;
        if (nightmareVolume != null) nightmareVolume.weight = mix;
    }
}
