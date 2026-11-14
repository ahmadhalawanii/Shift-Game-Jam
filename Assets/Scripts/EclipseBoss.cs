using UnityEngine;
using UnityEngine.InputSystem;

// One timeline owns the encounter. The AttackDirector picks attacks; this class owns phases,
// the world-swap timer, Dread, the Nightmare damage multiplier and the Sleepwalk wake window.
public class EclipseBoss : Enemy
{
    [Tooltip("Overwritten from BossAttackData.bossHealth on Start.")]
    public float maxHealth = 300f;
    public float activationDistance = 25.5f;
    [Range(0.5f, 1f)] public float bindingPowerFraction = 1f;
    public bool Sealed => BindingReady || beat == Beat.PhaseTell || vanish < 0.5f || (!Stunned && CurrentWorld == World.Dream);
    public bool BindingReady { get; private set; }
    public bool WakeActive { get; private set; }
    public float WakeCharge01 => Mathf.Clamp01(wakeCharge / sheet.lucidShotCharge);
    public float SwapSeconds => Mathf.Max(0f, swapAt - Time.time);
    public float Swap01 => 1f - Mathf.Clamp01(SwapSeconds / Mathf.Max(.1f, PhaseData.swapInterval));
    float wakeUntil, wakeGraceUntil, wakeCharge;
    LineRenderer wakeBeam;
    Material wakeMaterial;
    public void BeginWakeWindow()
    {
        if (WakeActive || Time.time < wakeGraceUntil || EclipseEncounter.Finished) return;
        WakeActive = true; BindingReady = true; wakeCharge = 0f; wakeUntil = Time.time + sheet.wakeWindowSeconds;
        director.StopAll(); EnemyBullet.ClearBoss(); GroundWave.ClearAll(); BossHazards.ClearAll();
        SetVanish(1f);
        if (WorldState.Instance != null) WorldState.Instance.HoldBoundary = true;
        if (WorldShift.Instance != null) WorldShift.Instance.HoldBoundary(true);
        Tell("WAKE WINDOW / HOLD FIRE TO CHARGE, RELEASE TO AWAKEN", sheet.wakeWindowSeconds);
        LucidAudio.Play(LucidAudio.Cue.Charge);
    }
    void TickWakeWindow()
    {
        if (LucidInput.Fire.IsPressed()) wakeCharge = Mathf.Min(sheet.lucidShotCharge, wakeCharge + Time.deltaTime);
        if (wakeBeam == null)
        {
            wakeMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); wakeMaterial.SetColor("_BaseColor", new Color(2f, 1.1f, 1.6f));
            var go = new GameObject("Lucid Shot"); go.transform.SetParent(transform);
            wakeBeam = go.AddComponent<LineRenderer>(); wakeBeam.sharedMaterial = wakeMaterial; wakeBeam.positionCount = 24;
        }
        wakeBeam.enabled = wakeCharge > 0f;
        Vector3 hand = player.position + Vector3.up * 1.2f;
        for (int i = 0; i < 24; i++)
        {
            float t = i / 23f, a = t * 18f - Time.time * 7f;
            wakeBeam.SetPosition(i, Vector3.Lerp(hand, transform.position, t) + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Mathf.Sin(t * Mathf.PI) * WakeCharge01);
        }
        wakeBeam.widthMultiplier = .035f + WakeCharge01 * .1f;
        if (LucidInput.Fire.WasReleasedThisFrame())
        {
            if (WakeCharge01 >= 1f)
            {
                EndWakeWindow(true); wakeBeam.widthMultiplier = 1.2f;
                CombatPulse.Spawn(transform.position, 18f, new Color(1f,.7f,.85f));
                GameFeel.HitStop(.16f); LucidAudio.Play(LucidAudio.Cue.Burst); health = 0f; Die(); return;
            }
            wakeCharge = 0f;
        }
        if (Time.time >= wakeUntil) EndWakeWindow(false);
    }
    void EndWakeWindow(bool success)
    {
        WakeActive = false; BindingReady = false;
        if (WorldState.Instance != null) { WorldState.Instance.HoldBoundary = false; if (success) WorldState.Instance.Set(World.Dream); }
        if (WorldShift.Instance != null) WorldShift.Instance.HoldBoundary(false);
        if (wakeBeam != null) wakeBeam.enabled = success;
        if (!success)
        {
            health = maxHealth * sheet.wakeWindowAt; wakeGraceUntil = Time.time + sheet.wakeWindowGrace;
            beat = Beat.Fight; director.Rest(.5f); director.ScheduleTeleport(); swapAt = Time.time + PhaseData.swapInterval;
            Tell("THE SLEEPER STIRS / ANOTHER CHANCE WILL COME", 2f);
        }
    }
    void OnDestroy() { if (wakeMaterial != null) Destroy(wakeMaterial); }
    public bool InTransformation => beat == Beat.PhaseTell;

    public bool Engaged { get; private set; }
    public bool Charging => beat == Beat.PhaseTell || Time.time < tellUntil;
    public int Phase { get; private set; } = 1;
    public float ChargeAmount => beat == Beat.PhaseTell ? 1f - Mathf.Clamp01(remaining / tellDuration)
        : Time.time < tellUntil ? 1f - Mathf.Clamp01((tellUntil - Time.time) / tellDuration) : 0f;
    public string AttackName { get; private set; } = "THE SLEEPER";

    // Fight state read by the HUD and visuals.
    public bool Stunned => Time.time < stunUntil;
    public bool Teleporting => director != null && director.Teleporting;
    public float Dread => playerCombat != null ? playerCombat.Dread : 0f;
    public BossWeakPoints WeakPoints { get; private set; }
    BalanceSheet sheet;
    float windowDamage;
    int seedsLeft;
    float nextSeed, seedHealing;
    bool seedLegendShown;
    public float ClaimSeedHealing(float amount)
    {
        float granted = Mathf.Min(amount, Mathf.Max(0f, sheet.seedWindowHealCap - seedHealing));
        seedHealing += granted;
        return granted;
    }
    void BeginSeedWindow()
    {
        seedsLeft = sheet.seedStreamCount; seedHealing = 0f; nextSeed = Time.time + .5f;
        if (!seedLegendShown) { seedLegendShown = true; Tell("PINK = HEALS / HOLD WARD TO CATCH / GOLD & CYAN DRAIN WARD", 6f); }
    }
    void SeedStream()
    {
        if (CurrentWorld != World.Dream || seedsLeft <= 0 || Time.time < nextSeed || beat == Beat.PhaseTell) return;
        nextSeed = Time.time + Mathf.Min(sheet.seedStreamInterval, PhaseData.swapInterval * .4f / sheet.seedStreamCount); seedsLeft--;
        Vector3 direction = PlayerPosition - Emitter; direction.y = 0f;
        float speed = Mathf.Max(sheet.seedSpeed, direction.magnitude / Mathf.Max(.5f, SwapSeconds * .4f));
        var seed = EnemyBullet.SpawnBoss(Emitter, direction.normalized, speed, .01f, World.Dream);
        if (seed != null) seed.HeartSeed().Home(1f, 30f);
    }
    public bool SwapWarning => swapWarned;
    public World SwapTarget => swapTarget;
    public float Phase3Since { get; private set; }
    public float DamageMultiplier
    {
        get
        {
            return playerCombat != null ? playerCombat.DreadMultiplier : 1f;
        }
    }

    // Context for attacks.
    public BossAttackData Data { get; private set; }
    public BossPhaseData PhaseData => Data.Phase(Phase);
    public float Difficulty => Mathf.Clamp(Data.difficulty, 0.7f, 1.5f);
    public Vector3 Home => EclipseEncounter.ArenaCenter + Vector3.up * 7f;
    public Vector3 Emitter => new Vector3(transform.position.x, 1.1f, transform.position.z);
    public Transform PlayerTransform => player;
    public Vector3 PlayerPosition => player != null ? player.position : EclipseEncounter.ArenaCenter;
    public Vector3 PlayerVelocity => pc != null ? pc.Velocity : Vector3.zero;
    public Vector3 CameraForward { get { var f = cam != null ? cam.forward : Vector3.forward; f.y = 0f; return f.normalized; } }
    public Vector3 CameraRight => Vector3.Cross(Vector3.up, CameraForward);
    public AttackDirector Director => director;

    enum Beat { Dormant, Intro, Fight, PhaseTell, Dead }
    Beat beat;
    float remaining, tellDuration = 1f, tellUntil;
    PlayerCombat playerCombat;
    PlayerController pc;
    Transform cam;
    AttackDirector director;
    float nextWeave, stunUntil, nightmareSince, swapAt;
    float vanish = 1f;
    const float BaseScale = 1.25f;
    bool swapWarned, forcingSwap;
    World swapTarget;

    protected override void Start()
    {
        base.Start();
        Data = BossAttackData.Load();
        sheet = BalanceSheet.Load();
        maxHealth = sheet.bossHealth;
        WeakPoints = gameObject.AddComponent<BossWeakPoints>();
        gameObject.AddComponent<SilentSwapBeat>();
        health = maxHealth;
        radius = 3.4f * BaseScale;
        transform.localScale = Vector3.one * BaseScale;
        deathColor = new Color(1f, 0.28f, 0.12f);
        if (GetComponent<EclipseVisual>() == null) gameObject.AddComponent<EclipseVisual>();
        if (player != null)
        {
            playerCombat = player.GetComponent<PlayerCombat>();
            pc = player.GetComponent<PlayerController>();
        }
        if (Camera.main != null) cam = Camera.main.transform;
        director = new AttackDirector(this, Data);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        WorldState.Changed += OnWorldChanged;
    }

    protected override void OnDisable()
    {
        if (WorldState.Instance != null) WorldState.Instance.HoldBoundary = false;
        if (WorldShift.Instance != null) WorldShift.Instance.HoldBoundary(false);
        WorldState.Changed -= OnWorldChanged;
        beat = Beat.Dead;
        Engaged = false;
        if (director != null) director.StopAll();
        EnemyBullet.ClearBoss();
        GroundWave.ClearAll();
        BossHazards.ClearAll();
        base.OnDisable();
    }

    void Weave()
    {
        if (CurrentWorld != World.Dream || playerCombat == null) return;
        if (BindingReady && playerCombat.TrySpendPower(bindingPowerFraction))
        {
            CombatPulse.Spawn(transform.position, 14f, new Color(0.6f, 0.95f, 1f));
            Die();
        }
        else if (BindingReady)
            Tell("ABSORB DREAM SEEDS TO COMPLETE THE BINDING", 2f);
        else if (playerCombat.LucidPulseReady)
            LucidPulse();
        else if (Time.time >= nextWeave &&
            (player.position - DreamAnchorPosition).sqrMagnitude < 16f)
        {
            nextWeave = Time.time + 7f;
            playerCombat.AddPower(2f);
            CombatPulse.Spawn(DreamAnchorPosition, 4f, Color.cyan);
            Tell("ANCHOR WOVEN  +2 POWER", 1.4f);
        }
    }

    // Resonance x5: a ring that hits the boss through its Dream armour and clears nearby orbs.
    void LucidPulse()
    {
        playerCombat.ResetResonance();
        Vector3 at = player.position + Vector3.up;
        CombatPulse.Spawn(at, Data.lucidPulseRadius, new Color(1f, 0.95f, 0.6f));
        CombatPulse.Spawn(at, Data.lucidPulseRadius * 0.6f, new Color(0.5f, 1f, 1f));
        EnemyBullet.Clear(at, Data.lucidPulseRadius);
        Fx.Burst(at, new Color(1f, 0.9f, 0.5f), 120);
        ThirdPersonCamera.Shake(0.35f);
        GameFeel.HitStop(0.05f);
        LucidAudio.Play(LucidAudio.Cue.Burst);
        TakePulseDamage(Data.lucidPulseDamage, transform.position);
        Tell("LUCID PULSE", 1.5f);
    }

    public static Vector3 DreamAnchorPosition => EclipseEncounter.ArenaCenter + Vector3.left * 13f + Vector3.up;

    void Update()
    {
        if (DreamcatcherPresentation.CoversHud) return;
        if (player == null || EclipseEncounter.Finished || beat == Beat.Dead) return;
        DebugKeys();
        if (WakeActive) { TickWakeWindow(); return; }
        if (Phase == 3 && health <= maxHealth * sheet.wakeWindowAt && Time.time >= wakeGraceUntil) { BeginWakeWindow(); return; }
        if (!Engaged)
        {
            Vector3 fromCenter = player.position - EclipseEncounter.ArenaCenter;
            fromCenter.y = 0f;
            if (fromCenter.sqrMagnitude > activationDistance * activationDistance) return;
            Engage();
        }

        float dt = Time.deltaTime;
        Vector3 facing = player.position - transform.position;
        facing.y = 0f;
        if (facing.sqrMagnitude > 4f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(-facing),
                1f - Mathf.Exp(-3f * dt));
        SeedStream();
        if (EclipseEncounter.Finished) return;
        remaining -= dt;
        WorldPressure(dt);

        switch (beat)
        {
            case Beat.Intro:
                if (remaining <= 0f) { beat = Beat.Fight; director.ScheduleTeleport(); director.Rest(0f); }
                break;
            case Beat.Fight:
                if (director.Tick()) TryPhaseChange();
                break;
            case Beat.PhaseTell:
                if (remaining <= 0f) { beat = Beat.Fight; director.Rest(0.3f); }
                break;
        }
    }

    void Engage()
    {
        Engaged = true;
        if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.BeginBossFight();
        Tell("THE SLEEPER  /  HOLD RIGHT CLICK TO CAPTURE DREAM SEEDS", 3f);
        beat = Beat.Intro;
        remaining = 2.5f;
        swapAt = Time.time + 2.5f + PhaseData.swapInterval;
        if (CurrentWorld == World.Dream) BeginSeedWindow();
    }

    void TryPhaseChange()
    {
        if (!((Phase == 1 && health <= maxHealth * sheet.phase2At) || (Phase == 2 && health <= maxHealth * sheet.phase3At))) return;
        EnterPhase(Phase + 1, 2.6f);
    }

    void EnterPhase(int phase, float tell)
    {
        director.StopAll();
        Phase = phase;
        BindingReady = false;
        LucidAudio.Play(LucidAudio.Cue.Fusion);
        EnemyBullet.ClearBoss();
        GroundWave.ClearAll();
        BossHazards.ClearAll();
        AttackName = Phase == 1 ? "THE SLEEPER" : Phase == 2 ? "AWAKENED NIGHTMARE" : "THE BOUNDARY IS BREAKING";
        Tell(AttackName, tell);
        beat = Beat.PhaseTell;
        remaining = tellDuration = tell;
        if (Phase == 3) Phase3Since = Time.time + tell;
        director.ScheduleTeleport();
        swapAt = Time.time + tell + PhaseData.swapInterval;
        swapWarned = false;
        if (!DreamcatcherPresentation.ReduceFlash)
            CombatPulse.Spawn(transform.position, Phase == 3 ? 18f : 10f,
                Phase == 3 ? new Color(0.8f, 0.25f, 1f) : new Color(1f, 0.42f, 0.08f));
        ThirdPersonCamera.Shake(0.28f);
        if (phase == 3) DreamcatcherPresentation.PlayFusion(this);
    }

    // Timed world swaps with a 1.2 s warning, plus Dread that pushes the player out of a long Nightmare.
    void WorldPressure(float dt)
    {
        var ws = WorldState.Instance;
        if (ws == null) return;
        bool timed = beat == Beat.Fight && !BindingReady && !Teleporting;
        if (!timed) swapAt = Mathf.Max(swapAt, Time.time + 1f);
        else
        {
            if (!swapWarned && Time.time >= swapAt - Data.swapTelegraph)
            {
                swapWarned = true;
                swapTarget = ws.Current == World.Dream ? World.Nightmare : World.Dream;
                Tell("THE WORLD TURNS  /  " + (swapTarget == World.Dream ? "DREAM" : "NIGHTMARE") + "  /  SHIFT NOW FOR +POWER", Data.swapTelegraph);
                LucidAudio.Play(LucidAudio.Cue.Warning);
            }
            if (swapWarned && Time.time >= swapAt) ForceWorld(swapTarget);
        }

        if (beat == Beat.Dormant || beat == Beat.Intro) return;
    }

    void ForceWorld(World w)
    {
        if (WorldState.Instance == null) return;
        forcingSwap = true;
        LucidAudio.Hook("boss_swap");
        WorldState.Instance.Set(w);
        forcingSwap = false;
        swapWarned = false;
        swapAt = Time.time + PhaseData.swapInterval;
    }

    void OnWorldChanged(World w)
    {
        if (Engaged && w == World.Dream) BeginSeedWindow();
        if (w == World.Nightmare) nightmareSince = Time.time;
        windowDamage = 0f;   // each Nightmare window has its own damage cap
        if (swapWarned && !forcingSwap && w == swapTarget && playerCombat != null && player != null)
        {
            playerCombat.AddPower(Data.shiftRewardPower);
            EnemyBullet.ClearDanger(player.position, Data.shiftRewardClearRadius);
            CombatPulse.Spawn(player.position + Vector3.up, Data.shiftRewardClearRadius, new Color(0.5f, 1f, 1f));
            Tell("PERFECT SHIFT  +" + Mathf.RoundToInt(Data.shiftRewardPower) + " POWER", 1.4f);
        }
        if (Engaged)
        {
            swapWarned = false;
            swapAt = Time.time + PhaseData.swapInterval;
        }
    }

    // ---------- Attack context ----------

    public float Tele(float seconds, bool small = false)
    {
        float min = small && Phase == 3 ? 0.35f : 0.5f;
        return Mathf.Max(min, seconds * PhaseData.telegraphScale / Difficulty);
    }

    public void BeginTell(string name, float seconds)
    {
        AttackName = name;
        tellDuration = Mathf.Max(0.05f, seconds);
        tellUntil = Time.time + seconds;
        Tell(name, seconds + 0.6f);
    }

    // Damage is out of 100. Dangerous Dream patterns are cyan/gold; Heart Seeds use the budgeted stream.
    public EnemyBullet Orb(Vector3 origin, Vector3 direction, float speed, float damage, World world,
        Color color, float thorn, float scale, bool allowGold = true)
    {
        var b = EnemyBullet.SpawnBoss(origin, direction, speed, damage / 100f, world);
        if (b == null) return null;
        b.Look(world == World.Dream ? (thorn > .5f ? BossAttack.Gold : BossAttack.Cyan) : color, thorn, scale);
        return b;
    }

    public bool InsideArena(Vector3 p, float margin)
    {
        Vector3 d = p - EclipseEncounter.ArenaCenter; d.y = 0f;
        float m = d.magnitude;
        return m > EclipseArena.CoreRadius + 1f + margin && m < EclipseArena.SqueezeRadius - 0.5f - margin;
    }

    public Vector3 ClampToArena(Vector3 p, float margin)
    {
        Vector3 d = p - EclipseEncounter.ArenaCenter; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) d = Vector3.back;
        float lo = EclipseArena.CoreRadius + 1f + margin, hi = Mathf.Max(lo, EclipseArena.SqueezeRadius - 1f - margin);
        return EclipseEncounter.ArenaCenter + d.normalized * Mathf.Clamp(d.magnitude, lo, hi);
    }

    public void MoveTo(Vector3 position) { transform.position = position; }

    public void SetVanish(float visible)
    {
        vanish = Mathf.Clamp01(visible);
        transform.localScale = Vector3.one * BaseScale * Mathf.Max(0.001f, vanish);
    }

    public void Stun(float seconds)
    {
        stunUntil = Time.time + seconds;
        LucidAudio.Hook("boss_stagger");
        Fx.Burst(transform.position, new Color(0.6f, 1f, 1f), 60);
        Tell("STAGGERED  /  STRIKE NOW", seconds);
    }
    public void Stagger(float seconds)
    {
        if (!Engaged || WakeActive || InTransformation) return;
        director.StopAll(); director.Rest(seconds); SetVanish(1f);
        Stun(seconds);
    }

    World CurrentWorld => WorldState.Instance != null ? WorldState.Instance.Current : World.Dream;

    // Exposed as deterministic geometry for numerical tests: every curtain has
    // five consecutive missing beads, and the opening moves only one bead per row.
    public static float HaloAngle(int bead) => -70f + bead * 5f;
    public static bool IsHaloGap(int bead, int row)
    {
        int center = 13 + (row % 4);
        return Mathf.Abs(bead - center) <= 2;
    }
    public static float HelixAngle(int bead, bool mirrored)
    {
        float bend = 20f + Mathf.Sin(bead * 0.13f) * 42f;
        return mirrored ? -bend : bend;
    }

    static void Tell(string text, float duration)
    {
        if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.Tell(text, duration);
    }

    public override void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (!Engaged || EclipseEncounter.Finished) return;
        if (BindingReady || beat == Beat.PhaseTell || vanish < 0.5f)
        {
            Fx.Burst(hitPoint, new Color(0.65f, 0.85f, 1f), 5);
            return;
        }
        // Armoured in Dream: only a tenth gets through. The Dread multiplier is applied by the weapons.
        ApplyDamage(damage * (!Stunned && CurrentWorld == World.Dream ? sheet.dreamDamageScale : 1f), hitPoint);
    }

    // Lucid Pulse goes through Dream armour, but never through a transformation or the finale seal.
    public void TakePulseDamage(float damage, Vector3 hitPoint)
    {
        if (!Engaged || EclipseEncounter.Finished || BindingReady || beat == Beat.PhaseTell) return;
        ApplyDamage(damage, hitPoint);
    }

    void ApplyDamage(float damage, Vector3 hitPoint)
    {
        // Each threshold is a gate, so a single heavy shot cannot skip a transformation.
        float floor = Phase == 1 ? maxHealth * sheet.phase2At : Phase == 2 ? maxHealth * sheet.phase3At : maxHealth * sheet.wakeWindowAt;
        // One Nightmare window removes at most windowCap of the boss's health at full value; the rest is reduced.
        float room = Mathf.Max(0f, maxHealth * sheet.windowCap - windowDamage);
        float full = Mathf.Min(damage, room);
        damage = full + (damage - full) * sheet.overCapScale;
        windowDamage += damage;
        float previous = health;
        health = Mathf.Max(floor, health - damage);
        if (health < previous) Fx.Burst(hitPoint, Stunned ? new Color(0.6f, 1f, 1f) : new Color(1f, 0.6f, 0.2f), Stunned ? 12 : 6);
        if (Phase == 3 && health <= floor && !BindingReady && Time.time >= wakeGraceUntil)
        {
            BeginWakeWindow();
        }
    }

    protected override void Die()
    {
        bool cinematic = DreamcatcherPresentation.PlayEnding(this);
        if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.End(true);
        if (!cinematic) base.Die();
    }

    // Editor and development builds only: F5/F6/F7 phase, N swap, T teleport, V cycle attacks, K kill, H hitboxes.
    // Any of them before the fight starts drops the player into the arena.
    void DebugKeys()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var kb = Keyboard.current;
        if (kb == null) return;
        bool any = kb.f5Key.wasPressedThisFrame || kb.f6Key.wasPressedThisFrame || kb.f7Key.wasPressedThisFrame ||
            kb.nKey.wasPressedThisFrame || kb.tKey.wasPressedThisFrame || kb.vKey.wasPressedThisFrame ||
            kb.kKey.wasPressedThisFrame || kb.hKey.wasPressedThisFrame || kb.f9Key.wasPressedThisFrame;
        if (!any) return;
        if (!Engaged)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.position = EclipseEncounter.ArenaCenter + Vector3.back * 18f + Vector3.up * 0.15f;
            if (controller != null) controller.enabled = true;
            Engage();
            beat = Beat.Fight;
            director.ScheduleTeleport();
        }
        if (kb.f5Key.wasPressedThisFrame) DebugPhase(1);
        if (kb.f6Key.wasPressedThisFrame) DebugPhase(2);
        if (kb.f7Key.wasPressedThisFrame) DebugPhase(3);
        if (kb.f9Key.wasPressedThisFrame) { Phase = 3; beat = Beat.Fight; health = maxHealth * sheet.wakeWindowAt; wakeGraceUntil = 0f; BeginWakeWindow(); }
        if (kb.nKey.wasPressedThisFrame) ForceWorld(CurrentWorld == World.Dream ? World.Nightmare : World.Dream);
        if (kb.tKey.wasPressedThisFrame) { beat = Beat.Fight; director.ForceTeleport(); }
        if (kb.vKey.wasPressedThisFrame) { beat = Beat.Fight; Tell("DEBUG  /  " + director.CycleNext(), 2f); }
        if (kb.kKey.wasPressedThisFrame) Die();
        if (kb.hKey.wasPressedThisFrame) { BossHazards.ShowHitboxes = !BossHazards.ShowHitboxes; Tell("HITBOXES " + (BossHazards.ShowHitboxes ? "ON (enable Gizmos)" : "OFF"), 1.5f); }
#endif
    }

    void DebugPhase(int phase)
    {
        health = phase == 1 ? maxHealth : phase == 2 ? maxHealth * (2f / 3f) : maxHealth / 3f;
        EnterPhase(phase, 1f);
    }
}
