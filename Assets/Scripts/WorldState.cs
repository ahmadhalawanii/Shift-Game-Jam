using System;
using UnityEngine;

public enum World { Dream, Nightmare }

// Single source of truth for which world is active. Swap() flips it; Mix eases 0 (Dream) to 1 (Nightmare).
public class WorldState : MonoBehaviour
{
    public static WorldState Instance { get; private set; }
    public static event Action<World> Changed;

    public World Current { get; private set; } = World.Dream;
    public float Mix { get; private set; }

    [Header("Tuning")]
    public float transitionTime = 0.6f;
    public float swapCooldown = 0.4f;
    public WorldLook look;

    float target;
    float lastSwap = -99f;

    void Awake() { Instance = this; }
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
        target = w == World.Nightmare ? 1f : 0f;
        lastSwap = Time.time;
        if (Changed != null) Changed(w);
    }

    void Update()
    {
        Mix = Mathf.MoveTowards(Mix, target, Time.deltaTime / Mathf.Max(0.01f, transitionTime));
        if (look != null) look.Apply(Mix);
    }
}
