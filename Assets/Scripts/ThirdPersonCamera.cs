using UnityEngine;
using UnityEngine.InputSystem;

// One shoulder camera for free orbit and arena focus.
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 4.65f;
    public float shoulder = 0.55f;
    public float height = 1.6f;
    public float sensitivity = 0.12f;
    public float verticalSensitivity = 0.12f;
    public float minPitch = -12f;
    public float maxPitch = 55f;

    [Header("Feel")]
    [Min(0.01f)] public float followLag = 0.09f;
    [Range(40f, 90f)] public float baseFov = 65f;
    [Min(0f)] public float fovKick = 3f;
    [Min(0f)] public float impulseStrength = 0.24f;

    [Header("Collision")]
    public LayerMask obstructionMask = Physics.DefaultRaycastLayers;
    [Min(0.05f)] public float probeRadius = 0.24f;
    [Min(0.01f)] public float obstructionRecovery = 0.2f;
    [Min(0f)] public float arenaCameraMargin = 4.5f;

    // Over-the-shoulder aim: above and behind the shoulder. It blends on top of the free or the lock-on pose.
    [Header("Aim (over the shoulder)")]
    public float aimRight = 0.6f;
    public float aimUp = 1.7f;
    public float aimBack = 2.2f;
    public float aimFov = 57f;
    public float aimBlend = 0.2f;
    public float aimLookScale = 0.7f;       // mouse look is slower while aiming
    public float aimMinPitch = -40f;        // how far up the aim camera can look
    public float shoulderSwapTime = 0.15f;

    public static ThirdPersonCamera Instance;
    public static bool Aiming => Instance != null && Instance.aimWanted;
    public static void FovPunch(float degrees) { if (Instance != null) Instance.fovPunch = Mathf.Max(Instance.fovPunch, degrees); }
    public float AimWeight { get; private set; }
    bool aimWanted;
    float aimT, aimArm, shoulderSign = 1f, shoulderTarget = 1f, fovPunch;
    PlayerCombat combat;
    float trauma;
    public static void Shake(float amount) { if (Instance != null) Instance.trauma = Mathf.Min(1f, Instance.trauma + amount); }

    // Lock-on state.
    public static bool LockedOn => Instance != null && Instance.locked;
    public static void EngageLock() { if (Instance != null && !EclipseEncounter.Finished) Instance.locked = true; }
    public float LockWeight { get; private set; }
    public string LockDebug { get; private set; }
    public Vector3 LookPoint { get; private set; }

    float yaw;
    float pitch = 7f;
    Vector3 posVel;
    Vector3 followPoint;
    float dashFov;
    float obstructionLength, obstructionVelocity;
    bool obstructionInitialized;
    float viewPitch, viewPitchVelocity;
    float orbitYaw;
    EclipseBoss cachedBoss;
    CameraLockSettings lockCfg;
    Camera cam;
    PlayerController player;
    readonly RaycastHit[] obstructionHits = new RaycastHit[16];

    bool locked;
    float lockBlend;
    float lockYaw, lockYawVel;
    float lockDist, lockDistVel, lockFov, lockFovVel;
    Vector2 nudge;
    Vector3 anchor, anchorFrom, lastBossPos;
    float anchorEaseAt = -99f, phasePullAt = -99f;
    bool anchorReady, wasTransforming;
    EclipseVisual bossVisual;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        cam = GetComponent<Camera>();
        lockCfg = CameraLockSettings.Load();
        lockDist = lockCfg.distance;
        lockFov = lockCfg.minFov;
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            lockYaw = yaw;
            followPoint = target.position + Vector3.up * height;
            transform.SetPositionAndRotation(FreePosition(Quaternion.Euler(pitch, yaw, 0f)),
                Quaternion.Euler(pitch, yaw, 0f));
            if (cam != null) cam.fieldOfView = baseFov;
            player = target.GetComponent<PlayerController>();
            combat = target.GetComponent<PlayerCombat>();
        }
    }

    void LateUpdate()
    {
        if (target == null) return;
        bool finished = EclipseEncounter.Finished;
        var boss = EclipseEncounter.Instance != null ? EclipseEncounter.Instance.Boss : null;
        bool validBoss = boss != null && boss.isActiveAndEnabled && !finished;
        if (validBoss) DebugToggle();
        else locked = false;

        bool hadBlend = lockBlend > 0f;
        float dt = Time.unscaledDeltaTime;
        float blendTime = Mathf.Max(0.01f, locked ? lockCfg.blendIn : lockCfg.blendOut);
        lockBlend = Mathf.MoveTowards(lockBlend, locked ? 1f : 0f, dt / blendTime);
        LockWeight = Mathf.SmoothStep(0f, 1f, lockBlend);
        if (finished && !hadBlend && LockWeight <= 0f) return;

        if (locked && !hadBlend)
        {
            lockYaw = yaw;
            lockYawVel = 0f;
            orbitYaw = yaw;
            viewPitch = pitch;
            viewPitchVelocity = 0f;
            nudge = Vector2.zero;
        }
        if ((!locked && hadBlend) || !validBoss)
        {
            // On exit, start free orbit at the current viewing angle.
            if (hadBlend && !releasedToFree)
            {
                yaw = transform.eulerAngles.y;
                pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, transform.eulerAngles.x), minPitch, maxPitch);
                releasedToFree = true;
            }
        }
        else releasedToFree = false;

        Vector2 mouse = !finished ? LucidInput.LookDelta * Mathf.Lerp(1f, aimLookScale, AimWeight) : Vector2.zero;
        if (!finished && boss != null && LucidInput.Recenter.WasPressedThisFrame())
        {
            Vector3 direction = boss.transform.position - target.position;
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg; nudge = Vector2.zero;
        }
        if (!locked || aimT > 0f)
        {
            yaw += mouse.x * (sensitivity / 0.12f);
            pitch = Mathf.Clamp(pitch - mouse.y * (verticalSensitivity / 0.12f), aimT > 0f ? aimMinPitch : minPitch, maxPitch);
        }

        Vector3 chest = target.position + Vector3.up * height;
        if ((chest - followPoint).sqrMagnitude > 36f)
        {
            followPoint = chest;
            posVel = Vector3.zero;
            obstructionInitialized = false;
        }
        else followPoint = Vector3.SmoothDamp(followPoint, chest, ref posVel,
            Mathf.Max(0.01f, followLag), Mathf.Infinity, dt);

        bool dashing = player != null && player.IsDashing;
        dashFov = Mathf.Lerp(dashFov, dashing ? fovKick : 0f, 1f - Mathf.Exp(-7f * dt));
        Quaternion freeRot = Quaternion.Euler(pitch, yaw, 0f);
        float fov = baseFov + dashFov;
        Vector3 desired = FreePosition(freeRot);
        Quaternion rotation = freeRot;
        if (LockWeight > 0f)
        {
            if (validBoss && locked)
            {
                lockOffset = LockPose(boss, mouse, dt, out lastLockRotation, out lastLockFov) - followPoint;
            }
            desired = Vector3.Lerp(desired, followPoint + lockOffset, LockWeight);
            rotation = Quaternion.Slerp(freeRot, lastLockRotation, LockWeight);
            fov = Mathf.Lerp(fov, lastLockFov + dashFov * 0.5f, LockWeight);
        }

        float now = Time.unscaledTime * 18f;
        float shake = impulseStrength * trauma * trauma * Mathf.Lerp(1f, lockCfg.shakeScale, LockWeight);
        Vector3 impulse = new Vector3(Mathf.PerlinNoise(now, 1f) - 0.5f,
            (Mathf.PerlinNoise(now, 7f) - 0.5f) * lockCfg.shakeVertical, 0f) * (2f * shake);
        transform.SetPositionAndRotation(ResolvePosition(chest, desired + rotation * impulse, boss, dt), rotation);
        if (cam != null) cam.fieldOfView = fov;
        AimPose(boss, finished);
        if (cam != null && fovPunch > 0f)
        {
            if (impulseStrength > 0f) cam.fieldOfView += fovPunch;
            fovPunch = Mathf.MoveTowards(fovPunch, 0f, 45f * dt);
        }
        trauma = Mathf.Max(0f, trauma - 2.8f * dt);
    }

    // Blends the current pose toward the shoulder camera while the player aims.
    // The mouse aims freely, in the boss fight too; the lock-on framing returns when the player stops aiming.
    void AimPose(EclipseBoss boss, bool finished)
    {
        float udt = Time.unscaledDeltaTime;
        aimWanted = combat != null && combat.Aiming && !finished;
        if (LucidInput.SwapShoulder.WasPressedThisFrame()) shoulderTarget = -shoulderTarget;
        shoulderSign = Mathf.MoveTowards(shoulderSign, shoulderTarget, 2f * udt / Mathf.Max(0.01f, shoulderSwapTime));
        bool starting = aimT <= 0f;
        aimT = Mathf.MoveTowards(aimT, aimWanted ? 1f : 0f, udt / Mathf.Max(0.01f, aimBlend));
        AimWeight = Mathf.SmoothStep(0f, 1f, aimT);
        if (AimWeight <= 0f) return;

        // Free aim, also in the boss fight: the view starts where the camera was looking and the mouse takes over.
        if (starting)
        {
            yaw = transform.eulerAngles.y;
            float x = transform.eulerAngles.x;
            pitch = Mathf.Clamp(x > 180f ? x - 360f : x, aimMinPitch, maxPitch);
        }
        float armYaw = yaw;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 origin = target.position + Vector3.up * aimUp;
        Vector3 desired = origin + Quaternion.Euler(0f, armYaw, 0f) * Vector3.right * (aimRight * shoulderSign) - rot * Vector3.forward * aimBack;
        Vector3 dir = desired - origin;
        float length = dir.magnitude;
        dir /= Mathf.Max(0.01f, length);
        // Deoccluder: the arm shortens at once when something is in the way and eases back out.
        float free = length;
        int hits = Physics.SphereCastNonAlloc(origin, 0.25f, dir, obstructionHits, length,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
            if (!obstructionHits[i].transform.IsChildOf(target))
                free = Mathf.Min(free, Mathf.Max(0.3f, obstructionHits[i].distance - 0.05f));
        aimArm = starting || free < aimArm ? free : Mathf.MoveTowards(aimArm, free, 8f * udt);
        Vector3 pos = origin + dir * aimArm;

        transform.position = Vector3.Lerp(transform.position, pos, AimWeight);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, AimWeight);
        if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, aimFov, AimWeight);
    }

    bool releasedToFree;
    Vector3 lockOffset;
    Quaternion lastLockRotation;
    float lastLockFov;

    Vector3 FreePosition(Quaternion rotation)
    {
        return followPoint + rotation * (Vector3.right * shoulder - Vector3.forward * distance);
    }

    // Lock-on: the arm sits on the boss-player line behind the player and looks at a point weighted toward the boss.
    // Distance and FOV open with the gap and during telegraphs, and open further if either subject nears the frame edge.
    Vector3 LockPose(EclipseBoss boss, Vector2 mouse, float dt, out Quaternion rotation, out float fov)
    {
        var c = lockCfg;
        Vector3 p = target.position;
        Vector3 b = BossAnchor(boss);
        Vector3 toBoss = b - p; toBoss.y = 0f;
        float gap = toBoss.magnitude;
        if (gap > 0.1f)
            lockYaw = Mathf.SmoothDampAngle(lockYaw, Mathf.Atan2(toBoss.x, toBoss.z) * Mathf.Rad2Deg, ref lockYawVel, c.yawSmooth, c.maxTurnSpeed, dt);

        float wide = 1f - Mathf.InverseLerp(c.nearGap, c.farGap, gap);
        nudge += new Vector2(mouse.x * (sensitivity / 0.12f), mouse.y * (verticalSensitivity / 0.12f)) * c.nudgeSensitivity;
        nudge = Vector2.ClampMagnitude(nudge, c.nudgeMax);
        nudge *= Mathf.Exp(-c.nudgeReturn * dt);
        orbitYaw = Mathf.LerpAngle(orbitYaw, lockYaw + nudge.x, 1f - Mathf.Exp(-18f * dt));
        float wantDist = Mathf.Lerp(c.distance, c.closeDistance, wide);
        float wantFov = Mathf.Lerp(c.minFov, c.maxFov, wide);

        Quaternion arm = Quaternion.Euler(c.pitch, orbitYaw, 0f);
        Vector3 chest = followPoint + Vector3.up * (c.height - height);
        Vector3 pivot = chest + Quaternion.Euler(0f, orbitYaw, 0f) * Vector3.right * c.shoulder;
        float pull = PhasePull(boss);
        Vector3 pos = pivot - arm * Vector3.forward * (lockDist + pull * c.phasePullDistance);
        // Frame the boss by its core (the lock-on point), carried along with the eased anchor.
        if (bossVisual == null) bossVisual = boss.GetComponent<EclipseVisual>();
        Vector3 core = b + (bossVisual != null && bossVisual.Core != null ? bossVisual.Core.position - boss.transform.position : Vector3.zero);
        Vector3 look = Vector3.Lerp(chest, core, c.bossWeight);

        // Yaw aims at the weighted look point. Pitch and FOV are solved so the player sits at playerScreenY
        // and the boss at bossScreenY; if that needs more than maxFov, the arm pulls back instead.
        float aP = Elevation(pos, p + Vector3.up * 0.9f), aB = Elevation(pos, core);
        float fitFov = (aB - aP) / Mathf.Max(0.05f, c.bossScreenY - c.playerScreenY);
        wantFov = Mathf.Clamp(Mathf.Max(fitFov, wantFov), c.minFov, c.maxFov);
        lockFov = Mathf.SmoothDamp(lockFov, wantFov, ref lockFovVel, c.adaptSmooth, Mathf.Infinity, dt);
        // Exact pitch: put the player at playerScreenY, tilt up if the core would pass bossMaxScreenY,
        // but never push the player below minPlayerScreenY; past that the arm pulls back.
        float tanHalf = Mathf.Tan(lockFov * 0.5f * Mathf.Deg2Rad);
        float pitchForPlayer = aP - Mathf.Atan((c.playerScreenY * 2f - 1f) * tanHalf) * Mathf.Rad2Deg;
        float pitchForCore = aB - Mathf.Atan((c.bossMaxScreenY * 2f - 1f) * tanHalf) * Mathf.Rad2Deg;
        float pitchLimit = aP - Mathf.Atan((c.minPlayerScreenY * 2f - 1f) * tanHalf) * Mathf.Rad2Deg;
        float viewPitch = Mathf.Min(Mathf.Max(pitchForPlayer, pitchForCore), pitchLimit);
        if (pitchForCore > pitchLimit) wantDist = Mathf.Min(c.closeDistance + 0.5f, wantDist + (pitchForCore - pitchLimit) * 0.25f);
        lockDist = Mathf.SmoothDamp(lockDist, wantDist, ref lockDistVel, c.adaptSmooth, Mathf.Infinity, dt);
        Vector3 flatLook = look - pos; flatLook.y = 0f;
        float viewYaw = flatLook.sqrMagnitude > 0.01f ? Mathf.Atan2(flatLook.x, flatLook.z) * Mathf.Rad2Deg : lockYaw;
        float targetPitch = Mathf.Clamp(-viewPitch - nudge.y, minPitch, maxPitch);
        this.viewPitch = Mathf.SmoothDampAngle(this.viewPitch, targetPitch, ref viewPitchVelocity,
            c.framingSmooth, Mathf.Infinity, dt);
        rotation = Quaternion.Euler(this.viewPitch, Mathf.LerpAngle(viewYaw, orbitYaw, 0.25f), 0f);

        fov = lockFov + pull * c.phasePullFov;
        LookPoint = look;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Time.unscaledTime >= nextDebugUpdate)
        {
            nextDebugUpdate = Time.unscaledTime + 0.2f;
            LockDebug = $"LOCK  dist {lockDist:F1}  fov {lockFov:F0}  gap {gap:F1}";
        }
