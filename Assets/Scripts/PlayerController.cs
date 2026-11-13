using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Flow movement for the memory: momentum, dash chains, and a smooth "recall" when memory runs out.
// WASD move, Space dash (chain dashes for speed), Shift / right mouse swap world.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform cameraTransform;

    [Header("Flow (tune these)")]
    public float maxSpeed = 8f;
    public float accel = 45f;        // how fast you build speed
    public float glide = 16f;         // how fast extra speed (from dashes) bleeds off
    public float friction = 38f;      // drift to a stop when you let go. Lower = floatier
    public float turnRate = 14f;      // radians per second your direction swings. Lower = wider arcs
    public float gravity = -25f;

    [Header("Dash")]
    public float dashSpeed = 28f;
    public float dashTime = 0.16f;
    public float dashCooldown = 0.3f;
    public float afterDashSpeed = 15f;
    public float chainWindow = 0.55f;  // dash again this soon after a dash to chain
    public float chainBoost = 0.25f;   // extra speed per chain step
    public int maxChain = 3;

    [Header("Recall")]
    public float recallSpeed = 34f;
    public float crumbSeconds = 6f;
    public float recallBackSeconds = 1.2f;

    public static PlayerController Instance { get; private set; }
    public PlayerCombat Combat { get; private set; }
    public Vector3 Velocity { get { return vel; } }
    public bool Recalling { get { return recalling; } }
    public bool IsDashing { get { return dashLeft > 0f; } }
    public float LastHitTime { get; private set; }

    struct Crumb { public Vector3 pos; public float t; }

    CharacterController cc;
    MemoryAvatar avatar;
    Vector3 vel;
    float vy;
    float dashLeft;
    float lastDash = -99f;
    float lastDashEnd = -99f;
    int chain;
    Vector3 dashDir;

    readonly List<Crumb> crumbs = new List<Crumb>();
    float nextCrumb;
    bool recalling;
    float recallTargetT;
    float invulnUntil;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        avatar = GetComponentInChildren<MemoryAvatar>();
        Combat = GetComponent<PlayerCombat>();
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // Enemy hit. Dashing gives brief invulnerability. Running out of memory flows you back and restores it (no fail state yet).
    public void Damage(float amount)
    {
        if (recalling) return;
        if (dashLeft > 0f || Time.time - lastDashEnd < 0.12f) return;
        if (Time.time < invulnUntil) return;
        invulnUntil = Time.time + 0.35f;
        LastHitTime = Time.time;
        if (avatar != null)
        {
            avatar.memory = Mathf.Max(0.05f, avatar.memory - amount);
            avatar.Scatter(0.2f);
            if (avatar.memory <= 0.06f) { avatar.memory = 1f; BeginRecall(); }
        }
        ThirdPersonCamera.Shake(0.25f);
        GameFeel.HitStop(0.04f);
    }

    // Out of memory: flow back along the path you just walked, then return at full memory.
    void BeginRecall()
    {
        if (recalling) return;
        recalling = true;
        recallTargetT = Time.time - recallBackSeconds;
        cc.enabled = false;
        vel = Vector3.zero; vy = 0f; dashLeft = 0f;
        if (avatar != null) avatar.Scatter(0.3f);
    }

    void EndRecall()
    {
        recalling = false;
        cc.enabled = true;
        vel = Vector3.zero;
        if (avatar != null) avatar.Sharpen();
    }

    void UpdateRecall()
    {
        if (crumbs.Count == 0) { EndRecall(); return; }
        var c = crumbs[crumbs.Count - 1];
        transform.position = Vector3.MoveTowards(transform.position, c.pos, recallSpeed * Time.deltaTime);
        if ((transform.position - c.pos).sqrMagnitude < 0.01f)
        {
            if (c.t <= recallTargetT) EndRecall();
            else crumbs.RemoveAt(crumbs.Count - 1);
        }
    }

    void Update()
    {
        if (recalling) { UpdateRecall(); return; }
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null) return;

        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1;
        if (kb.sKey.isPressed) input.y -= 1;
        if (kb.dKey.isPressed) input.x += 1;
        if (kb.aKey.isPressed) input.x -= 1;
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 fwd = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 wish = fwd * input.y + right * input.x;
        bool hasWish = wish.sqrMagnitude > 0.01f;

        // Dash.
        if (kb.spaceKey.wasPressedThisFrame && dashLeft <= 0f && Time.time - lastDash > dashCooldown)
        {
            dashDir = hasWish ? wish.normalized : (vel.sqrMagnitude > 0.5f ? vel.normalized : transform.forward);
            chain = (Time.time - lastDashEnd < chainWindow) ? Mathf.Min(chain + 1, maxChain) : 0;
            dashLeft = dashTime;
            lastDash = Time.time;
            if (avatar != null) avatar.Scatter(dashTime + 0.12f);
        }

        if (dashLeft > 0f)
        {
            float boost = 1f + chain * chainBoost;
            vel = dashDir * dashSpeed * boost;
            dashLeft -= Time.deltaTime;
            if (dashLeft <= 0f)
            {
                lastDashEnd = Time.time;
                vel = dashDir * Mathf.Max(afterDashSpeed * boost, maxSpeed);
            }
        }
        else
        {
            Vector3 target = hasWish ? wish * maxSpeed : Vector3.zero;
            if (hasWish && vel.sqrMagnitude > 0.25f)
                vel = Vector3.RotateTowards(vel, wish.normalized * vel.magnitude, turnRate * Time.deltaTime, 0f);
            float over = vel.magnitude - target.magnitude;
            float rate = hasWish ? (over > 0.5f ? glide : accel) : (over > 0.5f ? Mathf.Min(glide, friction) : friction);
            vel = Vector3.MoveTowards(vel, target, rate * Time.deltaTime);
        }

        if (cc.isGrounded && vy < 0f) vy = -2f;
        vy += gravity * Time.deltaTime;
        Vector3 move = vel;
        move.y = vy;
        cc.Move(move * Time.deltaTime);

        Vector3 flat = vel; flat.y = 0f;
        if (flat.sqrMagnitude > 0.5f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 10f * Time.deltaTime);

        // Breadcrumbs for recall, only from solid ground.
        if (cc.isGrounded && Time.time >= nextCrumb)
        {
            nextCrumb = Time.time + 0.05f;
            crumbs.Add(new Crumb { pos = transform.position, t = Time.time });
            while (crumbs.Count > 0 && Time.time - crumbs[0].t > crumbSeconds) crumbs.RemoveAt(0);
        }

        bool swapPressed = kb.leftShiftKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame);
        if (swapPressed && WorldState.Instance != null) WorldState.Instance.Swap();
    }
}
