using UnityEngine;

// Small synthesized cues, cached for the lifetime of the Level 1 encounter.
public sealed class LucidAudio : MonoBehaviour
{
    public enum Cue { Thread, Charge, Burst, Capture, Shift, Warning, Fusion, Victory }
    static LucidAudio instance;
    AudioSource source;
    readonly AudioClip[] clips = new AudioClip[8];
    const int Rate = 24000;

    void Awake()
    {
        instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    public static void Play(Cue cue)
    {
        if (instance == null) return;
        int index = (int)cue;
        if (instance.clips[index] == null) instance.clips[index] = Create(cue);
        instance.source.PlayOneShot(instance.clips[index], cue == Cue.Thread ? 0.25f : 0.48f);
    }

    static AudioClip Create(Cue cue)
    {
        float duration, first, last;
        switch (cue)
        {
            case Cue.Thread: duration = 0.11f; first = 720f; last = 320f; break;
            case Cue.Charge: duration = 0.42f; first = 135f; last = 275f; break;
            case Cue.Burst: duration = 0.48f; first = 320f; last = 52f; break;
            case Cue.Capture: duration = 0.24f; first = 640f; last = 1280f; break;
            case Cue.Shift: duration = 0.5f; first = 250f; last = 82f; break;
            case Cue.Warning: duration = 0.28f; first = 220f; last = 170f; break;
            case Cue.Fusion: duration = 0.85f; first = 95f; last = 230f; break;
            default: duration = 0.95f; first = 392f; last = 588f; break;
        }
        int count = (int)(Rate * duration);
        var data = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            phase += 2f * Mathf.PI * Mathf.Lerp(first, last, t) / Rate;
            float envelope = Mathf.Min(1f, t * 35f) * (1f - t) * (1f - t);
            data[i] = envelope * (Mathf.Sin(phase) * 0.44f + Mathf.Sin(phase * 1.5f) * 0.19f);
        }
        var clip = AudioClip.Create(cue.ToString(), count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        foreach (var clip in clips) if (clip != null) Destroy(clip);
    }
}
