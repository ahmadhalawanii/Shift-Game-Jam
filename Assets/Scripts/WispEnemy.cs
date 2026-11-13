using UnityEngine;

// Floating wisp. Keeps its distance, circles, telegraphs, then fires.
// Dream: a slow ring of orbs you can absorb. Nightmare: fast aimed bursts.
public class WispEnemy : Enemy
{
    public float activateDistance = 45f;
    public float preferredDistance = 13f;
    public float speed = 5f;
    public float hoverHeight = 1.7f;
    public float intervalDream = 2.4f;
    public float intervalNightmare = 1.1f;
    public float telegraph = 0.5f;
    public float dreamOrbSpeed = 7f;
    public float nightmareOrbSpeed = 18f;
    public float damage = 0.07f;

    Light glow;
    float timer;
    float tele;
    bool telegraphing;
    float seed;
    float baseIntensity;
    float ringOffset;

    protected override void Start()
    {
        base.Start();
        glow = GetComponentInChildren<Light>();
        baseIntensity = glow != null ? glow.intensity : 2f;
        timer = Random.Range(0.6f, 1.6f);
        seed = Random.value * 10f;
    }

    void Update()
    {
        if (player == null) return;
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        float d = to.magnitude;
        if (d > activateDistance) return;
        World w = WorldState.Instance != null ? WorldState.Instance.Current : World.Dream;

        Vector3 dir = d > 0.01f ? to / d : transform.forward;
        float err = Mathf.Clamp(d - preferredDistance, -1f, 1f);
        Vector3 move = dir * err * speed + Vector3.Cross(Vector3.up, dir) * Mathf.Sin(Time.time * 0.8f + seed) * speed * 0.6f;
        Vector3 p = transform.position + move * Time.deltaTime;
        p.y = hoverHeight + Mathf.Sin(Time.time * 1.7f + seed) * 0.25f;
        transform.position = p;

        timer -= Time.deltaTime;
        if (!telegraphing && timer <= 0f) { telegraphing = true; tele = telegraph; }
        if (telegraphing)
        {
            tele -= Time.deltaTime;
            if (glow != null) glow.intensity = baseIntensity * (1f + 3f * (1f - Mathf.Clamp01(tele / telegraph)));
            transform.localScale = Vector3.one * (1f + 0.25f * (1f - Mathf.Clamp01(tele / telegraph)));
            if (tele <= 0f)
            {
                Fire(w);
                telegraphing = false;
                timer = w == World.Dream ? intervalDream : intervalNightmare;
                transform.localScale = Vector3.one;
                if (glow != null) glow.intensity = baseIntensity;
            }
        }
    }

    void Fire(World w)
    {
        Vector3 origin = transform.position;
        Vector3 aim = (player.position + Vector3.up - origin);
        aim.y = 0f;
        aim.Normalize();
        if (w == World.Dream)
        {
            ringOffset += 17f;
            for (int i = 0; i < 8; i++)
                Spawn(origin, Quaternion.Euler(0, i * 45f + ringOffset, 0) * Vector3.forward, dreamOrbSpeed, w);
        }
        else
        {
            for (int i = -1; i <= 1; i++)
                Spawn(origin, Quaternion.Euler(0, i * 11f, 0) * aim, nightmareOrbSpeed, w);
        }
        Fx.Burst(origin, w == World.Dream ? new Color(1f, 0.5f, 0.8f) : new Color(1f, 0.25f, 0.08f), 12);
    }

    void Spawn(Vector3 origin, Vector3 dir, float spd, World w)
    {
        var g = CombatAssets.Spawn("EnemyOrb", new Vector3(origin.x, 1.1f, origin.z), Quaternion.identity);
        if (g != null) g.GetComponent<EnemyBullet>().Init(dir, spd, damage, w);
    }
}
