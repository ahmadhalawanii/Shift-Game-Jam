using UnityEngine;

// The heart of a thorn gate. Closed and untouchable in Dream; in Nightmare it opens, glows, and can be shot.
// Destroying it opens its gates.
public class CorruptBud : Enemy
{
    public ThornGate[] gates;
    public Transform visual;
    float pulse;

    public override void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (WorldShift.WorldAt(transform.position) != World.Nightmare)
        {
            Fx.Burst(hitPoint, new Color(0.9f, 0.6f, 0.9f), 3);
            return;
        }
        pulse = 1f;
        base.TakeDamage(amount, hitPoint);
    }

    void Update()
    {
        pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 5f);
        bool open = WorldShift.WorldAt(transform.position) == World.Nightmare;
        if (visual != null)
            visual.localScale = Vector3.one * ((open ? 1f + 0.06f * Mathf.Sin(Time.time * 5f) : 0.8f) + pulse * 0.25f);
    }

    protected override void Die()
    {
        if (gates != null) foreach (var g in gates) if (g != null) g.Open();
        if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.Tell("The thorns let go.", 2.5f);
        base.Die();
    }
}
