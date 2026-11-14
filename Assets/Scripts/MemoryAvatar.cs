using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// The player visual: the Dreamwalker. Holds the player's health (0..1) and drives the Lucid/Dreamwalker shader,
// which animates the body from these parameters (stride, lean, aim, ward, recoil, reload, hurt, dissolve).
public class MemoryAvatar : MonoBehaviour
{
    [FormerlySerializedAs("memory")] [Range(0f, 1f)] public float health = 1f;
    public Renderer[] ghosts;               // 0 = the body
    public Light glow;

    [Header("Animation")]
    public float strideRate = 2.6f;     // walk cycle radians per metre
    public float leanAmount = 0.3f;     // radians of lean at full speed
    public float turnSpeed = 16f;
    [Range(0f, 1f)] public float reload01;  // set by the weapon while reloading

    static readonly int MixId = Shader.PropertyToID("_Mix"), WalkId = Shader.PropertyToID("_Walk"), SpeedId = Shader.PropertyToID("_Speed01"),
        LeanId = Shader.PropertyToID("_Lean"), RollId = Shader.PropertyToID("_Roll"), AimId = Shader.PropertyToID("_Aim"),
        ShieldId = Shader.PropertyToID("_Shield"), KickId = Shader.PropertyToID("_Kick"), ReloadId = Shader.PropertyToID("_Reload"),
        HitId = Shader.PropertyToID("_Hit"), WhipId = Shader.PropertyToID("_Whip"), DissolveId = Shader.PropertyToID("_Dissolve"),
        HealthId = Shader.PropertyToID("_Health");

    MaterialPropertyBlock block;
    CharacterController cc;
    PlayerController player;
    PlayerCombat combat;
    float scatterUntil, scatterLength = 0.01f, sharpenUntil;
    float yaw, walkPhase, speed01, lean, roll, aim, shield, hit, whip, dissolve, flash;
    bool smoke;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        cc = GetComponentInParent<CharacterController>();
        player = GetComponentInParent<PlayerController>();
        combat = GetComponentInParent<PlayerCombat>();
        yaw = transform.eulerAngles.y;
        // Nothing dark may follow the player: no shadows.
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    void OnEnable() { WorldState.Changed += OnWorldChanged; }
    void OnDisable() { WorldState.Changed -= OnWorldChanged; }

    void OnWorldChanged(World w)
    {
        Scatter(0.3f);
        ThirdPersonCamera.Shake(0.15f);
    }

    // The figure frays for a moment (teleport, world swap, recall).
    public void Scatter(float seconds)
    {
        scatterLength = Mathf.Max(0.01f, seconds);
        scatterUntil = Time.time + scatterLength;
    }

    // Brief bright pulse of the core (for a perfect absorb).
    public void Sharpen() { sharpenUntil = Time.time + 0.35f; }

    // Smoke dash: the body dissolves completely while on, and reforms with a cloak whip and a light flash when off.
    public void SetSmoke(bool on)
    {
        if (smoke && !on) { whip = 1f; flash = 1f; }
        smoke = on;
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var kb = Keyboard.current;   // test only: lower and raise health
        if (kb != null)
        {
            if (kb.leftBracketKey.isPressed) health = Mathf.Max(0.05f, health - 0.4f * Time.deltaTime);
            if (kb.rightBracketKey.isPressed) health = Mathf.Min(1f, health + 0.4f * Time.deltaTime);
        }
#endif
        if (ghosts == null || ghosts.Length == 0 || ghosts[0] == null) return;
        float dt = Time.deltaTime;
        Vector3 v = cc != null ? cc.velocity : Vector3.zero;
        v.y = 0f;
        float speed = v.magnitude;
        bool facing = ThirdPersonCamera.LockedOn || ThirdPersonCamera.Aiming || (combat != null && (combat.Firing || combat.IsChargingBurst));
        float wantYaw = speed > 0.5f && !facing ? Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg : (player != null ? player.transform.eulerAngles.y : yaw);
        yaw = Mathf.LerpAngle(yaw, wantYaw, 1f - Mathf.Exp(-turnSpeed * dt));

        // Lean into the movement, in the body's own frame: forward and back pitch, strafes roll.
        float maxSp = player != null ? player.maxSpeed : 8f;
        Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * v / maxSp;
        bool aiming = combat != null && (combat.Firing || combat.Aiming);
        walkPhase += speed * strideRate * dt;
        speed01 = Mathf.MoveTowards(speed01, Mathf.Clamp01(speed / maxSp), 6f * dt);
        lean = Mathf.MoveTowards(lean, Mathf.Clamp(local.z, -1f, 1.5f) * leanAmount, 3f * dt);
        roll = Mathf.MoveTowards(roll, -Mathf.Clamp(local.x, -1.5f, 1.5f) * leanAmount, 3f * dt);
        aim = Mathf.MoveTowards(aim, aiming ? 1f : 0f, 8f * dt);
        shield = Mathf.MoveTowards(shield, combat != null && combat.ShieldUp ? 1f : 0f, 8f * dt);
        whip = Mathf.MoveTowards(whip, 0f, 3.5f * dt);
        flash = Mathf.MoveTowards(flash, 0f, 5f * dt);
        float sinceHit = player != null ? Time.time - player.LastHitTime : 99f;
        hit = Mathf.Clamp01(1f - sinceHit / 0.3f);

        float scatter = Time.time < scatterUntil ? Mathf.Sin((scatterUntil - Time.time) / scatterLength * Mathf.PI) : 0f;
        bool dead = health <= 0f;
        float wantDissolve = smoke || dead ? 1f : scatter * 0.55f;
        dissolve = Mathf.MoveTowards(dissolve, wantDissolve, (dead ? 1.2f : smoke ? 22f : 8f) * dt);
        float mix = WorldState.MixOrZero;
        float sharp = Time.time < sharpenUntil ? 0.5f : 0f;

        var body = ghosts[0];
        body.transform.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, yaw, 0f));
        body.GetPropertyBlock(block);
        block.SetFloat(MixId, mix);
        block.SetFloat(WalkId, walkPhase);
        block.SetFloat(SpeedId, speed01);
        block.SetFloat(LeanId, lean);
        block.SetFloat(RollId, roll);
        block.SetFloat(AimId, aim);
        block.SetFloat(ShieldId, shield);
        block.SetFloat(KickId, combat != null ? combat.Kick : 0f);
        block.SetFloat(ReloadId, reload01);
        block.SetFloat(HitId, hit);
        block.SetFloat(WhipId, whip);
        block.SetFloat(DissolveId, dissolve);
        block.SetFloat(HealthId, Mathf.Clamp01(health + sharp));
        body.SetPropertyBlock(block);

        if (glow != null)
        {
            glow.color = Color.Lerp(new Color(1f, 0.8f, 0.65f), new Color(1f, 0.3f, 0.12f), mix);
            glow.intensity = Mathf.Lerp(0.3f, 1.2f, health) * (1f - dissolve * 0.8f) * (1f + aim * 0.5f + shield * 0.5f) + flash * 6f;
        }
    }
}
