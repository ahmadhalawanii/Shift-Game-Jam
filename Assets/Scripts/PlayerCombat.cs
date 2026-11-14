using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Thread Strike")]
    public float fireRate = 14f;
    [Min(0.1f)] public float threadDamage = 2f;
    public float spreadDegrees = 1.8f;
    public float assistRange = 45f;
    public float assistConeDegrees = 30f;

    [Header("Dream seed capture")]
    public float shieldRadius = 2.4f;
    public float maxPower = 10f;

    [Header("Dreamcatcher Burst")]
    public float burstDamage = 70f;
    [Min(0.1f)] public float burstChargeTime = 0.9f;
    [Min(0f)] public float burstCooldown = 0.55f;

    public float Power { get; private set; }
    // Dream combo: consecutive absorbs without being hit. Level 1..5; at 5 a Lucid Pulse is ready.
    public int ResonanceLevel => 1 + Mathf.Min(4, resonanceAbsorbs / Mathf.Max(1, BossAttackData.Load().absorbsPerResonance));
    public bool LucidPulseReady => ResonanceLevel >= 5;
    int resonanceAbsorbs;
    float resonanceSince;
    public bool ShieldUp { get; private set; }
    public bool Firing { get { return Time.time - lastShot < 0.18f || (weapons != null && weapons.Firing); } }
    public PlayerWeapons Weapons { get { return weapons; } }
    // Dread: 0..1, built by dealing damage in Nightmare, cut when hit. It multiplies Nightmare weapon damage.
    public float Dread { get; private set; }
    public float DreadMultiplier
    {
        get
        {
            var b = BalanceSheet.Load();
            bool fight = EclipseEncounter.Instance != null && EclipseEncounter.Instance.BossFightStarted;
            return Mathf.Min(1f + (b.dreadMaxMultiplier - 1f) * Dread, fight ? b.dreadMaxMultiplier : b.dreadGateMultiplier);
        }
    }
    public void AddDread(float damage) { Dread = Mathf.Clamp01(Dread + damage / Mathf.Max(1f, BalanceSheet.Load().dreadDamageToFill)); }
    public void SetDread(float value) { Dread = Mathf.Clamp01(value); }
    public void KickNow() { Kick = 1f; }
    PlayerWeapons weapons;
    float dreadHitSeen;
    public bool HeavyCast { get; private set; }
    public float Kick { get; private set; }
    public float ShieldRadius { get { return shieldRadius; } }
    public bool IsChargingBurst { get; private set; }
    // Nightmare: right mouse aims over the shoulder. Dream: right mouse raises the shield.
    public bool Aiming { get; private set; }
    public float LastHitConfirm { get; private set; } = -99f;
    public void HitConfirm() { LastHitConfirm = Time.unscaledTime; }
    public float BurstCharge01 { get { return IsChargingBurst ? Mathf.Clamp01(chargeAge / Mathf.Max(0.05f, burstChargeTime)) : 0f; } }

    PlayerController pc;
    MemoryAvatar avatar;
    Camera cam;
    GameObject shield;
    float nextShot;
    float lastShot = -99f;
    float shieldScale;
    float chargeAge;
    float nextBurst;
    BurstVisual chargingVisual;
    Renderer shieldRenderer;
    MaterialPropertyBlock shieldBlock;
    static readonly int ShieldColorId = Shader.PropertyToID("_Color");

    void Start()
    {
        pc = GetComponent<PlayerController>();
        weapons = GetComponent<PlayerWeapons>();
        if (weapons == null) weapons = gameObject.AddComponent<PlayerWeapons>();
        avatar = GetComponentInChildren<MemoryAvatar>();
        cam = Camera.main;
        shield = CombatAssets.Spawn("Shield", transform.position, Quaternion.identity);
        if (shield != null)
        {
            shield.transform.SetParent(transform, false);
            shield.transform.localPosition = new Vector3(0, 1f, 0);
            shield.transform.localScale = Vector3.zero;
            shieldRenderer = shield.GetComponentInChildren<Renderer>();
            if (shieldRenderer != null)
            {
                shieldRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // no dark blob under the shield
                shieldRenderer.receiveShadows = false;
            }
            shieldBlock = new MaterialPropertyBlock();
        }
    }

    public void ResetResonance()
    {
        resonanceAbsorbs = 0;
        resonanceSince = Time.time;
    }

    // Tether seeds and gold seeds add Power and heal on top of a normal absorb.
    public void OnGoldAbsorb(Vector3 where, float bonusPower, float heal)
    {
        AddPower(bonusPower);
        if (avatar != null) avatar.health = Mathf.Min(1f, avatar.health + heal);
        Fx.Burst(where, new Color(1f, 0.85f, 0.3f), 22);
    }

    void Update()
    {
        if (EclipseEncounter.Finished)
        {
            CancelBurst();
            HideShield();
            return;
        }
        World w = WorldState.Instance != null ? WorldState.Instance.Current : World.Dream;
        if ((pc != null && pc.Recalling) || (avatar != null && avatar.health <= 0f))
        {
            Aiming = false;
            weapons.Idle();
            CancelBurst();
            HideShield();
            return;
        }

        bool aimHeld = LucidInput.AimHeld;
        // In the boss fight, firing also brings up the free-aim shoulder camera: the lock-on view never aims for you.
        Aiming = w == World.Nightmare && (aimHeld || (ThirdPersonCamera.LockedOn && weapons.Firing));
        if (pc != null && pc.LastHitTime > dreadHitSeen) { dreadHitSeen = pc.LastHitTime; Dread *= BalanceSheet.Load().dreadKeptOnHit; }
        if (w == World.Nightmare) weapons.Tick(); else weapons.Idle();
        if (w == World.Nightmare && IsChargingBurst) CancelBurst();
        if (w == World.Dream && aimHeld && !IsChargingBurst && Time.time >= nextBurst)
        {
            chargeAge = 0f;
            IsChargingBurst = true;
            LucidAudio.Play(LucidAudio.Cue.Charge);
            chargingVisual = BurstVisual.Begin(transform, 4f, 0f);
        }

        if (IsChargingBurst)
        {
            chargeAge += Time.deltaTime;
            if (chargingVisual != null) chargingVisual.SetCharge(BurstCharge01);
            if (!aimHeld) ReleaseBurst();
        }

        ShieldUp = w == World.Dream && IsChargingBurst && aimHeld;
        if (pc != null && pc.LastHitTime > resonanceSince && resonanceAbsorbs > 0) ResetResonance();
        Kick = Mathf.MoveTowards(Kick, 0f, 9f * Time.deltaTime);
        bool striking = w == World.Dream && LucidInput.Fire.IsPressed() && !IsChargingBurst;
        if (striking || IsChargingBurst || Aiming) FaceAim();
        if (striking && Time.time >= nextShot) Fire(false);

        if (shield != null)
        {
            if (shieldRenderer != null)
            {
                float r = (ResonanceLevel - 1) / 4f;
                shieldBlock.SetColor(ShieldColorId, Color.Lerp(new Color(0.3f, 0.9f, 1f), new Color(1f, 0.92f, 0.55f), r) * (1f + r * 1.5f));
                shieldRenderer.SetPropertyBlock(shieldBlock);
            }
            shieldScale = Mathf.MoveTowards(shieldScale, ShieldUp ? shieldRadius * 2f : 0f, 30f * Time.deltaTime);
            shield.transform.localScale = Vector3.one * shieldScale;
            shield.SetActive(shieldScale > 0.001f);
        }
    }

    public Vector3 FlatAim()
    {
        Vector3 f = cam != null ? cam.transform.forward : transform.forward;
        // The aim camera sits off the shoulder: shoot at the point under the crosshair, not parallel to the view.
        if (cam != null && ThirdPersonCamera.Aiming) f = cam.transform.position + f * 30f - transform.position;
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

    void Fire(bool heavy, float damage = 0f)
    {
        nextShot = Time.time + 1f / Mathf.Max(1f, fireRate);
        lastShot = Time.time;
        Kick = 1f;
        HeavyCast = heavy;
        Vector3 aim = FlatAim();
        Vector3 right = Vector3.Cross(Vector3.up, aim);
        Vector3 origin = transform.position + Vector3.up * 1.2f + aim * 0.7f + right * (heavy ? 0.48f : -0.48f);
        Enemy target = null;   // no aim assist: shots go where the crosshair points
        Vector3 dir = aim;
        if (target != null)
        {
            Vector3 to = (target.transform.position - origin).normalized;
            dir = Vector3.Slerp(aim, to, 0.85f);
        }
        if (!heavy) dir = Quaternion.Euler(0f, Random.Range(-spreadDegrees, spreadDegrees), 0f) * dir;

        var shard = CombatAssets.Spawn("Shard", origin, Quaternion.LookRotation(dir));
        Color tint = heavy ? new Color(0.85f, 0.2f, 1f) : new Color(0.12f, 0.9f, 1f);
        if (shard != null && shard.TryGetComponent<Projectile>(out var projectile))
        {
            projectile.Init(dir, target);
            projectile.Configure((heavy ? damage : threadDamage) * (pc != null ? pc.TakeDashShotBonus() : 1f), tint, heavy);
            projectile.owner = this;
        }
        Fx.Burst(origin, tint, heavy ? 22 : 4);
        LucidAudio.Play(heavy ? LucidAudio.Cue.Burst : LucidAudio.Cue.Thread);
        ThirdPersonCamera.Shake(heavy ? 0.13f : 0.012f);
    }

    public void AddPower(float amount) { Power = Mathf.Clamp(Power + amount, 0f, maxPower); }

    public bool TrySpendPower(float fraction)
    {
        if (Power < maxPower * fraction) return false;
        Power = 0f;
        return true;
    }

    void ReleaseBurst()
    {
        float charge = BurstCharge01;
        float power = maxPower > 0f ? Power / maxPower : 0f;
        CancelBurst();
        if (charge < 0.08f) return;
        float damage = burstDamage * Mathf.Lerp(0.16f, 0.65f, charge) * (1f + power * 0.65f);
        if (WorldState.Instance != null && WorldState.Instance.Current == World.Nightmare) Power = 0f;
        nextBurst = Time.time + burstCooldown;
        Fire(true, damage);
    }

    void CancelBurst()
    {
        IsChargingBurst = false;
        ShieldUp = false;
        chargeAge = 0f;
        if (chargingVisual != null) chargingVisual.Cancel();
        chargingVisual = null;
    }

    void HideShield()
    {
        ShieldUp = false;
        shieldScale = 0f;
        if (shield != null)
        {
            shield.transform.localScale = Vector3.zero;
            shield.SetActive(false);
        }
    }

    void OnDisable()
    {
        Aiming = false;
        CancelBurst();
        HideShield();
    }

    public void OnAbsorb(Vector3 where)
    {
        Power = Mathf.Min(maxPower, Power + 1f);
        int before = ResonanceLevel;
        resonanceAbsorbs++;
        if (ResonanceLevel != before) LucidAudio.PlayResonance(ResonanceLevel);
        if (avatar != null)
        {
            avatar.health = Mathf.Min(1f, avatar.health + 0.012f);
            avatar.Sharpen();
        }
        LucidAudio.Play(LucidAudio.Cue.Capture);
        Fx.Burst(where, new Color(0.15f, 1f, 0.85f), 10);
        ThirdPersonCamera.Shake(0.02f);
    }
}
