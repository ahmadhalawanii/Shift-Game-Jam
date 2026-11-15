using System.Collections.Generic;
using UnityEngine;

public enum BulletEnd { Expired, Ground, Shot, Absorbed }

// Attacks that react to their own bullets (split on expiry, crater on landing) implement this.
public interface IBulletListener { void OnBulletEnd(EnemyBullet bullet, BulletEnd how); }

// Boss orbs reuse scene-owned objects. Swept collision prevents a fast orb skipping the shield.
public class EnemyBullet : MonoBehaviour
{
    public const int BossCap = 400;
    static readonly List<EnemyBullet> active = new List<EnemyBullet>();
    static readonly List<EnemyBullet> shootable = new List<EnemyBullet>();
    static readonly Stack<EnemyBullet> pool = new Stack<EnemyBullet>();
    static Material bossMaterial;
    static PlayerController player;
    static PlayerCombat combat;
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int ThornId = Shader.PropertyToID("_Thorn");
    public static int BossCount { get; private set; }
    public bool IsDreamSeed { get; private set; }
    public bool IsHeartSeed { get; private set; }
    public EnemyBullet HeartSeed() { IsHeartSeed = true; Look(DreamCombat.Pink, 0f, 1.1f); return this; }
    public static void ClearDanger(Vector3 center, float radius)
    {
        for (int i = active.Count - 1; i >= 0; i--)
            if (!active[i].IsHeartSeed && (active[i].transform.position - center).sqrMagnitude <= radius * radius) active[i].Retire();
    }
    public static void AttractPink(Vector3 center, float radius, float step)
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var b = active[i];
            if (!b.IsHeartSeed || (b.transform.position - center).sqrMagnitude > radius * radius) continue;
            b.transform.position = Vector3.MoveTowards(b.transform.position, center, step);
            if ((b.transform.position - center).sqrMagnitude < 1f && combat != null && combat.Dream != null)
            { combat.Dream.Catch(center, true); b.End(BulletEnd.Absorbed); }
        }
    }
    public static bool PinkNearby(Vector3 position, float radius)
    {
        for (int i = 0; i < active.Count; i++) if (active[i].IsHeartSeed && (active[i].transform.position - position).sqrMagnitude < radius * radius) return true;
        return false;
    }
    public float hitRadius = 0.8f;
    public int Generation { get; private set; }
    public int Tag { get; private set; }
    public Vector3 Direction => dir;
    bool bossOrb;
    Vector3 dir;
    float speed, damage, life;
    float turnRate, homeUntil, homeRate, bonusPower, heal;
    bool faller, canBeShot;
    IBulletListener listener;
    Color color;
    Renderer core;
    TrailRenderer trail;
    MaterialPropertyBlock properties;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { active.Clear(); shootable.Clear(); pool.Clear(); BossCount = 0; bossMaterial = null; player = null; combat = null; }

    void Awake()
    {
        core = GetComponentInChildren<MeshRenderer>();
        trail = GetComponent<TrailRenderer>();
        properties = new MaterialPropertyBlock();
    }
    void OnEnable() { active.Add(this); if (bossOrb) BossCount++; }
    void OnDisable()
    {
        active.Remove(this);
        if (canBeShot) { shootable.Remove(this); canBeShot = false; }
        if (bossOrb) BossCount = Mathf.Max(0, BossCount - 1);
    }

    public static EnemyBullet SpawnBoss(Vector3 position, Vector3 direction, float speed, float damage, World world)
    {
        if (BossCount >= BossCap) return null;
        EnemyBullet bullet = null;
        while (pool.Count > 0 && bullet == null) bullet = pool.Pop();
        if (bullet == null)
        {
            var go = CombatAssets.Spawn("EnemyOrb", position, Quaternion.identity);
            if (go == null) return null;
            bullet = go.GetComponent<EnemyBullet>();
            if (bullet == null) { Destroy(go); return null; }
            bullet.bossOrb = true; BossCount++;
        }
        bullet.transform.position = position;
        if (!bullet.gameObject.activeSelf) bullet.gameObject.SetActive(true);
        bullet.Init(direction, speed, damage, world);
        bullet.Generation++;
        bullet.turnRate = 0f; bullet.homeUntil = 0f; bullet.bonusPower = 0f; bullet.heal = 0f;
        bullet.faller = false; bullet.listener = null; bullet.Tag = 0;
        if (bossMaterial == null)
        {
            // Visual only: boss projectiles are drawn as feathers. Colour and danger still come from Look().
            var shader = Resources.Load<Shader>("Combat/BossFeather");
            if (shader == null) shader = Resources.Load<Shader>("Combat/EclipseOrb");
            if (shader != null) bossMaterial = new Material(shader);
        }
        if (bullet.core != null && bossMaterial != null)
        {
            bullet.core.sharedMaterial = bossMaterial;
            var filter = bullet.core.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != BossFeatherMesh.Get()) filter.sharedMesh = BossFeatherMesh.Get();
        }
        bullet.Look(world == World.Dream ? new Color(0.12f, 0.7f, 1f) : new Color(1f, 0.38f, 0.06f), bullet.IsDreamSeed ? 0f : 1f, 1.25f);
        bullet.hitRadius = 0.65f;
        if (bullet.trail != null) { bullet.trail.time = 0.12f; bullet.trail.widthMultiplier = 0.45f; bullet.trail.Clear(); }
        return bullet;
    }

    // Per-attack identity: colour, spikiness, size. Hit radius follows size.
    public EnemyBullet Look(Color tint, float thorn, float scale)
    {
        color = tint;
        if (core != null && bossMaterial != null)
        {
            properties.Clear();
            // Pull the weakest channel down so bloom keeps the hue instead of washing it to white.
            float m = Mathf.Min(tint.r, Mathf.Min(tint.g, tint.b)) * 0.6f;
            properties.SetColor(ColorId, new Color(tint.r - m, tint.g - m, tint.b - m) * 1.6f);
            properties.SetFloat(ThornId, thorn);
            core.SetPropertyBlock(properties);
        }
        if (trail != null) { trail.startColor = tint; trail.endColor = new Color(tint.r, tint.g, tint.b, 0f); }
        transform.localScale = Vector3.one * scale;
        hitRadius = 0.52f * scale;
        return this;
    }
    public EnemyBullet Curve(float degreesPerSecond) { turnRate = degreesPerSecond; return this; }
    public EnemyBullet Home(float seconds, float degreesPerSecond) { homeUntil = Time.time + seconds; homeRate = degreesPerSecond; return this; }
    public EnemyBullet Life(float seconds) { life = seconds; return this; }
    public EnemyBullet Trail(float seconds, float width) { if (trail != null) { trail.time = seconds; trail.widthMultiplier = width; } return this; }
    public EnemyBullet Faller() { faller = true; return this; }
    public EnemyBullet Reward(float power, float memoryHeal) { bonusPower = power; heal = memoryHeal; return this; }
    public EnemyBullet Listen(IBulletListener l, int tag = 0) { listener = l; Tag = tag; return this; }
    public EnemyBullet Shootable() { if (!canBeShot) { canBeShot = true; shootable.Add(this); } return this; }
    public bool Alive(int generation) => gameObject.activeSelf && Generation == generation;

    public static void Clear(Vector3 center, float radius)
    {
        float r2=radius*radius;
        for(int i=active.Count-1;i>=0;i--)
        {
            var b=active[i];
            if(b!=null && (b.transform.position-center).sqrMagnitude<=r2) b.Retire();
        }
    }
    public static void ClearBoss()
    {
        for(int i=active.Count-1;i>=0;i--) if(active[i]!=null && active[i].bossOrb) active[i].Retire();
    }
    public static void ReleaseSceneResources()
    {
        while(pool.Count>0) { var b=pool.Pop(); if(b!=null) Destroy(b.gameObject); }
        if(bossMaterial!=null) Destroy(bossMaterial);
        bossMaterial=null; player=null; combat=null;
    }

    // Player shards sweep this so homing orbs and meteors can be shot down. Returns true on a hit.
    public static bool Shoot(Vector3 from, Vector3 to, float refund)
    {
        for (int i = shootable.Count - 1; i >= 0; i--)
        {
            var b = shootable[i];
            Vector3 c = b.transform.position;
            float r = b.hitRadius + 0.35f;
            if (CombatGeometry.SegmentDistanceSquared(from.x, from.y, from.z, to.x, to.y, to.z, c.x, c.y, c.z) > r * r) continue;
            if (combat != null) combat.AddPower(refund);
            Fx.Burst(c, b.color, 14);
            b.End(BulletEnd.Shot);
            return true;
        }
        return false;
    }

    public static void DrawHitboxes()
    {
        for (int i = 0; i < active.Count; i++) BossHazards.Circle(active[i].transform.position, active[i].hitRadius, Color.yellow);
    }

    void End(BulletEnd how)
    {
        var l = listener;
        listener = null;
        Retire();
        if (l != null) l.OnBulletEnd(this, how);
    }

    void Retire()
    {
        if (!gameObject.activeSelf) return;
        listener = null;
        gameObject.SetActive(false);
        if(bossOrb) pool.Push(this); else Destroy(gameObject);
    }

    public void Init(Vector3 direction, float spd, float dmg, World world)
    {
        dir=direction.normalized; speed=spd; damage=dmg; life=8f;
        IsDreamSeed = world == World.Dream;
        IsHeartSeed = false;
        color=world==World.Dream?new Color(0.15f,0.75f,1f):new Color(1f,0.35f,0.06f);
        if(core!=null)
        {
            properties.Clear(); properties.SetColor("_BaseColor",color); properties.SetColor("_EmissionColor",color*1.6f);
            core.SetPropertyBlock(properties);
        }
        if(trail!=null)
        {
            trail.Clear(); trail.startColor=color; trail.endColor=new Color(color.r,color.g,color.b,0f);
        }
        transform.localScale=Vector3.one*(world==World.Dream?1f:0.8f);
    }
    void Update()
    {
        if(EclipseEncounter.Finished) return;
        if(player==null)
        {
            player=FindFirstObjectByType<PlayerController>();
            combat=player!=null?player.GetComponent<PlayerCombat>():null;
        }
        float dt=Time.deltaTime;
        if(turnRate!=0f) dir=Quaternion.Euler(0f,turnRate*dt,0f)*dir;
        if(homeUntil>Time.time && player!=null)
        {
            Vector3 to=player.transform.position+Vector3.up-transform.position; to.y=0f;
            if(to.sqrMagnitude>0.01f) dir=Vector3.RotateTowards(dir,to.normalized,homeRate*Mathf.Deg2Rad*dt,0f);
        }
        Vector3 from=transform.position;
        transform.position+=dir*speed*dt;
        // Feathers fly point first and spin on their shaft.
        if(bossOrb && core!=null && dir.sqrMagnitude>0.001f) core.transform.rotation=Quaternion.FromToRotation(Vector3.up,dir)*Quaternion.AngleAxis(Time.time*320f+(GetInstanceID()&255),Vector3.up);
        life-=dt;
        if(life<=0f) { End(BulletEnd.Expired); return; }
        if(faller && transform.position.y<=BossHazards.FloorY) { End(BulletEnd.Ground); return; }
        if(player==null || player.Recalling || damage<=0f) return;
        Vector3 to2=transform.position, c=player.transform.position+Vector3.up;
        float d2=CombatGeometry.SegmentDistanceSquared(from.x,from.y,from.z,to2.x,to2.y,to2.z,c.x,c.y,c.z);
        bool dream=WorldState.Instance!=null && WorldState.Instance.Current==World.Dream;
        if(dream && IsDreamSeed && combat!=null && combat.Dream != null && combat.Dream.WardUp && d2<combat.ShieldRadius*combat.ShieldRadius)
        {
            if (IsHeartSeed) combat.Dream.Catch(transform.position);
            else combat.Dream.Block(Mathf.Lerp(BalanceSheet.Load().wardDrainSmall, BalanceSheet.Load().wardDrainBig, Mathf.InverseLerp(.06f, .18f, damage)));
            Fx.Burst(transform.position,color,3); End(BulletEnd.Absorbed);
        }
        else if(d2<hitRadius*hitRadius)
        {
            if (!IsHeartSeed) { float hit = player.LastHitTime; player.Damage(damage); if (player.LastHitTime != hit) LucidCombatHUD.DamageSource = from; }
            Retire();
        }
    }
}
