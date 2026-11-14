using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// The spine of the level: zones, checkpoints, fall recovery, hints and the demo/debug keys.
//   G  jump to the next zone          B  jump to the boss entrance          (all builds, for the demo)
//   F1 force Dream   F2 force Nightmare   F3 wave from the player   F4 wave from the boss arena   (editor and dev builds)
[DefaultExecutionOrder(-20)]
public class LevelFlow : MonoBehaviour
{
    [Serializable]
    public class Zone
    {
        public string name;
        public Vector3 spawn;
        public float yaw;
        [TextArea] public string hint;
    }

    public static LevelFlow Instance { get; private set; }
    public static PlayerController Player { get; private set; }

    public Zone[] zones = new Zone[0];
    public PlayerController player;
    public float killDepth = 14f;      // fall this far below the last safe ground to be brought back
    public float fallCost = 0.05f;     // health lost per fall (never the last of it)
    public float firstSwapSpeed = 0.5f; // the first swap of a fresh run is slow and gentle

    public int Current { get; private set; }
    public string Hint { get { return zones.Length > 0 ? zones[Mathf.Clamp(Current, 0, zones.Length - 1)].hint : ""; } }
    public string ZoneName { get { return zones.Length > 0 ? zones[Mathf.Clamp(Current, 0, zones.Length - 1)].name : ""; } }

    // Survives the R reload so a retry returns to the last zone reached. Cleared when play starts.
    static int saved = -1;

    Vector3 safePos;
    float safeYaw;
    float nextSafeCheck;
    MemoryAvatar avatar;
    readonly RaycastHit[] hits = new RaycastHit[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { saved = -1; Instance = null; Player = null; }

    void Awake()
    {
        Instance = this;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        Player = player;
    }

    void OnDestroy() { if (Instance == this) { Instance = null; Player = null; } }

    IEnumerator Start()
    {
        if (player != null)
        {
            avatar = player.GetComponentInChildren<MemoryAvatar>();
            safePos = player.transform.position;
            safeYaw = player.transform.eulerAngles.y;
        }
        // One frame, so the encounter's own boss retry (which moves the player into the arena) goes first.
        yield return null;
        if (player == null) yield break;
        var encounter = EclipseEncounter.Instance;
        if (encounter != null) encounter.Tell("Climb toward the light of the great tree.", 6f);
        if (saved > 0 && !InArena(player.transform.position))
        {
            Current = Mathf.Min(saved, zones.Length - 1);
            MoveTo(Current);
        }
        else if (saved > 0) Current = zones.Length - 1;   // boss retry: the encounter already put the player in the arena
        else WorldShift.NextSpeedScale = firstSwapSpeed;
    }

    static bool InArena(Vector3 p)
    {
        p -= EclipseEncounter.ArenaCenter;
        p.y = 0f;
        return p.magnitude < EclipseEncounter.ArenaRadius + 2f;
    }

    public void Reach(int zone)
    {
        if (zone <= Current || zone >= zones.Length) return;
        Current = zone;
        saved = zone;
        if (player != null) player.checkpoint = zones[zone].spawn;
        if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.Tell(zones[zone].name.ToUpper(), 3.5f);
        if (player != null) Fx.Burst(player.transform.position + Vector3.up, new Color(1f, 0.85f, 0.54f), 30);
    }

    public void MoveTo(int zone)
    {
        if (player == null || zones.Length == 0) return;
        zone = Mathf.Clamp(zone, 0, zones.Length - 1);
        Current = zone;
        saved = Mathf.Max(saved, zone);
        var z = zones[zone];
        player.Teleport(z.spawn, z.yaw);
        player.checkpoint = z.spawn;
        safePos = z.spawn;
        safeYaw = z.yaw;
        if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.Tell(z.name.ToUpper(), 3f);
    }

    // Out of health before the boss fight: wake at the last checkpoint instead of ending the run.
    public void Respawn()
    {
        if (player == null || zones.Length == 0) return;
        var z = zones[Mathf.Clamp(Current, 0, zones.Length - 1)];
        EnemyBullet.Clear(Vector3.zero, float.PositiveInfinity);
        player.Teleport(z.spawn, z.yaw);
        safePos = z.spawn;
        safeYaw = z.yaw;
        if (avatar != null) avatar.health = 1f;
        if (EclipseEncounter.Instance != null) EclipseEncounter.Instance.Tell("You fade, and wake at the last light.", 3.5f);
    }

    void Update()
    {
        if (player == null || EclipseEncounter.Finished) return;
        var encounter = EclipseEncounter.Instance;
        bool fighting = encounter != null && encounter.BossFightStarted;
        var kb = Keyboard.current;
        if (kb != null && !fighting)
        {
            if (kb.gKey.wasPressedThisFrame) MoveTo(Current + 1);
            if (kb.bKey.wasPressedThisFrame) MoveTo(zones.Length - 1);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (kb != null && WorldState.Instance != null)
        {
            Vector3 chest = player.transform.position + Vector3.up;
            if (kb.f1Key.wasPressedThisFrame) { WorldShift.NextOrigin = chest; WorldState.Instance.Set(World.Dream); }
            if (kb.f2Key.wasPressedThisFrame) { WorldShift.NextOrigin = chest; WorldState.Instance.Set(World.Nightmare); }
            if (kb.f3Key.wasPressedThisFrame) { WorldShift.NextOrigin = chest; Flip(); }
            if (kb.f4Key.wasPressedThisFrame) { WorldShift.NextOrigin = EclipseEncounter.ArenaCenter + Vector3.up; WorldShift.NextSpeedScale = 0.45f; Flip(); }
        }
#endif
        if (fighting) return;

        // Remember the last solid, permanent ground the player stood on.
        if (Time.time >= nextSafeCheck && player.Grounded)
        {
            nextSafeCheck = Time.time + 0.2f;
            Vector3 from = player.transform.position + Vector3.up * 0.5f;
            int n = Physics.RaycastNonAlloc(from, Vector3.down, hits, 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Collider ground = null;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].transform.IsChildOf(player.transform) || hits[i].distance >= best) continue;
                best = hits[i].distance;
                ground = hits[i].collider;
            }
            if (ground != null && ground.GetComponentInParent<PhasePlatform>() == null && ground.GetComponentInParent<BounceBlossom>() == null)
            {
                safePos = player.transform.position;
                safeYaw = player.transform.eulerAngles.y;
            }
        }

        if (player.transform.position.y < safePos.y - killDepth)
        {
            player.Teleport(safePos + Vector3.up * 0.1f, safeYaw);
            if (avatar != null) avatar.health = Mathf.Max(0.05f, avatar.health - fallCost);
            ThirdPersonCamera.Shake(0.2f);
        }
    }

    static void Flip()
    {
        WorldState.Instance.Set(WorldState.Instance.Current == World.Dream ? World.Nightmare : World.Dream);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.5f);
        for (int i = 0; i < zones.Length; i++) Gizmos.DrawWireSphere(zones[i].spawn + Vector3.up, 0.6f);
    }
}
