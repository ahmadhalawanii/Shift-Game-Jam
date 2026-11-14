using UnityEngine;

// Small synthesized cues, cached for the lifetime of the Level 1 encounter.
public sealed class LucidAudio : MonoBehaviour
{
    public enum Cue { Thread, Charge, Burst, Capture, Shift, Warning, Fusion, Victory }
    static LucidAudio instance;
    AudioSource source;
    readonly AudioClip[] clips = new AudioClip[8];
    readonly AudioClip[] resonance = new AudioClip[5];
    readonly System.Collections.Generic.Dictionary<string, AudioClip> hooks = new System.Collections.Generic.Dictionary<string, AudioClip>();
    readonly System.Collections.Generic.List<AudioClip> owned = new System.Collections.Generic.List<AudioClip>();
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

    // Rising combo tone, one clip per Resonance level.
    public static void PlayResonance(int level)
    {
        if (instance == null) return;
        int i = Mathf.Clamp(level, 1, 5) - 1;
        if (instance.resonance[i] == null) instance.resonance[i] = Tone("resonance" + level, 0.22f, 440f * Mathf.Pow(1.26f, i), 520f * Mathf.Pow(1.26f, i));
        instance.source.PlayOneShot(instance.resonance[i], 0.42f);
    }

    // Named audio hooks: atk_<name>_telegraph / _fire / _impact, boss_teleport_out/in, boss_swap, boss_stagger.
    // A clip at Resources/Audio/<id> overrides the synthesized placeholder. Pass cached id strings.
    public static void Hook(string id)
    {
        if (instance == null || string.IsNullOrEmpty(id)) return;
        if (!instance.hooks.TryGetValue(id, out var clip))
        {
            clip = Resources.Load<AudioClip>("Audio/" + id);
            if (clip == null)
            {
                // A stable, distinct tone per id: the hash picks the pitch, the suffix picks the shape.
                int h = id.GetHashCode() & 0x7fffffff;
                float baseHz = 140f + (h % 37) * 14f;
                if (id.EndsWith("_telegraph")) clip = Tone(id, 0.3f, baseHz, baseHz * 1.5f);
                else if (id.EndsWith("_impact")) clip = Tone(id, 0.38f, baseHz * 0.8f, baseHz * 0.3f);
                else if (id.EndsWith("_out")) clip = Tone(id, 0.45f, 520f, 120f);
                else if (id.EndsWith("_in")) clip = Tone(id, 0.35f, 90f, 360f);
                else clip = Tone(id, 0.2f, baseHz * 2f, baseHz * 1.2f);
                instance.owned.Add(clip);
            }
            instance.hooks[id] = clip;
        }
        if (clip != null) instance.source.PlayOneShot(clip, 0.36f);
    }

    static AudioClip Tone(string name, float duration, float first, float last)
    {
        int count = (int)(Rate * duration);
        var data = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            phase += 2f * Mathf.PI * Mathf.Lerp(first, last, t) / Rate;
            float envelope = Mathf.Min(1f, t * 35f) * (1f - t) * (1f - t);
            data[i] = envelope * (Mathf.Sin(phase) * 0.44f + Mathf.Sin(phase * 2f) * 0.12f);
        }
        var clip = AudioClip.Create(name, count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
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
        foreach (var clip in resonance) if (clip != null) Destroy(clip);
        foreach (var clip in owned) if (clip != null) Destroy(clip);
    }
}
