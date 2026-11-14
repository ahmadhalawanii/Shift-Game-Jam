using UnityEngine;

public class DreamCombat : MonoBehaviour
{
    public static readonly Color Pink = new Color(1f, .478f, .784f);
    public float Ward { get; private set; }
    public bool WardUp { get; private set; }
    public bool Broken => Time.time < brokenUntil;
    public bool Drawing => drawStarted >= 0f;
    public float Draw01 => Drawing ? Mathf.Clamp01((Time.time - drawStarted) / sheet.bowDrawTime) : 0f;
    public float PulseReady01 => 1f - Mathf.Clamp01((pulseUntil - Time.time) / sheet.lucidPulse.cooldown);
    public float BloomReady01 => 1f - Mathf.Clamp01((bloomReady - Time.time) / sheet.bloom.cooldown);
    public float PerfectAt { get; private set; } = -99f;
    public bool BloomActive => Time.time < bloomUntil;
    public Vector3 BloomPosition { get; private set; }
    BalanceSheet sheet;
    PlayerCombat combat;
    PlayerController pc;
    MemoryAvatar avatar;
    float raisedAt = -99f, touchedAt = -99f, brokenUntil, drawStarted = -1f, nextBow;
    float pulseUntil, bloomReady, bloomUntil, mistUntil, bloomHeal, baseSpeed, streakUntil;
    LineRenderer streak;
    Material material;
    GameObject blossom;
    void Awake()
    {
        sheet = BalanceSheet.Load(); combat = GetComponent<PlayerCombat>(); pc = GetComponent<PlayerController>();
        avatar = GetComponentInChildren<MemoryAvatar>(); Ward = sheet.wardMax; baseSpeed = pc.maxSpeed;
        pc.DashEnded += DashMist; WorldState.Changed += Changed;
    }
    public void RestoreWard()
    {
        Ward = sheet.wardMax;
        brokenUntil = 0f;
    }

