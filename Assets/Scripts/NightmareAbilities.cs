using UnityEngine;

public class NightmareAbilities : MonoBehaviour
{
    BalanceSheet sheet;
    PlayerCombat combat;
    PlayerController pc;
    float burstUntil, stepUntil, driveUntil;
    int drivenPhase = -1;
    public float BurstReady01 => 1f - Mathf.Clamp01((burstUntil - Time.time) / sheet.burst.cooldown);
    public float StepReady01 => 1f - Mathf.Clamp01((stepUntil - Time.time) / sheet.shadowstep.cooldown);
    public float DriveReady01 => 1f - Mathf.Clamp01((driveUntil - Time.time) / sheet.overdrive.cooldown);
    public bool DriveAvailable => combat.Dread >= .999f && Time.time >= driveUntil && Boss != null && Boss.Phase != drivenPhase;
    EclipseBoss Boss => EclipseEncounter.Instance != null ? EclipseEncounter.Instance.Boss : null;
    void Awake() { sheet = BalanceSheet.Load(); combat = GetComponent<PlayerCombat>(); pc = GetComponent<PlayerController>(); }
    void Update()
    {
        if (!combat.enabled || DreamcatcherPresentation.CoversHud || EclipseEncounter.Finished || pc.Recalling || WorldState.CurrentOrDream != World.Nightmare) return;
        if (LucidInput.Pressed(LucidInput.Ability1) && Time.time >= burstUntil && combat.SpendPower(sheet.burst.powerCost))
        {
            LucidInput.Consume(LucidInput.Ability1); burstUntil = Time.time + sheet.burst.cooldown;
            EnemyBullet.ClearDanger(transform.position, sheet.burst.radius);
            if (Boss != null && Boss.Engaged) Boss.Stagger(sheet.burst.amount);
            CombatPulse.Spawn(transform.position + Vector3.up, sheet.burst.radius, new Color(.7f,.85f,1f));
            LucidAudio.Play(LucidAudio.Cue.Burst); ThirdPersonCamera.Shake(.16f);
        }
        if (LucidInput.Pressed(LucidInput.Ability2) && Time.time >= stepUntil && combat.Power >= sheet.shadowstep.powerCost)
        {
            Enemy target = null; float best = sheet.shadowstep.radius * sheet.shadowstep.radius;
            for (int i = 0; i < Enemy.All.Count; i++)
            {
                var e = Enemy.All[i]; if (e == null || e.health <= 0) continue;
                float d = (e.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; target = e; }
            }
            if (target != null)
            {
                Vector3 destination = target.transform.position - target.transform.forward * (target.radius + 2f); destination.y = transform.position.y;
                if (target is EclipseBoss) { destination = ((EclipseBoss)target).ClampToArena(destination, 1f); destination.y = transform.position.y; }
                Vector3 delta = destination - transform.position;
                // Do not blink through arena walls or level geometry.
                if (!Physics.SphereCast(transform.position + Vector3.up, .35f, delta.normalized, out var hit, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    LucidInput.Consume(LucidInput.Ability2); combat.SpendPower(sheet.shadowstep.powerCost); stepUntil = Time.time + sheet.shadowstep.cooldown;
                    Fx.Burst(transform.position + Vector3.up, new Color(.7f,.8f,1f), 24);
                    pc.Teleport(destination, transform.eulerAngles.y); combat.Weapons.empowered = sheet.shadowstep.amount;
                    CombatPulse.Spawn(destination + Vector3.up, 3f, Color.white); LucidAudio.Play(LucidAudio.Cue.DashNightmare);
                }
            }
        }
        if (LucidInput.Pressed(LucidInput.Overdrive) && DriveAvailable)
        {
            LucidInput.Consume(LucidInput.Overdrive); drivenPhase = Boss.Phase; driveUntil = Time.time + sheet.overdrive.cooldown;
            combat.Weapons.BeginOverdrive(sheet.overdriveSeconds);
            combat.SetDread(0f); LucidAudio.Play(LucidAudio.Cue.Fusion);
            EclipseEncounter.Instance.Tell("OVERDRIVE / FIVE SECONDS", sheet.overdriveSeconds);
        }
    }
}
