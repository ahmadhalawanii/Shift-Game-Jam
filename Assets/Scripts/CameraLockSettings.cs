using UnityEngine;

// Tunables for the boss-fight lock-on camera. Lives at Resources/CameraLockSettings.asset
// (menu Lucid > Build Camera Lock Settings). If the asset is missing, these code defaults are used.
[CreateAssetMenu(menuName = "Lucid/Camera Lock Settings")]
public class CameraLockSettings : ScriptableObject
{
    [Header("Blend")]
    public float blendIn = 0.6f;
    public float blendOut = 0.8f;

    [Header("Framing")]
    public float distance = 4.5f;          // arm length when close to the boss
    public float height = 1.6f;            // pivot height above the player's feet
    public float pitch = 17f;              // arm pitch in degrees
    public float shoulder = 0.45f;         // sideways offset to the right
    [Range(0f, 1f)] public float bossWeight = 0.65f; // look point: 0 = player, 1 = boss core
    public float yawSmooth = 0.08f;        // seconds; how tight the orbit tracks the boss

    [Header("Adaptive distance and FOV")]
    public float nearGap = 8f;             // player-boss gap (m) where framing is tightest
    public float farGap = 26f;             // gap where framing is widest
    public float farDistance = 7f;
    public float minFov = 60f;
    public float maxFov = 75f;
    public float bigAttackBonus = 0.35f;   // extra pull-back (0..1 of the range) while the boss telegraphs
    [Range(0f, 1f)] public float playerScreenY = 0.3f; // player height on screen (0 = bottom)
    [Range(0f, 1f)] public float bossScreenY = 0.66f;  // boss height on screen when it fits
    [Range(0f, 1f)] public float bossMaxScreenY = 0.88f; // above this the arm pulls back to keep the boss in frame
    public float adaptSmooth = 0.35f;      // SmoothDamp time for distance and FOV

    [Header("Mouse nudge")]
    public float nudgeMax = 10f;           // degrees
    public float nudgeSensitivity = 0.05f;
    public float nudgeReturn = 4f;         // spring-back rate per second

    [Header("Collision")]
    public float probeRadius = 0.3f;
    public float bossClearance = 0.8f;     // extra metres kept from the boss body

    [Header("Boss events")]
    public float teleportHold = 0.15f;
    public float teleportEase = 0.4f;
    public float phasePullBack = 1.5f;     // seconds of cinematic pull back at a phase transition
    public float phasePullDistance = 6f;
    public float phasePullFov = 8f;
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
