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
    public float sensitivity = 0.12f;
    public float minPitch = -10f;
    public float maxPitch = 55f;

    [Header("Feel")]
    public float followLag = 0.12f;     // seconds of lag. Higher = floatier
    public float baseFov = 60f;
    public float fovKick = 14f;         // extra FOV at full speed
    public float distanceKick = 1.5f;

    public static ThirdPersonCamera Instance;
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
        if (LockWeight > 0f) yaw = lockYaw;
        var mouse = Mouse.current;
        if (mouse != null && LockWeight <= 0f)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * sensitivity;
            pitch = Mathf.Clamp(pitch - d.y * sensitivity, minPitch, maxPitch);
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

        if (trauma > 0f)
        {
            transform.position += Random.insideUnitSphere * trauma * trauma * 0.6f;
            trauma = Mathf.Max(0f, trauma - 2.2f * Time.unscaledDeltaTime);
        }
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
        Vector3 b = boss.transform.position;
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
        Vector3 pos = LockCollide(pivot, -(arm * Vector3.forward), lockDist, boss);
        Vector3 look = Vector3.Lerp(chest, b, c.bossWeight);

        // Yaw aims at the weighted look point. Pitch and FOV are solved so the player sits at playerScreenY
        // and the boss at bossScreenY; if that needs more than maxFov, the arm pulls back instead.
        float aP = Elevation(pos, p + Vector3.up * 0.9f), aB = Elevation(pos, b);
        float fitFov = (aB - aP) / Mathf.Max(0.05f, c.bossScreenY - c.playerScreenY);
        float inFrameFov = (aB - aP) / Mathf.Max(0.05f, c.bossMaxScreenY - c.playerScreenY);
        if (inFrameFov > c.maxFov) wantDist = Mathf.Min(c.farDistance + 4f, wantDist + (inFrameFov - c.maxFov) * 0.15f);
        wantFov = Mathf.Clamp(Mathf.Max(fitFov, wantFov), c.minFov, c.maxFov);
        lockDist = Mathf.SmoothDamp(lockDist, wantDist, ref lockDistVel, c.adaptSmooth);
        lockFov = Mathf.SmoothDamp(lockFov, wantFov, ref lockFovVel, c.adaptSmooth);
        float viewPitch = aP - (c.playerScreenY * 2f - 1f) * lockFov * 0.5f;
        Vector3 flatLook = look - pos; flatLook.y = 0f;
        float viewYaw = flatLook.sqrMagnitude > 0.01f ? Mathf.Atan2(flatLook.x, flatLook.z) * Mathf.Rad2Deg : lockYaw;
        rotation = Quaternion.Euler(-viewPitch, viewYaw, 0f);

        // Mouse only nudges the view a few degrees and springs back; it never moves the arm.
        var mouse = Mouse.current;
        if (mouse != null && !EclipseEncounter.Finished) nudge += mouse.delta.ReadValue() * c.nudgeSensitivity;
        nudge = Vector2.ClampMagnitude(nudge, c.nudgeMax);
        nudge = Vector2.Lerp(nudge, Vector2.zero, 1f - Mathf.Exp(-c.nudgeReturn * dt));
        rotation = Quaternion.AngleAxis(nudge.x, Vector3.up) * rotation * Quaternion.AngleAxis(-nudge.y, Vector3.right);

        fov = lockFov;
        LookPoint = look;
        LockDebug = $"LOCK  dist {lockDist:F1}  fov {lockFov:F0}  gap {gap:F1}  nudge {nudge.x:F1},{nudge.y:F1}";
        return pos;
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
            if (!obstructionHits[i].transform.IsChildOf(target))
                free = Mathf.Min(free, Mathf.Max(0.6f, obstructionHits[i].distance - 0.05f));
        lockArm = free < lockArm ? free : Mathf.SmoothDamp(lockArm, free, ref lockArmVel, 0.25f);
        Vector3 pos = pivot + back * lockArm;
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
