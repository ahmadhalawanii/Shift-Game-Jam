using UnityEngine;

public enum WorldVisibility { Both, DreamOnly, NightmareOnly }

// Put on anything that only exists in one world (platforms, hazards, doors, props).
// Hides renderers and lights, and turns colliders off, when the other world is active.
public class WorldObject : MonoBehaviour
{
    public WorldVisibility visibleIn = WorldVisibility.Both;
    public bool affectColliders = true;

    Renderer[] renderers;
    Collider[] colliders;
    Light[] lights;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        lights = GetComponentsInChildren<Light>(true);
    }

    void OnEnable()
    {
        WorldState.VisualChanged += Apply;
        Apply(WorldState.Instance != null ? WorldState.Instance.Current : World.Dream);
    }

    void OnDisable() { WorldState.VisualChanged -= Apply; }

    void Apply(World w)
    {
        bool on = visibleIn == WorldVisibility.Both
            || (visibleIn == WorldVisibility.DreamOnly && w == World.Dream)
            || (visibleIn == WorldVisibility.NightmareOnly && w == World.Nightmare);
        foreach (var r in renderers) if (r) r.enabled = on;
        foreach (var l in lights) if (l) l.enabled = on;
        if (affectColliders) foreach (var c in colliders) if (c) c.enabled = on;
    }
}
