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
    Color impactColor = Color.cyan;
    bool heavy;

    public void Configure(float amount, Color tint, bool isHeavy)
    {
        damage = amount;
        impactColor = tint;
        heavy = isHeavy;
        if (heavy) transform.localScale *= 2.8f;
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_Color", tint);
            properties.SetColor("_BaseColor", tint);
            properties.SetColor("_EmissionColor", tint * 1.5f);
            renderer.SetPropertyBlock(properties);
        }
    }

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

        if (EnemyBullet.Shoot(from, transform.position, BossAttackData.Load().shootPowerRefund))
        {
            Destroy(gameObject);
            return;
        }

        // Sweep against enemies along this frame's segment so fast shards never tunnel.
        for (int i = 0; i < Enemy.All.Count; i++)
        {
            var e = Enemy.All[i];
            if (e == null) continue;
            Vector3 p = ClosestOnSegment(from, transform.position, e.transform.position);
            if ((p - e.transform.position).sqrMagnitude < (e.radius + 0.15f) * (e.radius + 0.15f))
            {
                e.TakeDamage(damage, p);
                Fx.Burst(p, impactColor, heavy ? 24 : 5);
                ThirdPersonCamera.Shake(heavy ? 0.16f : 0.03f);
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
