using UnityEngine;

// An enemy orb. In Dream it is slow and can be absorbed by the shield. In Nightmare it is fast and sharp.
public class EnemyBullet : MonoBehaviour
{
    public float hitRadius = 0.8f;

    Vector3 dir;
    float speed;
    float damage;
    float life = 7f;
    Color color;

    static MaterialPropertyBlock block; // created lazily: Unity forbids creating one in a MonoBehaviour constructor

    public void Init(Vector3 direction, float spd, float dmg, World world)
    {
        dir = direction.normalized;
        speed = spd;
        damage = dmg;
        color = world == World.Dream ? new Color(1f, 0.45f, 0.78f) : new Color(1f, 0.22f, 0.08f);
        var r = GetComponentInChildren<Renderer>();
        if (r != null)
        {
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_EmissionColor", color * 3.5f);
            r.SetPropertyBlock(block);
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
        transform.position += dir * speed * Time.deltaTime;
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        var player = PlayerController.Instance;
        if (player == null) return;
        var combat = player.Combat;

        Vector3 c = player.transform.position + Vector3.up;
        float d2 = (c - transform.position).sqrMagnitude;
        if (combat != null && combat.ShieldUp && d2 < combat.shieldRadius * combat.shieldRadius)
        {
            combat.OnAbsorb();
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
