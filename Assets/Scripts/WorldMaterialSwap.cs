using UnityEngine;

// Swaps the material on this object (and children) when the world changes.
public class WorldMaterialSwap : MonoBehaviour
{
    public Material dream;
    public Material nightmare;

    Renderer[] renderers;

    void Awake() { renderers = GetComponentsInChildren<Renderer>(true); }

    void OnEnable()
    {
        WorldState.Changed += Apply;
        Apply(WorldState.CurrentOrDream);
    }

    void OnDisable() { WorldState.Changed -= Apply; }

    void Apply(World w)
    {
        var m = w == World.Dream ? dream : nightmare;
        if (m == null) return;
        foreach (var r in renderers) if (r) r.sharedMaterial = m;
    }
}
