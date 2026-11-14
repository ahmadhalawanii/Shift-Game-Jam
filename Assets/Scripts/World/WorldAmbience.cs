using UnityEngine;

// Ambience for both worlds, synthesised so the project carries no audio files for it.
// Dream: a warm open chord that breathes. Nightmare: a low drone with a dissonant partner and wind.
// The two crossfade with the world mix, and every swap adds a rising swell.
[RequireComponent(typeof(AudioSource))]
public class WorldAmbience : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 0.16f;
    [Range(0f, 1f)] public float swellVolume = 0.35f;

    static readonly float[] DreamNotes = { 174.61f, 220f, 261.63f, 329.63f, 392f }; // F major 7 add 9, open
    static readonly float[] NightNotes = { 55f, 58.27f, 82.41f, 110f };              // low A with a minor second

    readonly double[] dreamPhase = new double[5];
    readonly double[] nightPhase = new double[4];
    double lfo, swellPhase;
    float mix, swell, swellAge = 9f;
    float sampleRate = 48000f;
    float noiseState, windState;
    uint rng = 0x9E3779B9u;
    volatile float targetMix;
    volatile bool swellRequest;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
        var source = GetComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.loop = true;
        source.playOnAwake = true;
        if (!source.isPlaying) source.Play();
    }

    void OnEnable() { WorldShift.Started += OnShift; }
    void OnDisable() { WorldShift.Started -= OnShift; }
    void OnShift(World w, Vector3 origin) { swellRequest = true; }

    void Update() { targetMix = WorldState.MixOrZero; }

    float Noise()
    {
        rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
        return (rng & 0xFFFF) / 32768f - 1f;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (swellRequest) { swellRequest = false; swellAge = 0f; }
        float dt = 1f / sampleRate;
        const double Tau = System.Math.PI * 2.0;
        for (int i = 0; i < data.Length; i += channels)
        {
            mix += (targetMix - mix) * 0.00008f;
            lfo += dt;
            float breathe = 0.75f + 0.25f * Mathf.Sin((float)(lfo * 0.35));

            float dream = 0f;
            for (int k = 0; k < DreamNotes.Length; k++)
            {
                dreamPhase[k] += DreamNotes[k] * dt * (1.0 + 0.002 * System.Math.Sin(lfo * (0.2 + k * 0.07)));
                if (dreamPhase[k] > 1.0) dreamPhase[k] -= 1.0;
                dream += Mathf.Sin((float)(dreamPhase[k] * Tau)) * (0.5f + 0.5f * Mathf.Sin((float)(lfo * (0.13 + k * 0.05) + k)));
            }
            dream *= 0.2f * breathe;

            float night = 0f;
            for (int k = 0; k < NightNotes.Length; k++)
            {
                nightPhase[k] += NightNotes[k] * dt;
                if (nightPhase[k] > 1.0) nightPhase[k] -= 1.0;
                float s = Mathf.Sin((float)(nightPhase[k] * Tau));
                night += s + 0.3f * s * s * s; // a little grit
            }
            float white = Noise();
            windState += (white - windState) * (0.01f + 0.008f * Mathf.Sin((float)(lfo * 0.22)));
            night = night * 0.2f + windState * 1.6f;

            // Swell: filtered noise and a rising tone over 1.4 seconds.
            float sw = 0f;
            if (swellAge < 1.6f)
            {
                swellAge += dt;
                float k = swellAge / 1.4f;
                float env = Mathf.Clamp01(k * 1.6f) * Mathf.Clamp01((1.6f - swellAge) * 3f);
                noiseState += (white - noiseState) * Mathf.Lerp(0.02f, 0.35f, Mathf.Clamp01(k));
                swellPhase += Mathf.Lerp(110f, 440f, k * k) * dt;
                if (swellPhase > 1.0) swellPhase -= 1.0;
                sw = (noiseState * 1.2f + Mathf.Sin((float)(swellPhase * Tau)) * 0.35f) * env * swellVolume;
            }

            float sample = (dream * (1f - mix) + night * mix) * volume + sw;
            for (int c = 0; c < channels; c++) data[i + c] += sample;
        }
    }
}
