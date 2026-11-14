using UnityEngine;

// Glimmer Moth. Dream: a harmless drifting light; raise the shield (hold right-click) near it to absorb it
// for Power. Nightmare: it keeps its distance and fires a slow fan of thorn orbs after a visible swell.
public class GlimmerMoth : Enemy
{
    public float activateDistance = 30f;
    public float preferredDistance = 9f;
    public float leash = 13f;
    public float speed = 4.5f;
    public float hover = 2.4f;
    public float interval = 2.6f;
    public float telegraph = 0.6f;
    public float orbSpeed = 8f;
    public float damage = 0.05f;
    public Transform[] wings;
    public Transform body;

    Vector3 home;
    float timer, tele, seed;
    bool telegraphing;
    PlayerCombat combat;

    protected override void Start()
    {
        base.Start();
        home = transform.position;
        seed = Random.value * 20f;
        timer = Random.Range(1f, 2.2f);
        if (player != null) combat = player.GetComponent<PlayerCombat>();
    }

    void Update()
    {
        float t = Time.time;
        World w = WorldShift.WorldAt(transform.position);
        // Wings: slow and wide in Dream, fast and tight in Nightmare.
        float flap = Mathf.Sin(t * (w == World.Dream ? 9f : 22f) + seed) * (w == World.Dream ? 38f : 22f);
        if (wings != null)
            for (int i = 0; i < wings.Length; i++)
                if (wings[i] != null) wings[i].localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * (18f + flap));
        if (player == null) return;

        Vector3 chest = player.position + Vector3.up;
        Vector3 to = chest - transform.position;
        float d = to.magnitude;

        if (w == World.Dream)
        {
            telegraphing = false;
            if (body != null) body.localScale = Vector3.one;
            Vector3 drift = home + new Vector3(Mathf.Sin(t * 0.5f + seed) * 2.5f, Mathf.Sin(t * 0.9f + seed) * 0.6f, Mathf.Cos(t * 0.4f + seed) * 2.5f);
            transform.position = Vector3.MoveTowards(transform.position, drift, speed * 0.6f * Time.deltaTime);
            if (combat != null && combat.ShieldUp && d < combat.ShieldRadius + radius + 0.6f)
            {
                combat.OnAbsorb(transform.position);
                combat.AddPower(1f);
                Fx.Burst(transform.position, new Color(1f, 0.85f, 0.54f), 30);
                Destroy(gameObject);
            }
            return;
        }

        if (d > activateDistance) return;
        Vector3 flat = to; flat.y = 0f;
        float fd = flat.magnitude;
        Vector3 dir = fd > 0.01f ? flat / fd : transform.forward;
        Vector3 want = transform.position
            + dir * Mathf.Clamp(fd - preferredDistance, -1f, 1f) * speed * Time.deltaTime
            + Vector3.Cross(Vector3.up, dir) * Mathf.Sin(t * 0.8f + seed) * speed * 0.6f * Time.deltaTime;
        want.y = Mathf.MoveTowards(transform.position.y, player.position.y + hover + Mathf.Sin(t * 1.7f + seed) * 0.3f, speed * Time.deltaTime);
        Vector3 fromHome = want - home;
        if (fromHome.magnitude > leash) want = home + fromHome.normalized * leash;
        transform.position = want;
        if (fd > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 6f * Time.deltaTime);

        timer -= Time.deltaTime;
        if (!telegraphing && timer <= 0f) { telegraphing = true; tele = telegraph; }
        if (!telegraphing) return;
        tele -= Time.deltaTime;
        float k = 1f - Mathf.Clamp01(tele / telegraph);
        if (body != null) body.localScale = Vector3.one * (1f + 0.9f * k);
        if (tele > 0f) return;
        telegraphing = false;
        timer = interval;
        if (body != null) body.localScale = Vector3.one;
        Vector3 aim = (chest - transform.position).normalized;
        for (int i = -1; i <= 1; i++)
        {
            var g = CombatAssets.Spawn("EnemyOrb", transform.position, Quaternion.identity);
            if (g != null) g.GetComponent<EnemyBullet>().Init(Quaternion.AngleAxis(i * 14f, Vector3.up) * aim, orbSpeed, damage, World.Nightmare);
        }
        Fx.Burst(transform.position, new Color(1f, 0.35f, 0.21f), 12);
    }
}
