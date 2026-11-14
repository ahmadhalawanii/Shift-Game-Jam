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

    // Traversal zones are fully 3D. The boss arena stays plane-locked: there the jump is the short hop,
    // and hover, wall-run and the air-dash limit are off.
    [Header("Jump and air (outside the boss arena)")]
    public float jumpSpeed = 7f;             // the hop used in the boss arena
    public float jumpSpeedDream = 10.5f;     // floaty: about 2.2 m high
    public float jumpSpeedNightmare = 9.2f;  // sharper: about 1.7 m high
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;
    public float hoverTime = 0.6f;           // Dream only: hold Space while falling
    public float hoverFallSpeed = 1.2f;
    public float nightmareDashScale = 1.15f;

    [Header("Wall run (only on WallRunSurface)")]
    public float wallRunTime = 1.8f;
    public float wallRunSpeed = 11f;
    public float wallRunRise = 2.2f;         // upward speed at the start of the run
    public float wallRunSink = 1.6f;         // downward speed at the end
    public float wallJumpUp = 9.5f;
    public float wallJumpOut = 7f;
    public float wallReach = 1.25f;          // how close to the wall a run can start
    public float wallStick = 0.62f;          // distance kept from the wall while running

    public Vector3 checkpoint;
    public Vector3 Velocity { get { return vel; } }
    public float VerticalSpeed { get { return vy; } }
    public int Chain { get { return chain; } }
    public bool Recalling { get { return recalling; } }
    public bool IsDashing { get { return dashLeft > 0f; } }
    public float LastHitTime { get; private set; }
    public bool Grounded { get { return cc != null && cc.enabled && cc.isGrounded; } }
    public bool WallRunning { get; private set; }
    public bool Hovering { get; private set; }
    // +1 when the wall is on the player's right, -1 on the left, 0 when not wall-running.
    public float WallSide { get; private set; }

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

    float lastGrounded = -99f;
    float lastJumpPressed = -99f;
    bool airDashUsed;
    float hoverLeft;
    float wallRunLeft;
    float wallCooldownUntil;
    Vector3 wallNormal, wallTangent;
    WallRunSurface wall, lastWall;

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
                // On the way to the boss, fading only costs the walk back from the last checkpoint.
                if (LevelFlow.Instance != null && !EclipseEncounter.Instance.BossFightStarted) { LevelFlow.Instance.Respawn(); return; }
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
        EndWallRun(0.4f);
    }

    // A bounce blossom throws the player upward and gives the air dash and hover back.
    public void Launch(float upSpeed)
    {
        vy = upSpeed;
        airDashUsed = false;
        hoverLeft = hoverTime;
        lastGrounded = -99f;
        EndWallRun(0.1f);
    }

    // Instant move (checkpoints, zone jumps). Clears momentum and the recall trail.
    public void Teleport(Vector3 position, float yaw)
    {
        bool was = cc.enabled;
        cc.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        cc.enabled = was;
        vel = Vector3.zero; vy = 0f; dashLeft = 0f;
        crumbs.Clear();
        EndWallRun(0.1f);
        if (avatar != null) avatar.Scatter(0.25f);
    }

    void EndWallRun(float cooldown)
    {
        if (!WallRunning) return;
        WallRunning = false;
        WallSide = 0f;
        lastWall = wall;
        wall = null;
        wallCooldownUntil = Time.time + cooldown;
    }

    // Nearest marked wall within reach of the chest. Returns false when there is none.
    bool FindWall(WallRunSurface only, float reach, out WallRunSurface found, out Vector3 normal, out float distance)
    {
        found = null; normal = Vector3.zero; distance = float.MaxValue;
        Vector3 chest = transform.position + Vector3.up;
        var all = WallRunSurface.All;
        for (int i = 0; i < all.Count; i++)
        {
            var s = all[i];
            if (s == null || (only != null && s != only) || !s.UsableAt(chest)) continue;
            Vector3 d = chest - s.Closest(chest);
            if (Mathf.Abs(d.y) > 0.9f) continue; // above or below the panel
            d.y = 0f;
            float m = d.magnitude;
            if (m < 0.02f || m > reach || m >= distance) continue;
            found = s; normal = d / m; distance = m;
        }
        return found != null;
    }

    void TryStartWallRun(Vector3 wish, bool hasWish)
    {
        WallRunSurface s; Vector3 n; float d;
        if (!FindWall(null, wallReach, out s, out n, out d)) return;
        if (s == lastWall && Time.time < wallCooldownUntil) return;
        Vector3 t = Vector3.Cross(Vector3.up, n);
        Vector3 flat = vel; flat.y = 0f;
        float along = Vector3.Dot(flat, t);
        if (Mathf.Abs(along) < 3f && hasWish) along = Vector3.Dot(wish, t) * maxSpeed;
        if (Mathf.Abs(along) < 3f) return;
        // Not while moving away from the wall.
        if (Vector3.Dot(flat, n) > 4f) return;
        wall = s;
        wallNormal = n;
        wallTangent = t * Mathf.Sign(along);
        wallRunLeft = wallRunTime;
        WallRunning = true;
        airDashUsed = false;
        hoverLeft = hoverTime;
        dashLeft = 0f;
        if (avatar != null) avatar.Sharpen();
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

        var encounter = EclipseEncounter.Instance;
        bool arena = encounter != null && encounter.BossFightStarted;
        bool nightmare = WorldState.CurrentOrDream == World.Nightmare;
        bool grounded = cc.isGrounded;
        float dt = Time.deltaTime;
        if (grounded) { lastGrounded = Time.time; airDashUsed = false; hoverLeft = hoverTime; lastWall = null; }
        if (kb.spaceKey.wasPressedThisFrame) lastJumpPressed = Time.time;
        bool jumpQueued = Time.time - lastJumpPressed <= jumpBuffer;
        if (arena) EndWallRun(0f);

        // Dash. Outside the boss arena there is one air dash per jump; walls and blossoms give it back.
        bool dashPressed = kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame;
        if (dashPressed && dashLeft <= 0f && Time.time - lastDash > dashCooldown && (arena || grounded || WallRunning || !airDashUsed))
        {
            if (!arena && !grounded) airDashUsed = true;
            EndWallRun(0.35f);
            dashDir = hasWish ? wish.normalized : (vel.sqrMagnitude > 0.5f ? vel.normalized : transform.forward);
            chain = (Time.time - lastDashEnd < chainWindow) ? Mathf.Min(chain + 1, maxChain) : 0;
            dashLeft = dashTime;
            lastDash = Time.time;
            if (avatar != null) avatar.Scatter(dashTime + 0.12f);
        }

        // Wall run: airborne next to a marked wall, moving along it.
        if (!arena && !WallRunning && !grounded && Time.time - lastGrounded > 0.1f) TryStartWallRun(wish, hasWish);
        if (WallRunning)
        {
            WallRunSurface s; Vector3 n; float d;
            bool held = FindWall(wall, wallReach + 0.7f, out s, out n, out d);
            wallRunLeft -= dt;
            if (!held || wallRunLeft <= 0f || (grounded && wallRunLeft < wallRunTime - 0.2f))
            {
                // Running off the end of a wall leaves a short window to jump.
                if (!held) lastGrounded = Time.time;
                EndWallRun(0.45f);
            }
            else if (jumpQueued)
            {
                lastJumpPressed = -99f;
                lastGrounded = -99f;
                vy = wallJumpUp;
                vel = wallTangent * Mathf.Max(wallRunSpeed, maxSpeed) + n * wallJumpOut;
                EndWallRun(0.3f);
                if (avatar != null) avatar.Scatter(0.15f);
            }
            else
            {
                wallNormal = n;
                Vector3 t = Vector3.Cross(Vector3.up, n);
                wallTangent = Vector3.Dot(t, wallTangent) >= 0f ? t : -t;
                vy = Mathf.Lerp(wallRunRise, -wallRunSink, 1f - wallRunLeft / wallRunTime);
                vel = wallTangent * wallRunSpeed - n * ((d - wallStick) * 10f);
                WallSide = Vector3.Dot(Vector3.Cross(Vector3.up, wallTangent), -n) > 0f ? 1f : -1f;
            }
        }

        if (WallRunning) { }
        else if (dashLeft > 0f)
        {
            float boost = (1f + chain * chainBoost) * (nightmare && !arena ? nightmareDashScale : 1f);
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

        if (grounded && vy < 0f) vy = -2f;
        // Jump, with a short buffer before landing and coyote time after leaving a ledge.
        bool canJump = grounded || (!arena && vy <= 0f && Time.time - lastGrounded <= coyoteTime);
        if (jumpQueued && canJump && !WallRunning)
        {
            vy = arena ? jumpSpeed : (nightmare ? jumpSpeedNightmare : jumpSpeedDream);
            lastJumpPressed = -99f;
            lastGrounded = -99f;
        }
        Hovering = false;
        if (!WallRunning)
        {
            bool airDash = dashLeft > 0f && !arena && !grounded;
            if (airDash) vy = 0f; // a flat dash, so it crosses gaps
            else if (!arena && !nightmare && !grounded && vy < 0f && hoverLeft > 0f && kb.spaceKey.isPressed)
            {
                // Dream: hold Space to drift down slowly for a moment.
                Hovering = true;
                hoverLeft -= dt;
                vy = Mathf.MoveTowards(vy, -hoverFallSpeed, 60f * dt);
            }
            else vy += gravity * dt;
        }
        Vector3 move = vel;
        move.y = vy;
        cc.Move(move * Time.deltaTime);
        if (arena)
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

        Vector3 flat = ThirdPersonCamera.LockedOn && toBoss.sqrMagnitude > 0.01f ? toBoss : (WallRunning ? wallTangent : vel); flat.y = 0f;
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
