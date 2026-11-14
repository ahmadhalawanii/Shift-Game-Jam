using UnityEngine;

// Tunables for the boss-fight lock-on camera. Lives at Resources/CameraLockSettings.asset
// (menu Lucid > Build Camera Lock Settings). If the asset is missing, these code defaults are used.
[CreateAssetMenu(menuName = "Lucid/Camera Lock Settings")]
public class CameraLockSettings : ScriptableObject
{
    [Header("Blend")]
    public float blendIn = 0.38f;
    public float blendOut = 0.42f;

    [Header("Framing")]
    public float distance = 4.8f;          // normal lock-on arm length
    public float height = 1.6f;            // pivot height above the player's feet
    public float pitch = 12f;              // arm pitch in degrees
    public float shoulder = 0.55f;         // sideways offset to the right
    [Range(0f, 1f)] public float bossWeight = 0.45f; // look point: 0 = player, 1 = boss core
    public float yawSmooth = 0.18f;        // seconds; how tight the orbit tracks the boss

    [Min(0.01f)] public float framingSmooth = 0.16f;
    [Min(1f)] public float maxTurnSpeed = 220f;

    [Header("Close-range distance and FOV")]
    public float nearGap = 7f;             // player-boss gap (m) where framing is widest
    public float farGap = 19f;             // gap where framing is tightest
    public float closeDistance = 6.1f;
    public float minFov = 65f;
    public float maxFov = 70f;
    [Range(0f, 1f)] public float playerScreenY = 0.32f; // player height on screen (0 = bottom)
    [Range(0f, 1f)] public float bossScreenY = 0.66f;  // boss height on screen when it fits
    [Range(0f, 1f)] public float bossMaxScreenY = 0.8f;   // the core never goes above this
    [Range(0f, 1f)] public float minPlayerScreenY = 0.22f; // the player may drop to here to fit the core, then the arm pulls back
    public float adaptSmooth = 0.35f;      // SmoothDamp time for distance and FOV

    [Header("Mouse nudge")]
    public float nudgeMax = 16f;           // degrees
    public float nudgeSensitivity = 0.65f;
    public float nudgeReturn = 2.8f;         // spring-back rate per second

    [Header("Collision")]
    public float probeRadius = 0.3f;
    public float bossClearance = 0.8f;     // extra metres kept from the boss body

    [Header("Boss events")]
    public float teleportHold = 0.15f;
    public float teleportEase = 0.4f;
    public float phasePullBack = 1.5f;     // seconds of cinematic pull back at a phase transition
    public float phasePullDistance = 0.65f;
    public float phasePullFov = 1.5f;
    public float shakeScale = 0.6f;        // shake while locked (camera is closer, so less)
    [Range(0f, 1f)] public float shakeVertical = 0.3f; // vertical share; bullets fly in the horizontal plane

    static CameraLockSettings cached;
    public static CameraLockSettings Load()
    {
        if (cached == null) cached = Resources.Load<CameraLockSettings>("CameraLockSettings");
        if (cached == null) cached = CreateInstance<CameraLockSettings>();
        return cached;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Lucid/Build Camera Lock Settings")]
    static void Build()
    {
        const string path = "Assets/Resources/CameraLockSettings.asset";
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<CameraLockSettings>(path) != null) { Debug.Log("CameraLockSettings already at " + path); return; }
        UnityEditor.AssetDatabase.CreateAsset(CreateInstance<CameraLockSettings>(), path);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("CameraLockSettings created at " + path);
    }
#endif
}
