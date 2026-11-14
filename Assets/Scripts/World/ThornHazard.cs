using UnityEngine;

// A patch that hurts while its world is present (Nightmare thorns by default). The box is in local space.
// With riseSpeed set it is the rising gloom of the Rootclimb: it climbs in Nightmare and sinks back in Dream.
public class ThornHazard : MonoBehaviour
{
    public ExistsIn activeIn = ExistsIn.Nightmare;
    public Vector3 size = new Vector3(3f, 1.2f, 3f);
    public float damage = 0.08f;
    public float interval = 0.8f;
    public Color sparks = new Color(1f, 0.35f, 0.21f);

    [Header("Rising gloom (optional)")]
    public float riseSpeed;
    public float sinkSpeed = 6f;
    public float maxRise = 20f;

    float nextHit;
    float baseY;
    float risen;

    void Start() { baseY = transform.position.y; }

    bool Active()
    {
        if (activeIn == ExistsIn.Both) return true;
        return (WorldShift.WorldAt(transform.position) == World.Nightmare) == (activeIn == ExistsIn.Nightmare);
    }

    void Update()
    {
        var player = LevelFlow.Player;
        if (player == null) return;
        bool active = Active();
        if (riseSpeed > 0f)
        {
            // Only climbs while the player is in the shaft above it.
            Vector3 flat = player.transform.position - transform.position;
            bool above = Mathf.Abs(flat.x) < size.x * 0.5f && Mathf.Abs(flat.z) < size.z * 0.5f;
            risen = Mathf.MoveTowards(risen, active && above ? maxRise : 0f, (active && above ? riseSpeed : sinkSpeed) * Time.deltaTime);
            var p = transform.position;
            p.y = baseY + risen;
            transform.position = p;
        }
        if (!active || Time.time < nextHit) return;
        Vector3 local = transform.InverseTransformPoint(player.transform.position + Vector3.up * 0.5f);
        if (Mathf.Abs(local.x) > size.x * 0.5f || Mathf.Abs(local.z) > size.z * 0.5f || local.y > size.y * 0.5f) return;
        // A static patch only hurts inside its box. The rising gloom hurts everything it has swallowed.
        if (riseSpeed <= 0f && local.y < -size.y * 0.5f) return;
        nextHit = Time.time + interval;
        player.Damage(damage);
        Fx.Burst(player.transform.position + Vector3.up * 0.5f, sparks, 14);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.5f);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
