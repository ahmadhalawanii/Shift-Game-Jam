using UnityEngine;

public enum WorldVisibility { Both, DreamOnly, NightmareOnly }

// Put on anything that only exists in one world (props, dressing, particles).
// Hides its renderers and lights while the other world is active.
public class WorldObject : MonoBehaviour
{
    public WorldVisibility visibleIn = WorldVisibility.Both;

    Renderer[] renderers;
    Light[] lights;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        lights = GetComponentsInChildren<Light>(true);
    }

    void OnEnable()
    {
        WorldState.Changed += Apply;
        Apply(WorldState.CurrentOrDream);
    }

    void OnDisable() { WorldState.Changed -= Apply; }

    void Apply(World w)
    {
        bool on = visibleIn == WorldVisibility.Both
            || (visibleIn == WorldVisibility.DreamOnly && w == World.Dream)
            || (visibleIn == WorldVisibility.NightmareOnly && w == World.Nightmare);
        foreach (var r in renderers) if (r) r.enabled = on;
        foreach (var l in lights) if (l) l.enabled = on;
    }
}
