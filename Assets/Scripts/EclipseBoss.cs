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
    public float Dread { get; private set; }
    public bool SwapWarning => swapWarned;
    public World SwapTarget => swapTarget;
    public float Phase3Since { get; private set; }
    public float DamageMultiplier
    {
        get
        {
            if (CurrentWorld != World.Nightmare) return 1f;
            float since = Mathf.Max(nightmareSince, pc != null ? pc.LastHitTime : 0f);
            return 1f + (Data.maxDamageMultiplier - 1f) * Mathf.Clamp01((Time.time - since) / Mathf.Max(0.1f, Data.multiplierRampSeconds));
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
        maxHealth = Data.bossHealth;
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
        if (player == null || EclipseEncounter.Finished || beat == Beat.Dead) return;
        DebugKeys();
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
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Weave();
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
    }

    void TryPhaseChange()
    {
        if (!((Phase == 1 && health <= maxHealth * (2f / 3f)) || (Phase == 2 && health <= maxHealth / 3f))) return;
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
        CombatPulse.Spawn(transform.position, Phase == 3 ? 18f : 10f,
            Phase == 3 ? new Color(0.8f, 0.25f, 1f) : new Color(1f, 0.42f, 0.08f));
        ThirdPersonCamera.Shake(0.28f);
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
        if (ws.Current == World.Nightmare) Dread += dt / Mathf.Max(1f, Data.dreadFillSeconds);
        else Dread = Mathf.Max(0f, Dread - dt / Mathf.Max(0.5f, Data.dreadDrainSeconds));
        if (Dread > 0.55f) ThirdPersonCamera.Shake(dt * (Dread - 0.55f) * 0.5f);
        if (Dread >= 1f)
        {
            Dread = 0.6f;
            ForceWorld(World.Dream);
            if (pc != null) pc.Damage(Data.dreadDamage / 100f);
            Tell("DREAD OVERWHELMS YOU  /  BACK TO THE DREAM", 2f);
        }
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
        if (w == World.Nightmare) nightmareSince = Time.time;
        if (swapWarned && !forcingSwap && w == swapTarget && playerCombat != null && player != null)
        {
            playerCombat.AddPower(Data.shiftRewardPower);
            EnemyBullet.Clear(player.position, Data.shiftRewardClearRadius);
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

    // damage is out of 100. Dream seeds sometimes turn gold: they heal when absorbed.
    public EnemyBullet Orb(Vector3 origin, Vector3 direction, float speed, float damage, World world,
        Color color, float thorn, float scale, bool allowGold = true)
    {
        var b = EnemyBullet.SpawnBoss(origin, direction, speed, damage / 100f, world);
        if (b == null) return null;
        if (allowGold && world == World.Dream && damage > 0f && Random.value < Data.goldSeedChance)
            b.Look(BossAttack.Gold, 0f, scale * 1.15f).Reward(1f, Data.goldSeedHeal);
        else b.Look(color, thorn, scale);
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
        Tell("WAKE WINDOW  /  STRIKE NOW", seconds);
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
        if (Sealed)
        {
            Fx.Burst(hitPoint, new Color(0.65f, 0.85f, 1f), 5);
            return;
        }
        ApplyDamage(damage * Data.playerDamageScale * DamageMultiplier, hitPoint);
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
        float floor = Phase == 1 ? maxHealth * (2f / 3f) : Phase == 2 ? maxHealth / 3f : 1f;
        float previous = health;
        health = Mathf.Max(floor, health - damage);
        if (health < previous) Fx.Burst(hitPoint, Stunned ? new Color(0.6f, 1f, 1f) : new Color(1f, 0.6f, 0.2f), Stunned ? 12 : 6);
        if (Phase == 3 && health <= floor && !BindingReady)
        {
            director.StopAll();
            BindingReady = true;
            EnemyBullet.ClearBoss();
            GroundWave.ClearAll();
            BossHazards.ClearAll();
            director.Rest(0.65f);
            Tell("CORE BROKEN  /  Q TO DREAM, THEN E WITH FULL POWER", 5f);
        }
    }

    protected override void Die()
    {
        if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.End(true);
        base.Die();
    }

    // Editor and development builds only: 1/2/3 phase, N swap, T teleport, C cycle attacks, K kill, H hitboxes.
    // Any of them before the fight starts drops the player into the arena.
    void DebugKeys()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var kb = Keyboard.current;
        if (kb == null) return;
        bool any = kb.digit1Key.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame || kb.digit3Key.wasPressedThisFrame ||
            kb.nKey.wasPressedThisFrame || kb.tKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame ||
            kb.kKey.wasPressedThisFrame || kb.hKey.wasPressedThisFrame;
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
        if (kb.digit1Key.wasPressedThisFrame) DebugPhase(1);
        if (kb.digit2Key.wasPressedThisFrame) DebugPhase(2);
        if (kb.digit3Key.wasPressedThisFrame) DebugPhase(3);
        if (kb.nKey.wasPressedThisFrame) ForceWorld(CurrentWorld == World.Dream ? World.Nightmare : World.Dream);
        if (kb.tKey.wasPressedThisFrame) { beat = Beat.Fight; director.ForceTeleport(); }
        if (kb.cKey.wasPressedThisFrame) { beat = Beat.Fight; Tell("DEBUG  /  " + director.CycleNext(), 2f); }
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
