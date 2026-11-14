using UnityEngine;

// A point light with one look per world. It changes when the wave reaches it, not when the swap is pressed.
[RequireComponent(typeof(Light))]
public class WorldLight : MonoBehaviour
{
    public Color dream = new Color(0.75f, 0.9f, 1f);
    public Color nightmare = new Color(1f, 0.4f, 0.2f);
    public float dreamIntensity = 3f;
    public float nightmareIntensity = 1.2f;
    [Range(0f, 1f)] public float nightmareFlicker = 0.25f;
    public float response = 4f;

    Light lamp;
    float blend;
    float seed;

    void Awake()
    {
        lamp = GetComponent<Light>();
        seed = Random.value * 50f;
        blend = WorldShift.BlendAt(transform.position);
    }

    void Update()
    {
        blend = Mathf.MoveTowards(blend, WorldShift.BlendAt(transform.position), response * Time.deltaTime);
        float flicker = 1f - nightmareFlicker * blend * Mathf.PerlinNoise(Time.time * 7f, seed);
        lamp.color = Color.Lerp(dream, nightmare, blend);
        lamp.intensity = Mathf.Lerp(dreamIntensity, nightmareIntensity, blend) * flicker;
    }
}
