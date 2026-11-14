using UnityEngine;
using UnityEngine.InputSystem;

// One timeline owns the encounter. A player's shift never restarts that timeline.
public class EclipseBoss : Enemy
{
    public float maxHealth = 300f;
    public float activationDistance = 25.5f;
    [Range(0.5f, 1f)] public float bindingPowerFraction = 1f;
    public bool Sealed => CurrentWorld == World.Dream || beat == Beat.PhaseTell || BindingReady;
    public bool BindingReady { get; private set; }
    public bool InTransformation => beat == Beat.PhaseTell;

    public bool Engaged { get; private set; }
    public bool Charging => beat == Beat.Tell || beat == Beat.PhaseTell;
    public int Phase { get; private set; } = 1;
    public float ChargeAmount => Charging ? 1f - Mathf.Clamp01(remaining / tellDuration) : 0f;
    public string AttackName { get; private set; } = "THE SLEEPER";

    enum Beat { Dormant, Intro, Tell, Emit, Recover, PhaseTell, Dead }
    enum Pattern { BrokenHalo, TwinHelix, CrimsonTide, ThornFan, FusedBloom, TendrilSweep, PetalWheel }
    Beat beat;
    Pattern pattern;
    World formationWorld;
    float remaining, tellDuration, emitTimer, lockedYaw;
    int sequence, emission, emissionCount, formationPhase;
    PlayerCombat playerCombat;
    float nextWeave;

    protected override void Start()
    {
        base.Start();
        health = maxHealth;
        radius = 3.4f;
        deathColor = new Color(1f, 0.28f, 0.12f);
        if (GetComponent<EclipseVisual>() == null) gameObject.AddComponent<EclipseVisual>();
        if (player != null) playerCombat = player.GetComponent<PlayerCombat>();
    }

    protected override void OnDisable()
    {
        beat = Beat.Dead;
        Engaged = false;
        EnemyBullet.ClearBoss();
        GroundWave.ClearAll();
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
        else if (Time.time >= nextWeave &&
            (player.position - DreamAnchorPosition).sqrMagnitude < 16f)
        {
            nextWeave = Time.time + 7f;
            playerCombat.AddPower(2f);
            CombatPulse.Spawn(DreamAnchorPosition, 4f, Color.cyan);
            Tell("ANCHOR WOVEN  +2 POWER", 1.4f);
        }
    }

    public static Vector3 DreamAnchorPosition => EclipseEncounter.ArenaCenter + Vector3.left * 13f + Vector3.up;

