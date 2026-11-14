using System.Collections.Generic;
using UnityEngine;

// Boss orbs reuse scene-owned objects. Swept collision prevents a fast orb skipping the shield.
public class EnemyBullet : MonoBehaviour
{
    static readonly List<EnemyBullet> active = new List<EnemyBullet>();
    static readonly Stack<EnemyBullet> pool = new Stack<EnemyBullet>();
    static Material bossMaterial;
    static PlayerController player;
    static PlayerCombat combat;
    public static int BossCount { get; private set; }
    public bool IsDreamSeed { get; private set; }
    public float hitRadius = 0.8f;
    bool bossOrb;
    Vector3 dir;
    float speed, damage, life;
    Color color;
    Renderer core;
    TrailRenderer trail;
    MaterialPropertyBlock properties;

    void Awake()
    {
        core = GetComponentInChildren<Renderer>();
        trail = GetComponent<TrailRenderer>();
        properties = new MaterialPropertyBlock();
    }
    void OnEnable() { active.Add(this); if (bossOrb) BossCount++; }
    void OnDisable() { active.Remove(this); if (bossOrb) BossCount = Mathf.Max(0, BossCount - 1); }

    public static void SpawnBoss(Vector3 position, Vector3 direction, float speed, float damage, World world)
    {
        if (BossCount >= 220) return;
        EnemyBullet bullet = null;
        while (pool.Count > 0 && bullet == null) bullet = pool.Pop();
        if (bullet == null)
        {
            var go = CombatAssets.Spawn("EnemyOrb", position, Quaternion.identity);
            if (go == null) return;
            bullet = go.GetComponent<EnemyBullet>();
            if (bullet == null) { Destroy(go); return; }
            bullet.bossOrb = true; BossCount++;
        }
        bullet.transform.position = position;
        if (!bullet.gameObject.activeSelf) bullet.gameObject.SetActive(true);
        bullet.Init(direction, speed, damage, world);
        if (bossMaterial == null)
        {
            var shader = Resources.Load<Shader>("Combat/EclipseOrb");
            if (shader != null) bossMaterial = new Material(shader);
        }
        if (bullet.core != null && bossMaterial != null)
        {
            bullet.core.sharedMaterial = bossMaterial;
            bullet.properties.Clear();
            bullet.properties.SetColor("_Color", world == World.Dream ? new Color(0.12f,0.7f,1f) : new Color(1f,0.38f,0.06f));
            bullet.properties.SetFloat("_Thorn", bullet.IsDreamSeed ? 0f : 1f);
            bullet.core.SetPropertyBlock(bullet.properties);
        }
        bullet.transform.localScale = Vector3.one * 1.25f;
        bullet.hitRadius = 0.65f;
        if (bullet.trail != null) { bullet.trail.time = 0.12f; bullet.trail.widthMultiplier = 0.45f; bullet.trail.Clear(); }
    }

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
    void Retire()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        if(bossOrb) pool.Push(this); else Destroy(gameObject);
    }

    public void Init(Vector3 direction, float spd, float dmg, World world)
    {
        dir=direction.normalized; speed=spd; damage=dmg; life=8f;
        IsDreamSeed = world == World.Dream;
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
        Vector3 from=transform.position;
        transform.position+=dir*speed*Time.deltaTime;
        life-=Time.deltaTime;
        if(life<=0f) { Retire(); return; }
        if(player==null)
        {
            player=FindFirstObjectByType<PlayerController>();
            combat=player!=null?player.GetComponent<PlayerCombat>():null;
        }
        if(player==null || player.Recalling) return;
        Vector3 to=transform.position, c=player.transform.position+Vector3.up;
        float d2=CombatGeometry.SegmentDistanceSquared(from.x,from.y,from.z,to.x,to.y,to.z,c.x,c.y,c.z);
        bool dream=WorldState.Instance!=null && WorldState.Instance.Current==World.Dream;
        if(dream && IsDreamSeed && combat!=null && combat.ShieldUp && d2<combat.ShieldRadius*combat.ShieldRadius)
        {
            combat.OnAbsorb(transform.position);
            Fx.Burst(transform.position,color,3); Retire();
        }
        else if(d2<hitRadius*hitRadius)
        {
            player.Damage(damage); Retire();
        }
    }
}