#endif
        return pos;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    float nextDebugUpdate;
#endif

    // Sleepwalk: when the boss jumps, hold the old framing briefly, then ease onto the new position.
    Vector3 BossAnchor(EclipseBoss boss)
    {
        var c = lockCfg;
        Vector3 now = boss.transform.position;
        if (cachedBoss != boss)
        {
            cachedBoss = boss;
            bossVisual = null;
            anchorReady = false;
            wasTransforming = false;
            phasePullAt = -99f;
        }
        if (!anchorReady) { anchor = lastBossPos = now; anchorReady = true; }
        if ((now - lastBossPos).sqrMagnitude > 9f) { anchorFrom = anchor; anchorEaseAt = Time.time + c.teleportHold; }
        lastBossPos = now;
        float k = (Time.time - anchorEaseAt) / Mathf.Max(0.01f, c.teleportEase);
        anchor = k < 0f ? anchorFrom : Vector3.Lerp(anchorFrom, now, Mathf.SmoothStep(0f, 1f, k));
        return anchor;
    }

    // Phase transition: a smooth 0..1..0 pull back over phasePullBack seconds.
    float PhasePull(EclipseBoss boss)
    {
        if (boss.InTransformation && !wasTransforming) phasePullAt = Time.time;
        wasTransforming = boss.InTransformation;
        float k = (Time.time - phasePullAt) / Mathf.Max(0.01f, lockCfg.phasePullBack);
        return k >= 0f && k <= 1f ? Mathf.Sin(k * Mathf.PI) : 0f;
    }

    Vector3 ResolvePosition(Vector3 chest, Vector3 desired, EclipseBoss boss, float dt)
    {
        // Confine the camera to the outer combat footprint, with enough room behind the rim
        // to keep the character visible. The player boundary remains owned by movement.
        var encounter = EclipseEncounter.Instance;
        if (encounter != null && encounter.BossFightStarted)
        {
            Vector3 radial = desired - EclipseEncounter.ArenaCenter;
            radial.y = 0f;
            Vector3 limited = Vector3.ClampMagnitude(radial, EclipseEncounter.ArenaRadius + arenaCameraMargin);
            desired += limited - radial;
            desired.y = Mathf.Max(desired.y, EclipseEncounter.ArenaCenter.y + probeRadius);
        }

        Vector3 arm = desired - chest;
        float length = arm.magnitude;
        if (length < 0.001f) return chest;
        Vector3 direction = arm / length;
        float radius = Mathf.Lerp(probeRadius, lockCfg.probeRadius, LockWeight);
        float safe = length;
        int hits = Physics.SphereCastNonAlloc(chest, radius, direction, obstructionHits, length,
            obstructionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            var hit = obstructionHits[i];
            if (hit.transform.IsChildOf(target)) continue;
            safe = Mathf.Min(safe, Mathf.Max(0.05f, hit.distance - 0.06f));
        }

        if (boss != null && boss.Engaged)
        {
            // The procedural boss is colliderless. Keep the camera out of its central body.
            Vector3 offset = chest - boss.transform.position;
            float clearance = boss.radius * (boss.transform.localScale.y / 1.25f) + lockCfg.bossClearance;
            float projection = Vector3.Dot(offset, direction);
            float discriminant = projection * projection - offset.sqrMagnitude + clearance * clearance;
            if (discriminant >= 0f && projection < 0f)
            {
                float entry = -projection - Mathf.Sqrt(discriminant);
                if (entry >= 0f) safe = Mathf.Min(safe, Mathf.Max(0.05f, entry - radius));
            }
        }

        // Smooth only the lost distance. Intentional framing changes are already damped.
        float blocked = length - safe;
        if (!obstructionInitialized || blocked >= obstructionLength)
        {
            obstructionLength = blocked;
            obstructionVelocity = 0f;
            obstructionInitialized = true;
        }
        else obstructionLength = Mathf.SmoothDamp(obstructionLength, blocked, ref obstructionVelocity,
            Mathf.Max(0.01f, obstructionRecovery), Mathf.Infinity, dt);
        return chest + direction * Mathf.Clamp(length - obstructionLength, 0.05f, length);
    }

    // Degrees above the horizontal from one point to another.
    static float Elevation(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        return Mathf.Atan2(d.y, Mathf.Max(0.01f, new Vector2(d.x, d.z).magnitude)) * Mathf.Rad2Deg;
    }

    void OnDrawGizmos()
    {
        if (LockWeight <= 0f) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(LookPoint, 0.4f);
        if (target != null) Gizmos.DrawLine(target.position + Vector3.up * lockCfg.height, LookPoint);
    }

    // Editor and development builds: L toggles the lock for testing.
    void DebugToggle()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame && !EclipseEncounter.Finished) locked = !locked;
#endif
    }
}
