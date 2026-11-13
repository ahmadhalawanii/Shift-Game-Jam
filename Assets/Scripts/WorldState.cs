using System;
using UnityEngine;

public enum World { Dream, Nightmare }

// Single source of truth for which world is active. Swap() flips it; Mix eases 0 (Dream) to 1 (Nightmare).
// Runs early so other scripts see a valid Instance in their own Awake and OnEnable.
[DefaultExecutionOrder(-100)]
public class WorldState : MonoBehaviour
{
    public static WorldState Instance { get; private set; }
    public static event Action<World> Changed;

    // Safe to read before the scene is set up.
    public static World CurrentOrDream { get { return Instance != null ? Instance.Current : World.Dream; } }
    public static float MixOrZero { get { return Instance != null ? Instance.Mix : 0f; } }

    public World Current { get; private set; } = World.Dream;
    public float Mix { get; private set; }

    [Header("Tuning")]
    public float transitionTime = 0.6f;
    public float swapCooldown = 0.4f;
    public WorldLook look;

    float lastSwap = -99f;
    float appliedMix = -1f;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Swap()
    {
        if (Time.time - lastSwap < swapCooldown) return;
        lastSwap = Time.time;
        Current = Current == World.Dream ? World.Nightmare : World.Dream;
        if (Changed != null) Changed(Current);
    }

    void Update()
    {
        float target = Current == World.Nightmare ? 1f : 0f;
        Mix = Mathf.MoveTowards(Mix, target, Time.deltaTime / Mathf.Max(0.01f, transitionTime));
        if (look != null && Mathf.Abs(Mix - appliedMix) > 1e-4f)
        {
            look.Apply(Mix);
            appliedMix = Mix;
        }
    }
}
