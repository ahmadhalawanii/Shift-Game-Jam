using UnityEngine;

// Feedback for the traversal moves, kept out of the player and camera scripts:
// a light trail while wall-running and dashing, a camera roll on walls, motes while hovering.
// Runs after ThirdPersonCamera so the roll is applied on top of its final pose each frame.
[DefaultExecutionOrder(100)]
public class TraversalFx : MonoBehaviour
{
    public PlayerController player;
    public TrailRenderer trail;
    public ParticleSystem hoverMotes;
    public float wallRoll = 8f;
    public float rollSpeed = 60f;

    float roll;
    Camera cam;

    void Start()
    {
        if (player == null) player = GetComponentInParent<PlayerController>();
        cam = Camera.main;
        if (trail != null) trail.emitting = false;
    }

    void LateUpdate()
    {
        if (player == null) return;
        bool fast = player.WallRunning || player.IsDashing;
        if (trail != null)
        {
            trail.emitting = fast;
            Color c = WorldState.CurrentOrDream == World.Dream ? new Color(0.55f, 0.95f, 1f) : new Color(1f, 0.6f, 0.35f);
            trail.startColor = c;
            trail.endColor = new Color(c.r, c.g, c.b, 0f);
        }
        if (hoverMotes != null)
        {
            var emission = hoverMotes.emission;
            emission.enabled = player.Hovering;
        }
        // Lean the camera away from the wall.
        roll = Mathf.MoveTowards(roll, -player.WallSide * wallRoll, rollSpeed * Time.unscaledDeltaTime);
        if (cam != null && Mathf.Abs(roll) > 0.01f) cam.transform.rotation *= Quaternion.Euler(0f, 0f, roll);
    }
}
