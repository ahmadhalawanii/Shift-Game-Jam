using UnityEngine;

// The three Nightmare weapons: Thorn Revolver (precision), Gloam Scatter (close range), Wraith Chain (heat-limited beam).
// All numbers come from BalanceSheet. Shots are hitscan on the horizontal plane, straight along the crosshair:
// there is no aim assist and nothing homes. PlayerCombat calls Tick() while the player is in Nightmare.
public class PlayerWeapons : MonoBehaviour
{
    public int Index { get; private set; }
    public int Count { get { return sheet.weapons.Length; } }
    public WeaponData Current { get { return sheet.weapons[Index]; } }
    public WeaponData Data(int i) { return sheet.weapons[i]; }
    public int Mag(int i) { return mag[i]; }
    public int Reserve(int i) { return reserve[i]; }
    public bool Reloading { get { return reloadLeft[Index] > 0f; } }
    public float Reload01 { get { return Reloading ? 1f - reloadLeft[Index] / reloadTotal[Index] : 0f; } }
    public bool ReloadMissed { get { return tried[Index] && Reloading; } }
    public float Heat01 { get; private set; }
    public bool Overheated { get { return Time.time < lockoutUntil; } }
    public bool Buffed { get { return Time.time < buffUntil; } }
    public bool Firing { get { return Time.time - lastShot < 0.5f; } }
    public bool Overdrive { get { return Time.time < overdriveUntil; } }
    [System.NonSerialized] public float overdriveUntil = -99f;
    [System.NonSerialized] public float empowered = 1f;     // multiplier for the next shot (Shadowstep)

    BalanceSheet sheet;
    PlayerCombat combat;
    PlayerController pc;
    int[] mag, reserve;
    float[] reloadLeft, reloadTotal, carry;
    bool[] tried;
    float nextShot, lastShot = -99f, switchUntil, buffUntil, lockoutUntil, nextBeamTick;
    LineRenderer[] tracers;
    float[] tracerAge;
    LineRenderer beam;
    Light muzzle;
    const float TracerLife = 0.09f;

    void Awake()
    {
        sheet = BalanceSheet.Load();
        combat = GetComponent<PlayerCombat>();
        pc = GetComponent<PlayerController>();
        int n = Count;
        mag = new int[n]; reserve = new int[n]; reloadLeft = new float[n]; reloadTotal = new float[n]; carry = new float[n]; tried = new bool[n];
        Refill();
    }