    void Update()
    {
        if (player == null || EclipseEncounter.Finished || beat == Beat.Dead) return;
        if (!Engaged)
        {
            Vector3 fromCenter = player.position - EclipseEncounter.ArenaCenter;
            fromCenter.y = 0f;
            if (fromCenter.sqrMagnitude > activationDistance * activationDistance) return;
            Engaged = true;
            if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.BeginBossFight();
            Tell("THE SLEEPER  /  HOLD RIGHT CLICK TO CAPTURE DREAM SEEDS", 3f);
            beat = Beat.Intro;
            remaining = 2.5f;
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

        switch (beat)
        {
            case Beat.Intro:
            case Beat.Recover:
                if (remaining <= 0f) NextBeat();
                break;
            case Beat.Tell:
                if (remaining <= 0f)
                {
                    beat = Beat.Emit;
                    emission = 0;
                    emitTimer = 0f;
                }
                break;
            case Beat.Emit:
                emitTimer -= dt;
                // Never dump a backlog of overlapping rows after a long frame.
                if (emitTimer <= 0f)
                {
                    Emit();
                    emission++;
                    emitTimer = EmissionInterval;
                    if (emission >= emissionCount)
                    {
                        beat = Beat.Recover;
                        remaining = pattern == Pattern.CrimsonTide ? 2.6f : Phase == 1 ? 1.8f : 1.25f;
                    }
                }
                break;
            case Beat.PhaseTell:
                if (remaining <= 0f) StartPattern();
                break;
        }
    }

    void NextBeat()
    {
        if ((Phase == 1 && health <= maxHealth * (2f / 3f)) ||
            (Phase == 2 && health <= maxHealth / 3f))
        {
            Phase++;
            LucidAudio.Play(LucidAudio.Cue.Fusion);
            EnemyBullet.ClearBoss();
            GroundWave.ClearAll();
            AttackName = Phase == 2 ? "AWAKENED NIGHTMARE" : "THE BOUNDARY IS BREAKING";
            Tell(AttackName, 2.6f);
            beat = Beat.PhaseTell;
            remaining = tellDuration = 2.6f;
            sequence = 0;
            CombatPulse.Spawn(transform.position, Phase == 3 ? 18f : 10f,
                Phase == 3 ? new Color(0.8f, 0.25f, 1f) : new Color(1f, 0.42f, 0.08f));
            ThirdPersonCamera.Shake(0.28f);
            return;
        }
        StartPattern();
    }

    void StartPattern()
    {
        formationWorld = BindingReady ? World.Dream : CurrentWorld;
        int turn = sequence++;
        if (BindingReady) pattern = Pattern.BrokenHalo;
        else if (Phase == 3 && turn % 3 == 2) pattern = Pattern.FusedBloom;
        else if (Phase >= 2 && turn % 5 == 3) pattern = Pattern.PetalWheel;
        else if (formationWorld == World.Dream)
            pattern = Phase > 1 && turn % 3 == 2 ? Pattern.TwinHelix : turn % 2 == 0 ? Pattern.BrokenHalo : Pattern.CrimsonTide;
        else pattern = turn % 2 == 0 ? Pattern.ThornFan : Pattern.TendrilSweep;
        formationPhase = Phase;
        Vector3 aim = player.position - Emitter;
        lockedYaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
        switch (pattern)
        {
            case Pattern.BrokenHalo:
                AttackName = BindingReady ? "DREAM SEEDS  /  HOLD RIGHT CLICK" : "DREAM SEED HALO";
                tellDuration = 1.25f;
                emissionCount = formationPhase == 1 ? 4 : 6;
                Tell(BindingReady ? "CAPTURE CYAN SEEDS. E AT FULL POWER TO BIND." : "DREAM SEED HALO  /  FOLLOW THE OPENING", tellDuration);
                break;
            case Pattern.TwinHelix:
                AttackName = "TWIN HELIX";
                tellDuration = 1.3f;
                emissionCount = formationPhase == 1 ? 30 : 40;
                Tell("TWIN HELIX  /  WEAVE BETWEEN THE CHAINS", tellDuration);
                break;
            case Pattern.ThornFan:
                AttackName = "NIGHT TERROR FAN";
                tellDuration = 1.15f;
                emissionCount = Phase == 1 ? 2 : 3;
                Tell("NIGHT TERROR FAN  /  MOVE THROUGH THE GAPS", tellDuration);
                break;
            case Pattern.FusedBloom:
                AttackName = "BROKEN BOUNDARY";
                tellDuration = 1.55f;
                emissionCount = 4;
                Tell("BROKEN BOUNDARY  /  CAPTURE CYAN, DODGE EMBER", tellDuration);
                break;
            case Pattern.PetalWheel:
                AttackName = "PETAL WHEEL";
                tellDuration = 1.55f;
                emissionCount = 3;
                Tell("PETAL WHEEL  /  MOVE BETWEEN THE SPOKES", tellDuration);
                break;
            case Pattern.TendrilSweep:
                AttackName = "WHIP TENDRILS";
                tellDuration = 1.45f;
                emissionCount = Phase == 1 ? 1 : 2;
                CombatPulse.Spawn(Emitter + Quaternion.Euler(0f, lockedYaw, 0f) * Vector3.forward * 8f,
                    5f, new Color(1f, 0.18f, 0.25f));
                Tell("WHIP TENDRILS  /  DASH OR JUMP THE RED ARC", tellDuration);
                break;
            default:
                AttackName = "CRIMSON TIDE";
                tellDuration = 1.6f;
                emissionCount = formationPhase == 1 ? 2 : 3;
                Tell("CRIMSON TIDE  /  DASH THROUGH THE RED RING", tellDuration);
                break;
        }
        remaining = tellDuration;
        beat = Beat.Tell;
        LucidAudio.Play(LucidAudio.Cue.Warning);
    }

    float EmissionInterval => pattern == Pattern.BrokenHalo ? 0.52f :
        pattern == Pattern.TwinHelix ? 0.11f : (pattern == Pattern.CrimsonTide || pattern == Pattern.TendrilSweep) ? 1.1f : 0.68f;
    World CurrentWorld => WorldState.Instance != null ? WorldState.Instance.Current : World.Dream;
    Vector3 Emitter => new Vector3(transform.position.x, 1.1f, transform.position.z);

    void Emit()
    {
        Vector3 origin = Emitter;
        float speed = formationWorld == World.Dream ? 8.5f : 10.5f;
        if (pattern == Pattern.BrokenHalo)
        {
            for (int bead = 0; bead < 29; bead++)
                if (!IsHaloGap(bead, emission)) SpawnOrb(origin, lockedYaw + HaloAngle(bead), speed);
        }
        else if (pattern == Pattern.TwinHelix)
        {
            Vector3 side = Quaternion.Euler(0f, lockedYaw, 0f) * Vector3.right * 3.5f;
            SpawnOrb(origin - side, lockedYaw + HelixAngle(emission, false), speed);
            SpawnOrb(origin + side, lockedYaw + HelixAngle(emission, true), speed);
        }
        else if (pattern == Pattern.ThornFan || pattern == Pattern.FusedBloom)
        {
            for (int bead = -6; bead <= 6; bead++)
            {
                if ((bead + 6 + emission) % 5 < 2) continue;
                World attunement = pattern == Pattern.FusedBloom && bead % 2 == 0 ? World.Dream : World.Nightmare;
                SpawnOrb(origin, lockedYaw + bead * 8f + emission * 2f,
                    attunement == World.Dream ? 9f : 15f, attunement);
            }
        }
        else if (pattern == Pattern.PetalWheel)
        {
            for (int spoke = 0; spoke < 8; spoke++)
            {
                float angle = lockedYaw + 22.5f + spoke * 45f + emission * 12f;
                SpawnOrb(origin, angle - 3f, speed);
                SpawnOrb(origin, angle + 3f, speed);
            }
        }
        else if (pattern == Pattern.TendrilSweep)
        {
            GroundWave.Spawn(origin, player, 16f, lockedYaw + (emission == 0 ? 0f : 22f), 34f);
        }
        else
        {
            // The warning snapshots aim; these volleys do not track a dodging player.
            // Red waves persist through both worlds and are avoided with dash immunity.
            GroundWave.Spawn(origin, player, formationPhase == 1 ? 12f : 14f);
            for (int bead = 0; bead < 7; bead++)
                SpawnOrb(origin, lockedYaw + (bead - 3) * 11f, speed * 0.9f);
        }
    }

    void SpawnOrb(Vector3 origin, float yaw, float speed, World? world = null)
    {
        if (EnemyBullet.BossCount >= 220) return;
        EnemyBullet.SpawnBoss(origin, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward,
            speed, 0.075f, world ?? formationWorld);
    }

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
        // Each threshold is a gate, so a single heavy shot cannot skip a transformation.
        float floor = Phase == 1 ? maxHealth * (2f / 3f) : Phase == 2 ? maxHealth / 3f : 1f;
        float previous = health;
        health = Mathf.Max(floor, health - damage);
        if (health < previous) Fx.Burst(hitPoint, new Color(1f, 0.6f, 0.2f), 6);
        if (Phase == 3 && health <= floor)
        {
            BindingReady = true;
            EnemyBullet.ClearBoss();
            GroundWave.ClearAll();
            beat = Beat.Recover;
            remaining = 0.65f;
            Tell("CORE BROKEN  /  Q TO DREAM, THEN E WITH FULL POWER", 5f);
        }
    }

    protected override void Die()
    {
        if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.End(true);
        base.Die();
    }
}
