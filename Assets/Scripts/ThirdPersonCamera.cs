using UnityEngine;
using UnityEngine.InputSystem;

// Smooth orbit camera behind the player. Mouse looks around. FOV and distance open up with speed.
// During the boss fight a Sekiro-style lock-on takes over (see CameraLockSettings): it engages from
// BossArenaTrigger, orbits the player on the boss-player line and releases when the fight ends.
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 9f;
    public float height = 1.6f;
    public float minPitch = -10f;
    public float maxPitch = 55f;

    [Header("Feel")]
    public float followLag = 0.12f;     // seconds of lag. Higher = floatier
    public float baseFov = 60f;
    public float fovKick = 14f;         // extra FOV at full speed
    public float distanceKick = 1.5f;

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
    float aimT, aimArm, shoulder = 1f, shoulderTarget = 1f, fovPunch;
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
    Vector3 freePos;
    float freeFov;
    Camera cam;
    PlayerController player;
    readonly RaycastHit[] obstructionHits = new RaycastHit[16];

    CameraLockSettings lockCfg;
    bool locked;
    float lockBlend;
    float lockYaw, lockYawVel;
    float lockDist, lockDistVel, lockFov, lockFovVel;
    Vector2 nudge;
    float lockArm = 4.5f, lockArmVel;
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
        freePos = transform.position;
        freeFov = cam != null ? cam.fieldOfView : baseFov;
        lockDist = lockCfg.distance;
        lockFov = lockCfg.minFov;
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            lockYaw = yaw;
            player = target.GetComponent<PlayerController>();
            combat = target.GetComponent<PlayerCombat>();
        }
    }

    void LateUpdate()
    {
        if (target == null) return;
        bool finished = EclipseEncounter.Finished;
        var boss = EclipseEncounter.Instance != null ? EclipseEncounter.Instance.Boss : null;
        DebugToggle();
        if (finished || boss == null) locked = false;
        float blendTime = Mathf.Max(0.01f, locked ? lockCfg.blendIn : lockCfg.blendOut);
        lockBlend = Mathf.MoveTowards(lockBlend, locked ? 1f : 0f, Time.unscaledDeltaTime / blendTime);
        LockWeight = Mathf.SmoothStep(0f, 1f, lockBlend);
        if (finished && LockWeight <= 0f) return;

        // While locked, the free camera follows the lock's yaw so the blend back starts behind the player.
        if (LockWeight > 0f && aimT <= 0f) yaw = lockYaw;
        if (LockWeight <= 0f || aimT > 0f)
        {
            Vector2 d = LucidInput.LookDelta * Mathf.Lerp(1f, aimLookScale, AimWeight);
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, aimT > 0f ? aimMinPitch : minPitch, maxPitch);
        }
        Quaternion freeRot = FreePose();

        if (LockWeight > 0f && boss != null)
        {
            Quaternion lockRot;
            float lockFov;
            Vector3 lockPos = LockPose(boss, out lockRot, out lockFov);
            transform.position = Vector3.Lerp(freePos, lockPos, LockWeight);
            transform.rotation = Quaternion.Slerp(freeRot, lockRot, LockWeight);
            if (cam != null) cam.fieldOfView = Mathf.Lerp(freeFov, lockFov, LockWeight);
        }
        else
        {
            transform.position = freePos;
            transform.rotation = freeRot;
            if (cam != null) cam.fieldOfView = freeFov;
        }

        AimPose(boss, finished);
        if (cam != null && fovPunch > 0f)
        {
            cam.fieldOfView += fovPunch;
            fovPunch = Mathf.MoveTowards(fovPunch, 0f, 45f * Time.unscaledDeltaTime);
        }

        if (trauma > 0f)
        {
            // While locked the shake is smaller and mostly sideways, so the bullet plane stays put on screen.
            Vector3 s = Random.insideUnitSphere * trauma * trauma * 0.6f * Mathf.Lerp(1f, lockCfg.shakeScale, LockWeight);
            s.y *= Mathf.Lerp(1f, lockCfg.shakeVertical, LockWeight);
            transform.position += transform.rotation * s;
            trauma = Mathf.Max(0f, trauma - 2.2f * Time.unscaledDeltaTime);
        }
    }

    // Blends the current pose toward the shoulder camera while the player aims.
    // The mouse aims freely, in the boss fight too; the lock-on framing returns when the player stops aiming.
    void AimPose(EclipseBoss boss, bool finished)
    {
        float udt = Time.unscaledDeltaTime;
        aimWanted = combat != null && combat.Aiming && !finished;
        if (LucidInput.SwapShoulder.WasPressedThisFrame()) shoulderTarget = -shoulderTarget;
        shoulder = Mathf.MoveTowards(shoulder, shoulderTarget, 2f * udt / Mathf.Max(0.01f, shoulderSwapTime));
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
        Vector3 desired = origin + Quaternion.Euler(0f, armYaw, 0f) * Vector3.right * (aimRight * shoulder) - rot * Vector3.forward * aimBack;
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

    // The original free camera. Writes freePos and freeFov, returns its rotation.
    Quaternion FreePose()
    {
        float speed01 = player != null ? Mathf.Clamp01(player.Velocity.magnitude / 22f) : 0f;
        freeFov = Mathf.Lerp(freeFov, baseFov + speed01 * fovKick, 5f * Time.deltaTime);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focus = target.position + Vector3.up * height;
        Vector3 back = -(rot * Vector3.forward);
        float arm = distance + speed01 * distanceKick;
        int hits = Physics.SphereCastNonAlloc(focus, 0.28f, back, obstructionHits, arm,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            var hit = obstructionHits[i];
            if (!hit.transform.IsChildOf(target))
                arm = Mathf.Min(arm, Mathf.Max(0.5f, hit.distance - 0.12f));
        }
        Vector3 desired = focus + back * arm;
        if (arm + 0.01f < distance + speed01 * distanceKick && (freePos - focus).sqrMagnitude > arm * arm)
        {
            freePos = desired;
            posVel = Vector3.zero;
        }
        else freePos = Vector3.SmoothDamp(freePos, desired, ref posVel, followLag);
        return rot;
    }

    // Lock-on: the arm sits on the boss-player line behind the player and looks at a point weighted toward the boss.
    // Distance and FOV open with the gap and during telegraphs, and open further if either subject nears the frame edge.
    Vector3 LockPose(EclipseBoss boss, out Quaternion rotation, out float fov)
    {
        var c = lockCfg;
        float dt = Time.deltaTime;
        Vector3 p = target.position;
        Vector3 b = BossAnchor(boss);
        Vector3 toBoss = b - p; toBoss.y = 0f;
        float gap = toBoss.magnitude;
        if (gap > 0.1f)
            lockYaw = Mathf.SmoothDampAngle(lockYaw, Mathf.Atan2(toBoss.x, toBoss.z) * Mathf.Rad2Deg, ref lockYawVel, c.yawSmooth);

        float wide = Mathf.Clamp01(Mathf.InverseLerp(c.nearGap, c.farGap, gap) + (boss.Charging ? c.bigAttackBonus : 0f));
        float wantDist = Mathf.Lerp(c.distance, c.farDistance, wide);
        float wantFov = Mathf.Lerp(c.minFov, c.maxFov, wide);

        Quaternion arm = Quaternion.Euler(c.pitch, lockYaw, 0f);
        Vector3 chest = p + Vector3.up * c.height;
        Vector3 pivot = chest + Quaternion.Euler(0f, lockYaw, 0f) * Vector3.right * c.shoulder;
        float pull = PhasePull(boss);
        Vector3 pos = LockCollide(pivot, -(arm * Vector3.forward), lockDist + pull * c.phasePullDistance, boss);
        // Frame the boss by its core (the lock-on point), carried along with the eased anchor.
        if (bossVisual == null || bossVisual.gameObject != boss.gameObject) bossVisual = boss.GetComponent<EclipseVisual>();
        Vector3 core = b + (bossVisual != null && bossVisual.Core != null ? bossVisual.Core.position - boss.transform.position : Vector3.zero);
        Vector3 look = Vector3.Lerp(chest, core, c.bossWeight);

        // Yaw aims at the weighted look point. Pitch and FOV are solved so the player sits at playerScreenY
        // and the boss at bossScreenY; if that needs more than maxFov, the arm pulls back instead.
        float aP = Elevation(pos, p + Vector3.up * 0.9f), aB = Elevation(pos, core);
        float fitFov = (aB - aP) / Mathf.Max(0.05f, c.bossScreenY - c.playerScreenY);
        wantFov = Mathf.Clamp(Mathf.Max(fitFov, wantFov), c.minFov, c.maxFov);
        lockFov = Mathf.SmoothDamp(lockFov, wantFov, ref lockFovVel, c.adaptSmooth);
        // Exact pitch: put the player at playerScreenY, tilt up if the core would pass bossMaxScreenY,
        // but never push the player below minPlayerScreenY; past that the arm pulls back.
        float tanHalf = Mathf.Tan(lockFov * 0.5f * Mathf.Deg2Rad);
        float pitchForPlayer = aP - Mathf.Atan((c.playerScreenY * 2f - 1f) * tanHalf) * Mathf.Rad2Deg;
        float pitchForCore = aB - Mathf.Atan((c.bossMaxScreenY * 2f - 1f) * tanHalf) * Mathf.Rad2Deg;
        float pitchLimit = aP - Mathf.Atan((c.minPlayerScreenY * 2f - 1f) * tanHalf) * Mathf.Rad2Deg;
        float viewPitch = Mathf.Min(Mathf.Max(pitchForPlayer, pitchForCore), pitchLimit);
        if (pitchForCore > pitchLimit) wantDist = Mathf.Min(c.farDistance + 4f, wantDist + (pitchForCore - pitchLimit) * 0.25f);
        lockDist = Mathf.SmoothDamp(lockDist, wantDist, ref lockDistVel, c.adaptSmooth);
        Vector3 flatLook = look - pos; flatLook.y = 0f;
        float viewYaw = flatLook.sqrMagnitude > 0.01f ? Mathf.Atan2(flatLook.x, flatLook.z) * Mathf.Rad2Deg : lockYaw;
        rotation = Quaternion.Euler(-viewPitch, viewYaw, 0f);

        // Mouse only nudges the view a few degrees and springs back; it never moves the arm.
        if (!EclipseEncounter.Finished) nudge += LucidInput.Look.ReadValue<Vector2>() * c.nudgeSensitivity;
        nudge = Vector2.ClampMagnitude(nudge, c.nudgeMax);
        nudge = Vector2.Lerp(nudge, Vector2.zero, 1f - Mathf.Exp(-c.nudgeReturn * dt));
        rotation = Quaternion.AngleAxis(nudge.x, Vector3.up) * rotation * Quaternion.AngleAxis(-nudge.y, Vector3.right);

        fov = lockFov + pull * c.phasePullFov;
        LookPoint = look;
        LockDebug = $"LOCK  dist {lockDist:F1}  fov {lockFov:F0}  gap {gap:F1}  nudge {nudge.x:F1},{nudge.y:F1}";
        return pos;
    }

    // Sleepwalk: when the boss jumps, hold the old framing briefly, then ease onto the new position.
    Vector3 BossAnchor(EclipseBoss boss)
    {
        var c = lockCfg;
        Vector3 now = boss.transform.position;
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

    // Deoccluder: the arm shortens at once when arena geometry is in the way and eases back out.
    // The boss has no collider, so the camera is also pushed out of its body sphere.
    Vector3 LockCollide(Vector3 pivot, Vector3 back, float length, EclipseBoss boss)
    {
        var c = lockCfg;
        float free = length;
        int hits = Physics.SphereCastNonAlloc(pivot, c.probeRadius, back, obstructionHits, length,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            var hit = obstructionHits[i];
            if (hit.transform.IsChildOf(target)) continue;
            // Thin props (lamp poles) would yank the arm in for a frame or two; the camera passes them instead.
            Vector3 e = hit.collider.bounds.extents;
            if (e.x < c.thinProp && e.z < c.thinProp) continue;
            free = Mathf.Min(free, Mathf.Max(0.6f, hit.distance - 0.05f));
        }
        lockArm = free < lockArm ? free : Mathf.SmoothDamp(lockArm, free, ref lockArmVel, 0.25f);
        Vector3 pos = pivot + back * lockArm;
        // A shortened arm rises so the camera looks down over the player instead of into their back.
        pos.y += (length - lockArm) * 0.3f;
        pos.y = Mathf.Max(pos.y, target.position.y + 0.4f);

        Vector3 fromBoss = pos - boss.transform.position;
        float clear = boss.radius * boss.transform.localScale.y / 1.25f + c.bossClearance;
        if (fromBoss.sqrMagnitude < clear * clear)
            pos = boss.transform.position + (fromBoss.sqrMagnitude > 0.001f ? fromBoss.normalized : Vector3.up) * clear;
        return pos;
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
