using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Flow movement for the memory: momentum, wide curves, dash chains and a smooth "recall" instead of a hard respawn.
// WASD moves, Shift dashes, Space jumps, and Q changes worlds.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform cameraTransform;

    [Header("Flow (tune these)")]
    public float maxSpeed = 8f;
    public float accel = 45f;        // how fast you build speed
    public float glide = 16f;         // how fast extra speed (from dashes or rings) bleeds off
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

    public Vector3 checkpoint;
    public Vector3 Velocity { get { return vel; } }
    public int Chain { get { return chain; } }
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
    float recallCost;
    float recallTargetT;
    float invulnUntil;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        avatar = GetComponentInChildren<MemoryAvatar>();
        checkpoint = transform.position;
    }

    // Enemy hit. Dashing gives brief invulnerability; empty health ends the encounter.
    public void Damage(float amount)
    {
        if (EclipseEncounter.Finished) return;
        if (recalling) return;
        if (dashLeft > 0f || Time.time - lastDashEnd < 0.12f) return;
        if (Time.time < invulnUntil) return;
        invulnUntil = Time.time + 0.35f;
        LastHitTime = Time.time;
        if (avatar != null)
        {
            avatar.health = Mathf.Max(0f, avatar.health - amount);
            avatar.Scatter(0.2f);
            if (avatar.health <= 0f && EclipseEncounter.Instance != null)
            {
                avatar.health = 0f;
                EclipseEncounter.Instance.End(false);
                return;
            }
        }
        ThirdPersonCamera.Shake(0.4f); // about 0.2 s; boss slams shake harder
        GameFeel.HitStop(0.04f);
    }

    // Flat unit direction to the boss, or zero when there is no boss.
    Vector3 BossDirection()
    {
        var boss = EclipseEncounter.Instance != null ? EclipseEncounter.Instance.Boss : null;
        if (boss == null) return Vector3.zero;
        Vector3 d = boss.transform.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.zero;
    }

    // A ring or pad gives a push in a direction.
    public void Boost(Vector3 dir, float speed)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        vel = dir.normalized * Mathf.Max(speed, vel.magnitude);
        dashLeft = 0f;
    }

    // Fell or got hurt: flow back along the path you just walked.
    public void BeginRecall(float healthCost)
    {
        if (recalling) return;
        recalling = true;
        recallCost = healthCost;
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
        if (avatar != null)
        {
            avatar.health = Mathf.Max(0.05f, avatar.health - recallCost);
            if (avatar.health <= 0.06f) avatar.health = 1f; // out of health: restore (no fail state yet)
            avatar.Sharpen();
        }
    }

    void UpdateRecall()
    {
        if (crumbs.Count == 0)
        {
            transform.position = checkpoint;
            EndRecall();
            return;
        }
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
        if (EclipseEncounter.Finished) return;
        if (recalling) { UpdateRecall(); return; }
        var kb = Keyboard.current;
        if (kb == null) return;

        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1;
        if (kb.sKey.isPressed) input.y -= 1;
        if (kb.dKey.isPressed) input.x += 1;
        if (kb.aKey.isPressed) input.x -= 1;
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 fwd = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
        // Boss lock-on: W/S move toward and away from the boss, A/D strafe around it.
        Vector3 toBoss = BossDirection();
        if (ThirdPersonCamera.LockedOn && toBoss.sqrMagnitude > 0.01f) fwd = toBoss;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 wish = fwd * input.y + right * input.x;
        bool hasWish = wish.sqrMagnitude > 0.01f;

        // Dash.
        if ((kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame) && dashLeft <= 0f && Time.time - lastDash > dashCooldown)
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
        if (cc.isGrounded && kb.spaceKey.wasPressedThisFrame) vy = 7f;
        vy += gravity * Time.deltaTime;
        Vector3 move = vel;
        move.y = vy;
        cc.Move(move * Time.deltaTime);
        var encounter = EclipseEncounter.Instance;
        if (encounter != null && encounter.BossFightStarted)
        {
            Vector3 offset = transform.position - EclipseEncounter.ArenaCenter;
            offset.y = 0f;
            float limit = EclipseArena.SqueezeRadius - cc.radius;
            if (offset.sqrMagnitude > limit * limit)
            {
                Vector3 correction = offset.normalized * limit - offset;
                cc.Move(correction);
                vel = Vector3.ProjectOnPlane(vel, offset.normalized);
            }
            else if (offset.sqrMagnitude < EclipseArena.CoreRadius * EclipseArena.CoreRadius)
            {
                Vector3 outward = offset.sqrMagnitude > 0.001f ? offset.normalized : Vector3.back;
                cc.Move(outward * EclipseArena.CoreRadius - offset);
                if (Vector3.Dot(vel, outward) < 0f) vel = Vector3.ProjectOnPlane(vel, outward);
            }
        }

        Vector3 flat = ThirdPersonCamera.LockedOn && toBoss.sqrMagnitude > 0.01f ? toBoss : vel; flat.y = 0f;
        if (flat.sqrMagnitude > 0.5f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 10f * Time.deltaTime);

        // Breadcrumbs for recall, only from solid ground.
        if (cc.isGrounded && Time.time >= nextCrumb)
        {
            nextCrumb = Time.time + 0.05f;
            crumbs.Add(new Crumb { pos = transform.position, t = Time.time });
            while (crumbs.Count > 0 && Time.time - crumbs[0].t > crumbSeconds) crumbs.RemoveAt(0);
        }

        bool swapPressed = kb.qKey.wasPressedThisFrame;
        if (swapPressed && WorldState.Instance != null) WorldState.Instance.Swap();
    }
}
