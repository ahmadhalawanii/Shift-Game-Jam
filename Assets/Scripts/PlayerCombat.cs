using UnityEngine;
using UnityEngine.InputSystem;

// Hold left mouse. In Nightmare it fires a stream of shards (aim assist pulls them toward the nearest enemy).
// In Dream it raises a shield that absorbs enemy orbs and charges Power.
public class PlayerCombat : MonoBehaviour
{
    [Header("Nightmare shooting")]
    public float fireRate = 14f;
    public float spreadDegrees = 1.8f;
    public float assistRange = 45f;
    public float assistConeDegrees = 30f;

    [Header("Dream shield")]
    public float shieldRadius = 2.4f;
    public float maxPower = 10f;
    public float burstRadius = 42f;
    public float burstDamage = 45f;

    public float Power { get; private set; }
    public bool ShieldUp { get; private set; }
    public bool Firing { get { return Time.time - lastShot < 0.18f; } }
    public float Kick { get; private set; }
    public float ShieldRadius { get { return shieldRadius; } }

    PlayerController pc;
    MemoryAvatar avatar;
    Camera cam;
    GameObject shield;
    float nextShot;
    float lastShot = -99f;
    float shieldScale;

    void Start()
    {
        pc = GetComponent<PlayerController>();
        avatar = GetComponentInChildren<MemoryAvatar>();
        cam = Camera.main;
        shield = CombatAssets.Spawn("Shield", transform.position, Quaternion.identity);
        if (shield != null)
        {
            shield.transform.SetParent(transform, false);
            shield.transform.localPosition = new Vector3(0, 1f, 0);
            shield.transform.localScale = Vector3.zero;
        }
    }

    void Update()
    {
        if (EclipseEncounter.Finished) { ShieldUp = false; return; }
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) TryBurst();
        var mouse = Mouse.current;
        bool held = mouse != null && mouse.leftButton.isPressed && !(pc != null && pc.Recalling);
        World w = WorldState.Instance != null ? WorldState.Instance.Current : World.Dream;

        if (w == World.Nightmare)
        {
            ShieldUp = false;
            if (held && Time.time >= nextShot) Fire();
        }
        else
        {
            ShieldUp = held;
        }

        Kick = Mathf.MoveTowards(Kick, 0f, 9f * Time.deltaTime);
        if (held) FaceAim();

        if (shield != null)
        {
            shieldScale = Mathf.MoveTowards(shieldScale, ShieldUp ? shieldRadius * 2f : 0f, 30f * Time.deltaTime);
            shield.transform.localScale = Vector3.one * shieldScale;
        }
    }

    Vector3 FlatAim()
    {
        Vector3 f = cam != null ? cam.transform.forward : transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.001f ? f.normalized : transform.forward;
    }

    void FaceAim()
    {
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(FlatAim()), 18f * Time.deltaTime);
    }

    Enemy FindTarget(Vector3 aim, Vector3 from)
    {
        Enemy best = null;
        float bestAngle = assistConeDegrees;
        for (int i = 0; i < Enemy.All.Count; i++)
        {
            var e = Enemy.All[i];
            if (e == null) continue;
            Vector3 to = e.transform.position - from;
            if (to.magnitude > assistRange) continue;
            to.y = 0f;
            float a = Vector3.Angle(aim, to);
            if (a < bestAngle) { bestAngle = a; best = e; }
        }
        return best;
    }

    void Fire()
    {
        nextShot = Time.time + 1f / fireRate;
        lastShot = Time.time;
        Kick = 1f;
        Vector3 aim = FlatAim();
        Vector3 origin = transform.position + Vector3.up * 1.2f + aim * 0.7f;
        Enemy target = FindTarget(aim, origin);
        Vector3 dir = aim;
        if (target != null)
        {
            Vector3 to = (target.transform.position - origin).normalized;
            dir = Vector3.Slerp(aim, to, 0.85f);
        }
        dir = Quaternion.Euler(0f, Random.Range(-spreadDegrees, spreadDegrees), 0f) * dir;

        var shard = CombatAssets.Spawn("Shard", origin, Quaternion.LookRotation(dir));
        if (shard != null) shard.GetComponent<Projectile>().Init(dir, target);

        Fx.Burst(origin, new Color(1f, 0.75f, 0.3f), 4);
        ThirdPersonCamera.Shake(0.012f);
    }

    public void AddPower(float amount) { Power = Mathf.Clamp(Power + amount, 0f, maxPower); }

    public bool TryBurst()
    {
        if (EclipseEncounter.Finished || (pc != null && pc.Recalling)) return false;
        if (WorldState.Instance == null || WorldState.Instance.Current != World.Nightmare || Power < maxPower)
        {
            if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.Tell("Burst needs full Power and Nightmare.");
            return false;
        }
        Power = 0f;
        Vector3 center = transform.position;
        EnemyBullet.Clear(center, burstRadius);
        foreach (var enemy in Enemy.All.ToArray())
            if (enemy != null && (enemy.transform.position - center).sqrMagnitude <= burstRadius * burstRadius)
                enemy.TakeDamage(burstDamage, enemy.transform.position);
        CombatPulse.Spawn(center + Vector3.up * 0.25f, burstRadius, new Color(0.7f, 0.3f, 1.5f));
        Fx.Burst(center + Vector3.up, new Color(0.65f, 0.3f, 1f), 45);
        ThirdPersonCamera.Shake(0.3f);
        if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.Tell("MEMORY BURST");
        return true;
    }

    public void OnAbsorb(Vector3 where)
    {
        Power = Mathf.Min(maxPower, Power + 1f);
        if (avatar != null)
        {
            avatar.memory = Mathf.Min(1f, avatar.memory + 0.012f);
            avatar.Sharpen();
        }
        ThirdPersonCamera.Shake(0.02f);
    }
}
