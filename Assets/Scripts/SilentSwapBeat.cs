using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SilentSwapBeat : MonoBehaviour
{
    EclipseBoss boss;
    AudioMixerSnapshot open, held;
    ParticleSystem[] particles;
    float[] speeds;
    Volume volume;
    VolumeProfile profile;
    Vignette vignette;
    Bloom bloom;
    bool quiet;
    float nextPulse;
    void Start()
    {
        boss = GetComponent<EclipseBoss>();
        var mixer = Resources.Load<AudioMixer>("LucidCombat");
        if (mixer != null) { open = mixer.FindSnapshot("Open"); held = mixer.FindSnapshot("Held Breath"); }
        particles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None); speeds = new float[particles.Length];
        for (int i=0;i<particles.Length;i++) speeds[i]=particles[i].main.simulationSpeed;
        var go = new GameObject("Swap breath grading"); go.transform.SetParent(transform);
        volume=go.AddComponent<Volume>();volume.isGlobal=true;volume.priority=110;volume.weight=0;
        profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;
        vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.32f);vignette.smoothness.Override(.7f);
        bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.15f);
    }
    void Update()
    {
        bool next=boss.Engaged&&!EclipseEncounter.Finished&&boss.SwapWarning&&boss.SwapSeconds<=.5f;
        if(next!=quiet)
        {
            quiet=next;
            if (quiet)
            {
                particles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None); speeds = new float[particles.Length];
                for (int i = 0; i < particles.Length; i++) speeds[i] = particles[i].main.simulationSpeed;
            }
            var snapshot=quiet?held:open;if(snapshot!=null)snapshot.TransitionTo(.08f);
            for(int i=0;i<particles.Length;i++)if(particles[i]!=null)
            { var main=particles[i].main;main.simulationSpeed=speeds[i] * (quiet ? .3f : 1f); }
        }
        volume.weight=Mathf.MoveTowards(volume.weight,quiet?1f:0f,Time.deltaTime*8f);
        if(boss.SwapWarning&&Time.time>=nextPulse)
        {nextPulse=Time.time+.35f;LucidAudio.Sfx("heartbeat");}
    }
    void OnDisable()
    {
        if(open!=null)open.TransitionTo(0f);
        if(particles!=null)for(int i=0;i<particles.Length;i++)if(particles[i]!=null){var main=particles[i].main;main.simulationSpeed=speeds[i];}
        if(volume!=null)volume.weight=0;
    }
    void OnDestroy(){if(profile!=null)Destroy(profile);}
}
