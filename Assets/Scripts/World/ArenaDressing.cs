using UnityEngine;

// The boss fight builds its own floor and standing stones at runtime. This re-skins them with the world
// materials so the swap wave rolls across the arena too, and asks for the slow, wide wave at phase changes.
// The fight itself is untouched.
public class ArenaDressing : MonoBehaviour
{
    public Material floor;
    public Material stones;
    public float phaseWaveSpeed = 0.45f;

    bool dressed;
    bool wasTransforming;
    float slowUntil = -1f;

    void Update()
    {
        var encounter = EclipseEncounter.Instance;
        if (encounter == null) return;
        if (!dressed)
        {
            var root = GameObject.Find("Eclipse combat floor");
            if (root != null)
            {
                dressed = true;
                var renderer = root.GetComponent<MeshRenderer>();
                if (renderer != null && floor != null) renderer.sharedMaterial = floor;
                if (stones != null)
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                        if (r.gameObject != root) r.sharedMaterial = stones;
            }
        }
        var boss = encounter.Boss;
        if (boss == null) return;
        bool transforming = boss.InTransformation;
        if (transforming && !wasTransforming)
        {
            // The next swap is the phase change: one huge slow wave.
            WorldShift.NextSpeedScale = phaseWaveSpeed;
            slowUntil = Time.time + 5f;
        }
        wasTransforming = transforming;
        if (slowUntil > 0f && Time.time > slowUntil)
        {
            slowUntil = -1f;
            WorldShift.NextSpeedScale = 1f;
        }
    }
}
