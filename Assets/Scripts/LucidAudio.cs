using UnityEngine;

// Small synthesized cues, cached for the lifetime of the Level 1 encounter.
public sealed class LucidAudio : MonoBehaviour
{
    public enum Cue { Thread, Charge, Burst, Capture, Shift, Warning, Fusion, Victory, DashDream, DashNightmare }
    static LucidAudio instance;
    AudioSource source;
    AudioSource worldSource;
    readonly AudioClip[] clips = new AudioClip[10];
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
        worldSource = gameObject.AddComponent<AudioSource>();
        worldSource.playOnAwake = false; worldSource.spatialBlend = 0f;
        var mixer = Resources.Load<UnityEngine.Audio.AudioMixer>("LucidCombat");
        if (mixer != null)
        {
            var groups = mixer.FindMatchingGroups("Player"); if (groups.Length > 0) source.outputAudioMixerGroup = groups[0];
            var world = mixer.FindMatchingGroups("World"); if (world.Length > 0) worldSource.outputAudioMixerGroup = world[0];
        }
    }

    public static void Play(Cue cue)
    {
        if (instance == null) return;
        int index = (int)cue;
        if (instance.clips[index] == null) instance.clips[index] = cue >= Cue.DashDream ? Dash(cue == Cue.DashNightmare) : Create(cue);
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
        if (clip != null) instance.worldSource.PlayOneShot(clip, 0.36f);
    }

    // Weapon and UI sounds: a noise transient over a sine sweep with a decaying tail. Cached by id.
    public static void Sfx(string id)
    {
        if (instance == null) return;
        if (!instance.hooks.TryGetValue(id, out var clip))
        {
            float dur = 0.1f, f0 = 800f, f1 = 600f, noise = 0.2f, vol = 0.4f;
            switch (id)
            {
                case "revolver": dur = 0.26f; f0 = 210f; f1 = 55f; noise = 0.8f; break;
                case "scatter": dur = 0.42f; f0 = 130f; f1 = 38f; noise = 1.2f; break;
                case "chain": dur = 0.13f; f0 = 310f; f1 = 270f; noise = 0.12f; break;
                case "bow": dur = 0.3f; f0 = 900f; f1 = 1800f; noise = 0.1f; break;
                case "reload": dur = 0.09f; f0 = 950f; f1 = 700f; noise = 0.3f; break;
                case "reload_perfect": dur = 0.3f; f0 = 880f; f1 = 1760f; noise = 0.05f; break;
                case "empty": dur = 0.06f; f0 = 1300f; f1 = 900f; noise = 0.4f; break;
                case "hit": dur = 0.05f; f0 = 2100f; f1 = 1700f; noise = 0.1f; break;
                case "swap": dur = 0.08f; f0 = 500f; f1 = 760f; noise = 0.15f; break;
                case "seed": dur = 0.22f; f0 = 1046f; f1 = 1568f; noise = 0f; break;
                case "perfect": dur = 0.45f; f0 = 1318f; f1 = 2637f; noise = 0.05f; break;
                case "ward_break": dur = 0.5f; f0 = 600f; f1 = 80f; noise = 0.6f; break;
                case "heartbeat": dur = 0.3f; f0 = 70f; f1 = 45f; noise = 0f; break;
            }
            int count = (int)(Rate * dur);
            var data = new float[count];
            var random = new System.Random(id.GetHashCode());
            float phase = 0f, lp = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, Mathf.Sqrt(t)) / Rate;
                lp += ((float)random.NextDouble() * 2f - 1f - lp) * Mathf.Lerp(0.9f, 0.15f, t);
                float body = Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase * 0.5f) * 0.25f;
                data[i] = Mathf.Clamp((body * (1f - t) * (1f - t) + lp * noise * Mathf.Exp(-t * 9f)) * Mathf.Min(1f, t * 80f), -1f, 1f);
            }
            clip = AudioClip.Create(id, count, 1, Rate, false);
            clip.SetData(data, 0);
            instance.owned.Add(clip);
            instance.hooks[id] = clip;
        }
        instance.source.PlayOneShot(clip, id == "chain" ? 0.2f : 0.45f);
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

    // Smoke dash: a breath of filtered noise that opens and closes.
    // Dream adds a soft two-note chime, Nightmare a low thump under a darker whoosh.
    static AudioClip Dash(bool nightmare)
    {
        float duration = nightmare ? 0.5f : 0.42f;
        int count = (int)(Rate * duration);
        var data = new float[count];
        var random = new System.Random(nightmare ? 7 : 3);
        float low = 0f, band = 0f, phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float sec = (float)i / Rate;
            // State-variable filter swept up then down: the whoosh.
            float sweep = Mathf.Sin(Mathf.Pow(t, 0.6f) * Mathf.PI);
            float cutoff = nightmare ? Mathf.Lerp(180f, 1500f, sweep) : Mathf.Lerp(700f, 4200f, sweep);
            float f = 2f * Mathf.Sin(Mathf.PI * cutoff / Rate);
            float noise = (float)random.NextDouble() * 2f - 1f;
            low += f * band;
            band += f * (noise - low - 0.5f * band);
            float whoosh = band * sweep * (nightmare ? 0.75f : 0.5f);
            float tone;
            if (nightmare)
            {
                phase += 2f * Mathf.PI * Mathf.Lerp(95f, 38f, Mathf.Min(1f, sec / 0.18f)) / Rate;
                tone = Mathf.Sin(phase) * Mathf.Exp(-sec * 11f) * 0.9f;
            }
            else
            {
                tone = (Mathf.Sin(2f * Mathf.PI * 1318.5f * sec) * Mathf.Exp(-sec * 9f)
                    + Mathf.Sin(2f * Mathf.PI * 1975.5f * Mathf.Max(0f, sec - 0.06f)) * Mathf.Exp(-Mathf.Max(0f, sec - 0.06f) * 8f) * (sec > 0.06f ? 0.7f : 0f)) * 0.22f;
            }
            data[i] = Mathf.Clamp(whoosh + tone, -1f, 1f) * Mathf.Min(1f, t * 60f);
        }
        var clip = AudioClip.Create(nightmare ? "DashNightmare" : "DashDream", count, 1, Rate, false);
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
