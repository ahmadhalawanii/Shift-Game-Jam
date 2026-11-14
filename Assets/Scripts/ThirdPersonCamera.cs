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

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        cam = GetComponent<Camera>();
        lockCfg = CameraLockSettings.Load();
        freePos = transform.position;
        freeFov = cam != null ? cam.fieldOfView : baseFov;
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
    Vector3 LockPose(EclipseBoss boss, out Quaternion rotation, out float fov)
    {
        var c = lockCfg;
        Vector3 p = target.position;
        Vector3 b = boss.transform.position;
        Vector3 toBoss = b - p; toBoss.y = 0f;
        if (toBoss.sqrMagnitude > 0.01f)
            lockYaw = Mathf.SmoothDampAngle(lockYaw, Mathf.Atan2(toBoss.x, toBoss.z) * Mathf.Rad2Deg, ref lockYawVel, c.yawSmooth);

        Quaternion arm = Quaternion.Euler(c.pitch, lockYaw, 0f);
        Vector3 pivot = p + Vector3.up * c.height + Quaternion.Euler(0f, lockYaw, 0f) * Vector3.right * c.shoulder;
        Vector3 pos = pivot - arm * Vector3.forward * c.distance;
        Vector3 look = Vector3.Lerp(p + Vector3.up * c.height, b, c.bossWeight);
        rotation = Quaternion.LookRotation(look - pos);
        fov = c.minFov;
        LockDebug = $"LOCK  dist {c.distance:F1}  fov {fov:F0}  gap {toBoss.magnitude:F1}";
        return pos;
    }

    // Editor and development builds: L toggles the lock for testing.
    void DebugToggle()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame && !EclipseEncounter.Finished) locked = !locked;
#endif
    }
}
