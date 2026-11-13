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

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

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
        if (target == null) return;
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * sensitivity;
            pitch = Mathf.Clamp(pitch - d.y * sensitivity, minPitch, maxPitch);
        }

        float speed01 = player != null ? Mathf.Clamp01(player.Velocity.magnitude / 22f) : 0f;
        if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + speed01 * fovKick, 5f * Time.deltaTime);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focus = target.position + Vector3.up * height;
        Vector3 desired = focus - rot * Vector3.forward * (distance + speed01 * distanceKick);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref posVel, followLag);
        transform.rotation = rot;
        if (trauma > 0f)
        {
            transform.position += Random.insideUnitSphere * trauma * trauma * 0.6f;
            trauma = Mathf.Max(0f, trauma - 2.2f * Time.unscaledDeltaTime);
        }
    }
}
