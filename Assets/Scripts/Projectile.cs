using UnityEngine;

// The player's Nightmare shard. Fast, slightly homing toward the target it was aimed at.
public class Projectile : MonoBehaviour
{
    public float speed = 70f;
    public float life = 1.3f;
    public float damage = 1f;
    public float homingRadsPerSec = 5f;

    Vector3 dir;
    Enemy target;

    public void Init(Vector3 direction, Enemy homingTarget)
    {
        dir = direction.normalized;
        target = homingTarget;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    void Update()
    {
        if (target != null)
        {
            Vector3 to = target.transform.position - transform.position;
            dir = Vector3.RotateTowards(dir, to.normalized, homingRadsPerSec * Time.deltaTime, 0f);
        }
        Vector3 step = dir * speed * Time.deltaTime;
        Vector3 from = transform.position;
        transform.position += step;
        transform.rotation = Quaternion.LookRotation(dir);
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        // Sweep against enemies along this frame's segment so fast shards never tunnel.
        for (int i = 0; i < Enemy.All.Count; i++)
        {
            var e = Enemy.All[i];
            if (e == null) continue;
            Vector3 p = ClosestOnSegment(from, transform.position, e.transform.position);
            if ((p - e.transform.position).sqrMagnitude < (e.radius + 0.15f) * (e.radius + 0.15f))
            {
                e.TakeDamage(damage, p);
                ThirdPersonCamera.Shake(0.03f);
                Destroy(gameObject);
                return;
            }
        }
    }

    static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-6f) return a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        return a + ab * t;
    }
}
