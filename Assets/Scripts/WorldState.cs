using System;
using UnityEngine;

public enum World { Dream, Nightmare }

// Single source of truth for which world is active. Swap() flips it; Mix eases 0 (Dream) to 1 (Nightmare).
public class WorldState : MonoBehaviour
{
    public static WorldState Instance { get; private set; }
    public static event Action<World> Changed;
    // Visual-only objects switch under the transition crest instead of popping on input.
    public static event Action<World> VisualChanged;

    public World Current { get; private set; } = World.Dream;
    public float Mix { get; private set; }

    [Header("Tuning")]
    [Min(0.05f)] public float transitionTime = 0.72f;
    public float swapCooldown = 0.4f;
    [Range(0f, 1f)] public float transitionShake = 0.22f;
    public WorldLook look;

    float target;
    float startMix;
    float transitionElapsed;
    float transitionDuration;
    float transitionDirection;
    float lastSwap = -99f;
    bool transitioning;
    bool peakFired;

    void Awake()
    {
        Instance = this;
        target = Current == World.Nightmare ? 1f : 0f;
        Mix = target;
        if (look != null) look.Apply(Mix, 0f, 0f);
    }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public bool Swap()
    {
        if (Time.time - lastSwap < swapCooldown) return false;
        Set(Current == World.Dream ? World.Nightmare : World.Dream);
        return true;
    }

    public void Set(World w)
    {
        if (w == Current) return;
        Current = w;
        LucidAudio.Play(LucidAudio.Cue.Shift);
        target = w == World.Nightmare ? 1f : 0f;
        startMix = Mix;
        transitionElapsed = 0f;
        transitionDuration = transitionTime * Mathf.Max(0.15f, Mathf.Abs(target - startMix));
        transitionDirection = target > startMix ? 1f : -1f;
        transitioning = true;
        peakFired = false;
        lastSwap = Time.time;
        if (Changed != null) Changed(w);
    }

    void Update()
    {
        float pulse = 0f;

        if (transitioning)
        {
            transitionElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(transitionElapsed / Mathf.Max(0.01f, transitionDuration));
            float eased = SmootherStep(t);
            Mix = Mathf.LerpUnclamped(startMix, target, eased);

            // A soft crest at the midpoint makes the change read without covering the action.
            pulse = Mathf.Sin(t * Mathf.PI);
            pulse *= pulse;

            if (!peakFired && t >= 0.5f)
            {
                peakFired = true;
                if (VisualChanged != null) VisualChanged(Current);
                ThirdPersonCamera.Shake(transitionShake);
            }

            if (t >= 1f)
            {
                Mix = target;
                transitioning = false;
                pulse = 0f;
            }
        }

        if (look != null) look.Apply(Mix, pulse, transitionDirection);
    }

    static float SmootherStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
