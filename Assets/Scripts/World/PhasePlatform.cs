using UnityEngine;

// A platform that is only solid in one world. The look (petals folding, thorns dissolving) is done by the
// Lucid/World shaders from the same wave; this switches the colliders when the front arrives.
// It turns solid at once, and stays solid for a short grace time after its world leaves, with a shimmer,
// so a swap never drops the player without warning.
public class PhasePlatform : MonoBehaviour
{
    public ExistsIn existsIn = ExistsIn.Dream;
    public float grace = 0.5f;
    public Collider[] solids;
    public Renderer shimmer;

    bool solid = true;
    float dropAt = -1f;
    Vector3 shimmerScale;

    public bool Solid { get { return solid; } }

    void Awake()
    {
        if (solids == null || solids.Length == 0) solids = GetComponentsInChildren<Collider>();
        if (shimmer != null) { shimmerScale = shimmer.transform.localScale; shimmer.enabled = false; }
    }

    void Start() { SetSolid(Present()); }

    bool Present()
    {
        if (existsIn == ExistsIn.Both) return true;
        return (WorldShift.WorldAt(transform.position) == World.Nightmare) == (existsIn == ExistsIn.Nightmare);
    }

    void SetSolid(bool on)
    {
        solid = on;
        for (int i = 0; i < solids.Length; i++) if (solids[i] != null) solids[i].enabled = on;
    }

    void Update()
    {
        bool present = Present();
        if (present)
        {
            dropAt = -1f;
            if (!solid) SetSolid(true);
        }
        else if (solid)
        {
            if (dropAt < 0f) dropAt = Time.time + grace;
            if (Time.time >= dropAt) SetSolid(false);
        }
        if (shimmer != null)
        {
            bool warn = solid && !present;
            shimmer.enabled = warn;
            if (warn) shimmer.transform.localScale = shimmerScale * (0.85f + 0.3f * Mathf.Abs(Mathf.Sin(Time.time * 26f)));
        }
    }
}