    void Changed(World w) { drawStarted = -1f; WardUp = false; pc.maxSpeed = baseSpeed; }
    void DashMist() { if (WorldState.CurrentOrDream == World.Dream) mistUntil = Time.time + sheet.dreamMistSeconds; }
    public void Tick(World world)
    {
        bool dream = world == World.Dream;
        if (LucidInput.Aim.WasPressedThisFrame()) raisedAt = Time.time;
        WardUp = dream && LucidInput.AimHeld && !Broken;
        pc.maxSpeed = baseSpeed * (WardUp ? sheet.wardMoveScale : 1f);
        if (!Broken && ((!WardUp && Time.time - touchedAt >= sheet.wardRegenDelay) || Time.time < mistUntil))
            Ward = Mathf.Min(sheet.wardMax, Ward + sheet.wardRegenPerSecond * Time.deltaTime);
        if (dream && LucidInput.Fire.WasPressedThisFrame() && Time.time >= nextBow) drawStarted = Time.time;
        if (Drawing && LucidInput.Fire.WasReleasedThisFrame()) { if (Draw01 >= 1f) FireBow(); drawStarted = -1f; }
        if (!dream) drawStarted = -1f;
        if (dream && LucidInput.Pressed(LucidInput.Ability1) && Time.time >= pulseUntil)
        {
            LucidInput.Consume(LucidInput.Ability1); pulseUntil = Time.time + sheet.lucidPulse.cooldown;
            EnemyBullet.ClearDanger(transform.position, sheet.lucidPulse.radius);
            CombatPulse.Spawn(transform.position + Vector3.up, sheet.lucidPulse.radius, new Color(.7f, .9f, 1f));
            LucidAudio.Play(LucidAudio.Cue.Burst);
        }
        if (dream && LucidInput.Pressed(LucidInput.Ability2) && Time.time >= bloomReady)
        {
            LucidInput.Consume(LucidInput.Ability2); bloomReady = Time.time + sheet.bloom.cooldown;
            bloomUntil = Time.time + sheet.bloomSeconds; BloomPosition = transform.position + Vector3.up; bloomHeal = 0f;
            if (blossom == null)
            {
                blossom = GameObject.CreatePrimitive(PrimitiveType.Sphere); blossom.name = "Heart blossom";
                Destroy(blossom.GetComponent<Collider>()); blossom.GetComponent<Renderer>().sharedMaterial = LineMaterial;
            }
            blossom.SetActive(true); blossom.transform.position = BloomPosition;
            CombatPulse.Spawn(BloomPosition, sheet.bloom.radius, Pink); LucidAudio.Play(LucidAudio.Cue.Capture);
        }
        if (blossom != null)
        {
            blossom.SetActive(BloomActive);
            if (BloomActive) blossom.transform.localScale = Vector3.one * (.5f + .1f * Mathf.Sin(Time.time * 5f));
        }
        if (BloomActive)
        {
            EnemyBullet.AttractPink(BloomPosition, sheet.bloom.radius, sheet.bloomPullSpeed * Time.deltaTime);
            // Bloom redistributes healing from captured pink seeds, never free passive health.
            if (dream && bloomHeal > 0f && (transform.position - BloomPosition).sqrMagnitude < sheet.bloomHealRadius * sheet.bloomHealRadius)
            { float amount = Mathf.Min(bloomHeal, sheet.bloom.amount * Time.deltaTime); Heal(amount); bloomHeal -= amount; }
        }
        if (streak != null) streak.enabled = Time.time < streakUntil;
    }
    Material LineMaterial
    {
        get
        {
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetColor("_BaseColor", Pink * 2f); }
            return material;
        }
    }
    void FireBow()
    {
        nextBow = Time.time + sheet.bowInterval; combat.KickNow();
        Vector3 origin = transform.position + Vector3.up * 1.2f, dir = combat.FlatAim();
        var boss = EclipseEncounter.Instance != null ? EclipseEncounter.Instance.Boss : null;
        if (boss != null && boss.Engaged && boss.WeakPoints != null)
            for (int i = 0; i < BossWeakPoints.Count; i++)
            {
                Vector3 delta = boss.WeakPoints.Position(i) - origin; delta.y = 0f;
                float along = Vector3.Dot(delta, dir);
                if (along > 0f && along < sheet.bowRange && (delta - dir * along).sqrMagnitude <= sheet.weakPointSize * sheet.weakPointSize && boss.WeakPoints.Tag(i))
                { combat.HitConfirm(); CombatPulse.Spawn(boss.WeakPoints.Position(i), 2f, Color.white); EclipseEncounter.Instance.Tell("WEAK POINT TAGGED / CASH IN IN NIGHTMARE", 1.4f); }
            }
        if (streak == null)
        {
            var go = new GameObject("Lumen streak"); go.transform.SetParent(transform);
            streak = go.AddComponent<LineRenderer>(); streak.sharedMaterial = LineMaterial;
            streak.positionCount = 2; streak.widthMultiplier = .055f;
        }
        streak.SetPosition(0, origin); streak.SetPosition(1, origin + dir * sheet.bowRange);
        streak.enabled = true; streakUntil = Time.time + .16f; LucidAudio.Play(LucidAudio.Cue.Thread);
    }
    public bool Block(float drain)
    {
        if (!WardUp || Broken) return false;
        touchedAt = Time.time; Ward = Mathf.Max(0f, Ward - drain);
        if (Ward <= 0f) { brokenUntil = Time.time + sheet.wardBreakSeconds; WardUp = false; LucidAudio.Sfx("empty"); }
        return true;
    }
    public void Catch(Vector3 where, bool throughBloom = false)
    {
        bool perfect = !throughBloom && Time.time - raisedAt <= sheet.perfectCatchWindow;
        float heal = sheet.seedHeal * (perfect ? sheet.perfectCatchMultiplier : 1f);
        var boss = EclipseEncounter.Instance != null ? EclipseEncounter.Instance.Boss : null;
        if (boss != null && boss.Engaged) heal = boss.ClaimSeedHealing(heal);
        if (throughBloom) bloomHeal += heal; else Heal(heal);
        Ward = Mathf.Min(sheet.wardMax, Ward + sheet.seedWard); combat.AddPower(sheet.seedPower);
        Fx.Burst(where, Pink, perfect ? 32 : 12); LucidAudio.Play(LucidAudio.Cue.Capture);
        if (perfect)
        {
            PerfectAt = Time.unscaledTime;
            if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.Tell("PERFECT CATCH", 1f);
            if (boss != null && boss.Engaged)
            {
                var shot = CombatAssets.Spawn("Shard", where, Quaternion.identity);
                if (shot != null && shot.TryGetComponent<Projectile>(out var p))
                { p.Init((boss.transform.position - where).normalized, boss); p.Configure(sheet.perfectCatchShotDamage, Color.white, false); p.owner = combat; }
            }
        }
    }
    void Heal(float hp) { if (avatar != null) avatar.health = Mathf.Min(1f, avatar.health + hp / 100f); }
    void OnDestroy()
    {
        if (pc != null) { pc.DashEnded -= DashMist; pc.maxSpeed = baseSpeed; }
        WorldState.Changed -= Changed;
        if (blossom != null) Destroy(blossom); if (material != null) Destroy(material);
    }
}
