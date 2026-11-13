using UnityEngine;

// An enemy orb. In Dream it is slow and can be absorbed by the shield. In Nightmare it is fast and sharp.
public class EnemyBullet : MonoBehaviour
{
    static readonly System.Collections.Generic.List<EnemyBullet> active = new System.Collections.Generic.List<EnemyBullet>();
    void OnEnable() { active.Add(this); }
    void OnDisable() { active.Remove(this); }
    public static void Clear(Vector3 center, float radius)
    {
        foreach (var bullet in active.ToArray())
            if (bullet != null && (bullet.transform.position - center).sqrMagnitude <= radius * radius)
            {
                bullet.gameObject.SetActive(false);
                Destroy(bullet.gameObject);
            }
    }
    public float hitRadius = 0.8f;

    Vector3 dir;
    float speed;
    float damage;
    float life = 7f;
    Color color;

    static PlayerController player;
    static PlayerCombat combat;

    public void Init(Vector3 direction, float spd, float dmg, World world)
    {
        dir = direction.normalized;
        speed = spd;
        damage = dmg;
        color = world == World.Dream ? new Color(1f, 0.45f, 0.78f) : new Color(1f, 0.22f, 0.08f);
        var r = GetComponentInChildren<Renderer>();
        if (r != null)
        {
            var b = new MaterialPropertyBlock();
            r.GetPropertyBlock(b);
            b.SetColor("_BaseColor", color);
            b.SetColor("_EmissionColor", color * 3.5f);
            r.SetPropertyBlock(b);
        }
        var t = GetComponent<TrailRenderer>();
        if (t != null)
        {
            t.startColor = color;
            t.endColor = new Color(color.r, color.g, color.b, 0f);
        }
        transform.localScale = Vector3.one * (world == World.Dream ? 1f : 0.8f);
    }

    void Update()
    {
        if (EclipseEncounter.Finished) return;
        transform.position += dir * speed * Time.deltaTime;
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        if (player == null) { player = Object.FindFirstObjectByType<PlayerController>(); combat = player != null ? player.GetComponent<PlayerCombat>() : null; }
        if (player == null) return;

        Vector3 c = player.transform.position + Vector3.up;
        float d2 = (c - transform.position).sqrMagnitude;
        if (combat != null && combat.ShieldUp && d2 < combat.ShieldRadius * combat.ShieldRadius)
        {
            combat.OnAbsorb(transform.position);
            Fx.Burst(transform.position, color, 10);
            Destroy(gameObject);
        }
        else if (d2 < hitRadius * hitRadius)
        {
            player.Damage(damage);
            Fx.Burst(transform.position, color, 14);
            Destroy(gameObject);
        }
    }
}
