using UnityEngine;
using UnityEngine.InputSystem;

// Smooth orbit camera behind the player. Mouse looks around. FOV and distance open up with speed.
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

    float yaw;
    float pitch = 7f;
    Vector3 posVel;
    Camera cam;
    PlayerController player;
    bool bossFocus;
    readonly RaycastHit[] obstructionHits = new RaycastHit[16];

    void Awake() { Instance = this; }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        cam = GetComponent<Camera>();
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            player = target.GetComponent<PlayerController>();
        }
    }

    void LateUpdate()
    {
        if (EclipseEncounter.Finished) return;
        if (target == null) return;
        var mouse = Mouse.current;
        var boss = EclipseEncounter.Instance != null ? EclipseEncounter.Instance.Boss : null;
        bool canFocus = boss != null && boss.Engaged;
        if (canFocus && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            bossFocus = !bossFocus;

        if (canFocus && bossFocus)
        {
            Vector3 toBoss = boss.transform.position - (target.position + Vector3.up * height);
            float blend = 1f - Mathf.Exp(-5f * Time.deltaTime);
            if (new Vector2(toBoss.x, toBoss.z).sqrMagnitude > 1f)
                yaw = Mathf.LerpAngle(yaw, Mathf.Atan2(toBoss.x, toBoss.z) * Mathf.Rad2Deg, blend);
            pitch = Mathf.Lerp(pitch, 12f, blend);
        }
        else if (mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * sensitivity;
            pitch = Mathf.Clamp(pitch - d.y * sensitivity, minPitch, maxPitch);
        }

        float speed01 = player != null ? Mathf.Clamp01(player.Velocity.magnitude / 22f) : 0f;
        if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + speed01 * fovKick, 5f * Time.deltaTime);

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
        if (arm + 0.01f < distance + speed01 * distanceKick && (transform.position - focus).sqrMagnitude > arm * arm)
        {
            transform.position = desired;
            posVel = Vector3.zero;
        }
        else transform.position = Vector3.SmoothDamp(transform.position, desired, ref posVel, followLag);
        Quaternion view = rot;
        if (canFocus && bossFocus)
        {
            // Keep the player and the boss in frame without tilting the camera arm at the boss.
            Vector3 framing = Vector3.Lerp(focus, boss.transform.position, 0.32f);
            view = Quaternion.LookRotation(framing - transform.position);
        }
        transform.rotation = canFocus && bossFocus
            ? Quaternion.Slerp(transform.rotation, view, 1f - Mathf.Exp(-15f * Time.deltaTime)) : rot;
        if (trauma > 0f)
        {
            transform.position += Random.insideUnitSphere * trauma * trauma * 0.6f;
            trauma = Mathf.Max(0f, trauma - 2.2f * Time.unscaledDeltaTime);
        }
    }
}
