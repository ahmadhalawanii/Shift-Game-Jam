using UnityEngine;

public class EclipseBoss : Enemy
{
    public float maxHealth = 240f;
    public float activationDistance = 38f;
    public float swapInterval = 9f;
    public float warningDuration = 1.2f;
    public bool Engaged { get; private set; }
    public bool Warning { get; private set; }
    public bool Charging => Engaged && !Warning && attackTimer < 0.55f;
    public int Phase => health > maxHealth * 0.5f ? 1 : 2;
    float swapTimer, attackTimer, warningTimer, angle;
    bool perfectUsed, forcing;
    World nextWorld;
    Material core, rim;
    Transform halo;
    protected override void Start()
    {
        base.Start(); health = maxHealth; radius = 4f;
        core = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        core.SetColor("_BaseColor", new Color(0.012f, 0.005f, 0.025f));
        rim = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        Shape("Black sun", PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 7f, core);
        halo = new GameObject("Broken corona").transform; halo.SetParent(transform, false);
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI * 2f / 32f;
            var shard = Shape("Corona shard", PrimitiveType.Cube, halo,
                new Vector3(Mathf.Cos(a)*4.3f, Mathf.Sin(a)*4.3f, 0), new Vector3(0.22f, 0.7f + i%3*0.25f, 0.35f), rim);
            shard.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 90f);
        }
        swapTimer = swapInterval; attackTimer = 2f;
    }
    static Transform Shape(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var g = GameObject.CreatePrimitive(type); g.name = name;
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        Destroy(g.GetComponent<Collider>()); g.GetComponent<Renderer>().sharedMaterial = mat;
        return g.transform;
    }
    protected override void OnEnable() { base.OnEnable(); WorldState.Changed += OnShift; }
    protected override void OnDisable() { WorldState.Changed -= OnShift; base.OnDisable(); }
    void OnShift(World world)
    {
        if (!Warning || forcing || perfectUsed || world != nextWorld || EclipseEncounter.Finished) return;
        perfectUsed = true;
        var combat = player != null ? player.GetComponent<PlayerCombat>() : null;
        if (combat != null) combat.AddPower(3f);
        if (player != null)
        {
            EnemyBullet.Clear(player.position, 7f);
            CombatPulse.Spawn(player.position + Vector3.up * 0.2f, 7f, Color.cyan);
        }
        EclipseEncounter.Instance.Tell("PERFECT SHIFT  +3 POWER");
    }
    void Update()
    {
        if (player == null || EclipseEncounter.Finished) return;
        float mix = WorldState.Instance != null ? WorldState.Instance.Mix : 0f;
        rim.SetColor("_BaseColor", Color.Lerp(new Color(0.8f, 0.65f, 1f), new Color(1.4f, 0.18f, 0.03f), mix));
        halo.Rotate(0, 0, (Phase == 1 ? 10f : -22f) * Time.deltaTime);
        float charge = Charging ? 1f - Mathf.Clamp01(attackTimer / 0.55f) : 0f;
        halo.localScale = Vector3.one * (1f + charge * 0.18f);
        if (!Engaged)
        {
            if (Vector3.Distance(player.position, transform.position) > activationDistance) return;
            Engaged = true;
            EclipseEncounter.Instance.Tell("THE ECLIPSE — absorb, shift, unleash", 4f);
        }
        if (Warning)
        {
            warningTimer -= Time.deltaTime;
            if (warningTimer <= 0f)
            {
                forcing = true;
                if (WorldState.Instance != null) WorldState.Instance.Set(nextWorld);
                forcing = false; Warning = false;
                swapTimer = Phase == 1 ? swapInterval : swapInterval * 0.65f;
                attackTimer = 1f;
            }
            return;
        }
        swapTimer -= Time.deltaTime;
        if (swapTimer <= warningDuration)
        {
            Warning = true; perfectUsed = false; warningTimer = warningDuration;
            nextWorld = WorldState.Instance.Current == World.Dream ? World.Nightmare : World.Dream;
            EclipseEncounter.Instance.Tell("SHIFT TO " + nextWorld.ToString().ToUpper() + " NOW", warningDuration);
            CombatPulse.Spawn(new Vector3(transform.position.x, 0.2f, transform.position.z), 18f, new Color(1f, 0.4f, 0.8f));
            return;
        }
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            World w = WorldState.Instance.Current;
            Fire(w);
            attackTimer = (w == World.Dream ? 1.8f : 1.25f) / (Phase == 2 ? 1.3f : 1f);
        }
    }
    void Fire(World world)
    {
        Vector3 origin = new Vector3(transform.position.x, 1.1f, transform.position.z);
        Vector3 aim = player.position - origin; aim.y = 0f;
        float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
        int count = world == World.Dream ? 20 : 9;
        angle += 13f;
        for (int i = 0; i < count; i++)
        {
            float a = world == World.Dream ? i * 360f / count + angle : yaw + (i - count / 2) * 9f;
            SpawnOrb(origin, a, world);
        }
        if (Phase == 2) for (int i = 0; i < 12; i++) SpawnOrb(origin, angle * 2f + i * 30f, world);
    }
    void SpawnOrb(Vector3 origin, float angleDegrees, World world)
    {
        var orb = CombatAssets.Spawn("EnemyOrb", origin, Quaternion.identity);
        if (orb != null) orb.GetComponent<EnemyBullet>().Init(Quaternion.Euler(0, angleDegrees, 0) * Vector3.forward,
            world == World.Dream ? 8f : 13f, 0.09f, world);
    }
    public override void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (!Engaged || EclipseEncounter.Finished) return;
        if (WorldState.Instance.Current == World.Dream) damage *= 0.15f;
        base.TakeDamage(damage, hitPoint);
    }
    protected override void Die() { EclipseEncounter.Instance.End(true); base.Die(); }
    void OnDestroy() { if (core != null) Destroy(core); if (rim != null) Destroy(rim); }
}