    void Start()
    {
        var shard = CombatAssets.Get("Shard");
        var trail = shard != null ? shard.GetComponentInChildren<TrailRenderer>() : null;
        var burst = CombatAssets.Get("GlowBurst");
        Material mat = trail != null ? trail.sharedMaterial : burst != null ? burst.GetComponent<ParticleSystemRenderer>().sharedMaterial : null;
        tracers = new LineRenderer[12];
        tracerAge = new float[12];
        for (int i = 0; i < tracers.Length; i++) { tracers[i] = Line("Tracer", mat, 2); tracerAge[i] = 99f; }
        beam = Line("Chain beam", mat, 14);
        var lg = new GameObject("Muzzle light");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0.3f, 1.2f, 0.8f);
        muzzle = lg.AddComponent<Light>();
        muzzle.type = LightType.Point; muzzle.range = 7f; muzzle.intensity = 0f;
    }

    LineRenderer Line(string name, Material mat, int points)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var l = go.AddComponent<LineRenderer>();
        l.sharedMaterial = mat;
        l.useWorldSpace = true;
        l.positionCount = points;
        l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        l.textureMode = LineTextureMode.Stretch;
        l.enabled = false;
        return l;
    }

    // Full magazines and the starting reserve (fight start, checkpoints).
    public void Refill()
    {
        for (int i = 0; i < Count; i++) { mag[i] = Data(i).magazine; reserve[i] = Data(i).startReserve; reloadLeft[i] = 0f; }
    }

    // Heart Seeds: rounds per weapon in the order of the sheet. Fractions carry over to the next seed.
    public void AddAmmo(float revolver, float scatter)
    {
        for (int i = 0; i < Count; i++)
        {
            float add = Data(i).kind == WeaponKind.Revolver ? revolver : Data(i).kind == WeaponKind.Scatter ? scatter : 0f;
            carry[i] += add;
            int whole = Mathf.FloorToInt(carry[i]);
            carry[i] -= whole;
            reserve[i] += whole;
        }
    }
    // Stage D activates the existing Overdrive multipliers, cancelling reload/heat lockout.
    public void BeginOverdrive(float seconds)
    {
        overdriveUntil = Time.time + seconds; lockoutUntil = 0f; Heat01 = 0f;
        for (int i = 0; i < Count; i++) reloadLeft[i] = 0f;
    }

    public void Select(int i)
    {
        i = (i % Count + Count) % Count;
        if (i == Index) return;
        Index = i;
        switchUntil = Time.time + sheet.weaponSwapTime;   // reload progress of each weapon is kept
        LucidAudio.Sfx("swap");
    }

    // Hide the beam and let heat cool (Dream stance, or dead).
    public void Idle()
    {
        if (beam != null) beam.enabled = false;
        Cool();
    }

    void Cool() { if (!Overheated) Heat01 = Mathf.Max(0f, Heat01 - Time.deltaTime / 2f); else Heat01 = Mathf.Clamp01((lockoutUntil - Time.time) / Mathf.Max(0.1f, Current.lockoutSeconds)); }

    public void Tick()
    {
        if (LucidInput.Weapon1.WasPressedThisFrame()) Select(0);
        if (LucidInput.Weapon2.WasPressedThisFrame()) Select(1);
        if (LucidInput.Weapon3.WasPressedThisFrame()) Select(2);
        if (LucidInput.NextWeapon.WasPressedThisFrame()) Select(Index + 1);
        if (LucidInput.PrevWeapon.WasPressedThisFrame()) Select(Index - 1);
        float wheel = LucidInput.WeaponWheel.WasPerformedThisFrame() ? LucidInput.WeaponWheel.ReadValue<float>() : 0f;
        if (wheel != 0f) Select(Index + (wheel > 0f ? -1 : 1));

        var w = Current;
        bool ready = Time.time >= switchUntil;
        if (w.kind == WeaponKind.Chain) { TickBeam(w, ready); return; }
        beam.enabled = false;
        Cool();

        // Reload. A second press inside the sweet spot finishes early and buffs damage; a miss locks the timing out.
        if (Reloading)
        {
            if (LucidInput.Reload.WasPressedThisFrame() && !tried[Index])
            {
                tried[Index] = true;
                float p = Reload01;
                if (p >= w.sweetSpotStart && p <= w.sweetSpotStart + w.sweetSpotWidth)
                {
                    reloadLeft[Index] = Mathf.Min(reloadLeft[Index], Mathf.Max(0.01f, w.activeReloadTime - (reloadTotal[Index] - reloadLeft[Index])));
                    reloadLeft[Index] = Mathf.Min(reloadLeft[Index], 0.12f);
                    buffUntil = Time.time + w.activeBuffSeconds;
                    tried[Index] = false;
                    LucidAudio.Sfx("reload_perfect");
                    if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.Tell("PERFECT RELOAD", 0.8f);
                }
                else LucidAudio.Sfx("empty");
            }
            reloadLeft[Index] -= Time.deltaTime;
            if (reloadLeft[Index] <= 0f)
            {
                int n = Mathf.Min(w.magazine - mag[Index], reserve[Index]);
                mag[Index] += n; reserve[Index] -= n; reloadLeft[Index] = 0f;
                LucidAudio.Sfx("reload");
            }
            return;
        }
        if (!Overdrive && LucidInput.Pressed(LucidInput.Reload) && mag[Index] < w.magazine && reserve[Index] > 0) { LucidInput.Consume(LucidInput.Reload); BeginReload(w); return; }

        // One click, one shot (the press is buffered).
        if (!ready || Time.time < nextShot || !LucidInput.Pressed(LucidInput.Fire)) return;
        LucidInput.Consume(LucidInput.Fire);
        if (mag[Index] <= 0 && !Overdrive)
        {
            LucidAudio.Sfx("empty");
            if (reserve[Index] > 0) BeginReload(w);
            return;
        }
        Shoot(w);
    }

    void BeginReload(WeaponData w)
    {
        reloadTotal[Index] = reloadLeft[Index] = w.reloadTime;
        tried[Index] = false;
        LucidAudio.Sfx("reload");
    }

    float Multiplier(WeaponData w)
    {
        float m = combat.DreadMultiplier * (Buffed ? w.activeBuff : 1f) * (Overdrive ? sheet.overdrive.amount : 1f) * empowered;
        empowered = 1f;
        return m;
    }

    Vector3 Origin(Vector3 aim) { return transform.position + Vector3.up * 1.2f + aim * 0.6f + Vector3.Cross(Vector3.up, aim) * 0.3f; }

    void Shoot(WeaponData w)
    {
        if (!Overdrive) mag[Index]--;
        nextShot = Time.time + 1f / (Mathf.Max(0.1f, w.shotsPerSecond) * (Overdrive ? sheet.overdriveFireRate : 1f));
        lastShot = Time.time;
        Vector3 aim = combat.FlatAim();
        Vector3 origin = Origin(aim);
        float mult = Multiplier(w) * (pc != null ? pc.TakeDashShotBonus() : 1f);
        float spread = w.spreadDegrees * (w.kind == WeaponKind.Revolver && combat.Aiming ? 0.3f : 1f);
        bool weak = false;
        for (int i = 0; i < Mathf.Max(1, w.pellets); i++)
        {
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * aim;
            Vector3 end = origin + dir * w.range;
            if (!EnemyBullet.Shoot(origin, end, sheet.thornShotPower))
            {
                float dist;
                var e = Ray(origin, dir, w.range, out end, out dist);
                if (e != null)
                {
                    float fall = w.falloffDistance > 0f ? Mathf.Lerp(1f, w.falloffFloor, Mathf.Clamp01(dist / w.falloffDistance)) : 1f;
                    weak |= Deal(e, end, w.damage * mult * fall, w);
                    Fx.Burst(end, w.color, 5);
                }
            }
            Tracer(origin, end, w);
        }
        // Feel: flash, light, recoil, punch, sound. Weak-point hits get a short hit-stop and a ring.
        Fx.Burst(origin, w.color, w.kind == WeaponKind.Scatter ? 26 : 9);
        Fx.Burst(origin, Color.white, 3);
        muzzle.color = w.color; muzzle.intensity = w.kind == WeaponKind.Scatter ? 9f : 5f;
        combat.KickNow();
        ThirdPersonCamera.Shake(w.shake);
        if (w.fovPunch > 0f) ThirdPersonCamera.FovPunch(w.fovPunch);
        LucidAudio.Sfx(w.kind == WeaponKind.Scatter ? "scatter" : "revolver");
        if (weak) GameFeel.HitStop(0.04f);
        if (mag[Index] <= 0 && reserve[Index] > 0 && !Overdrive) BeginReload(w);
    }

    void TickBeam(WeaponData w, bool ready)
    {
        bool on = ready && !Overheated && LucidInput.Fire.IsPressed();
        beam.enabled = on;
        if (!on) { Cool(); return; }
        lastShot = Time.time;
        if (!Overdrive) Heat01 += Time.deltaTime / Mathf.Max(0.1f, w.heatSeconds);
        Vector3 aim = combat.FlatAim();
        Vector3 origin = Origin(aim), end;
        float dist;
        var e = Ray(origin, aim, w.range, out end, out dist);
        bool half = false;
        if (e == null)
        {
            // Forgiving tracking: a target just outside the beam but inside the cone still takes half damage.
            e = InCone(origin, aim, w.range, w.lockConeDegrees);
            if (e != null) { half = true; end = e.transform.position; end.y = origin.y; }
        }
        if (Time.time >= nextBeamTick)
        {
            nextBeamTick = Time.time + 0.1f;
            EnemyBullet.Shoot(origin, end, sheet.thornShotPower);
            if (e != null) { Deal(e, end, w.damage * 0.1f * Multiplier(w) * (half ? 0.5f : 1f), w); Fx.Burst(end, w.color, 3); }
            LucidAudio.Sfx("chain");
            ThirdPersonCamera.Shake(w.shake);
        }
        // A curved violet ribbon from the hand to the hit point.
        Vector3 side = Vector3.Cross(Vector3.up, aim);
        int n = beam.positionCount;
        for (int i = 0; i < n; i++)
        {
            float t = i / (n - 1f);
            float bow = Mathf.Sin(t * Mathf.PI);
            beam.SetPosition(i, Vector3.Lerp(origin, end, t) + side * (Mathf.Sin(t * 9f - Time.time * 14f) * 0.22f * bow) + Vector3.up * (bow * 0.35f));
        }
        beam.widthMultiplier = 0.22f + Mathf.Sin(Time.time * 40f) * 0.04f;
        beam.startColor = w.color; beam.endColor = Color.Lerp(w.color, Color.white, 0.4f);
        muzzle.color = w.color; muzzle.intensity = 2.5f;
        if (Heat01 >= 1f)
        {
            lockoutUntil = Time.time + w.lockoutSeconds;
            beam.enabled = false;
            LucidAudio.Sfx("empty");
            if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.Tell("CHAIN OVERHEATED", 1f);
        }
    }

    // Applies damage. Returns true if a boss weak point was hit.
    bool Deal(Enemy e, Vector3 point, float damage, WeaponData w)
    {
        bool weak = false;
        var boss = e as EclipseBoss;
        if (boss != null && boss.WeakPoints != null)
        {
            int i = boss.WeakPoints.Hit(point);
            if (i >= 0)
            {
                weak = true;
                damage *= w.weakPointMultiplier;
                if (boss.WeakPoints.CashIn(i))
                {
                    damage *= sheet.cashInMultiplier;
                    combat.AddPower(sheet.cashInPower);
                    CombatPulse.Spawn(point, 3.5f, new Color(1f, 0.8f, 0.4f));
                    Fx.Burst(point, new Color(1f, 0.85f, 0.5f), 40);
                    if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.Tell("WEAK POINT CASHED IN", 0.9f);
                }
            }
        }
        e.TakeDamage(damage, point);
        combat.AddDread(damage);
        combat.HitConfirm();
        return weak;
    }

    // First enemy a flat ray meets. The test is on the horizontal plane, so height does not matter (within reason).
    public static Enemy Ray(Vector3 origin, Vector3 dir, float range, out Vector3 point, out float dist)
    {
        Enemy best = null;
        dist = range;
        for (int i = 0; i < Enemy.All.Count; i++)
        {
            var e = Enemy.All[i];
            if (e == null || e.health <= 0f) continue;
            Vector3 c = e.transform.position - origin;
            if (!(e is EclipseBoss) && Mathf.Abs(c.y) > 4.5f) continue;
            c.y = 0f;
            float along = Vector3.Dot(c, dir);
            if (along <= 0f) continue;
            float off2 = c.sqrMagnitude - along * along, r = e.radius;
            if (off2 > r * r) continue;
            float t = Mathf.Max(0f, along - Mathf.Sqrt(r * r - off2));
            if (t < dist) { dist = t; best = e; }
        }
        point = origin + dir * dist;
        return best;
    }

    static Enemy InCone(Vector3 origin, Vector3 dir, float range, float degrees)
    {
        Enemy best = null;
        float bestAngle = degrees;
        for (int i = 0; i < Enemy.All.Count; i++)
        {
            var e = Enemy.All[i];
            if (e == null || e.health <= 0f) continue;
            Vector3 c = e.transform.position - origin;
            c.y = 0f;
            if (c.magnitude > range + e.radius) continue;
            float a = Vector3.Angle(dir, c);
            if (a < bestAngle) { bestAngle = a; best = e; }
        }
        return best;
    }

    void Tracer(Vector3 from, Vector3 to, WeaponData w)
    {
        int slot = 0;
        for (int i = 1; i < tracers.Length; i++) if (tracerAge[i] > tracerAge[slot]) slot = i;
        var l = tracers[slot];
        tracerAge[slot] = 0f;
        l.enabled = true;
        l.SetPosition(0, from); l.SetPosition(1, to);
        l.startColor = Color.Lerp(w.color, Color.white, 0.6f); l.endColor = w.color;
        l.widthMultiplier = w.kind == WeaponKind.Scatter ? 0.1f : 0.16f;
    }

    void Update()
    {
        for (int i = 0; i < tracers.Length; i++)
        {
            if (!tracers[i].enabled) continue;
            tracerAge[i] += Time.deltaTime;
            if (tracerAge[i] >= TracerLife) { tracers[i].enabled = false; tracerAge[i] = 99f; }
            else tracers[i].widthMultiplier *= 1f - Time.deltaTime / TracerLife * 0.8f;
        }
        muzzle.intensity = Mathf.MoveTowards(muzzle.intensity, 0f, 60f * Time.deltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f8Key.wasPressedThisFrame) DpsDummy.Spawn(transform, sheet);
#endif
    }
}
