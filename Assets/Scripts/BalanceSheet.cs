using System;
using UnityEngine;

public enum WeaponKind { Revolver, Scatter, Chain }

// One weapon's numbers. Lives in the BalanceSheet asset.
[Serializable]
public class WeaponData
{
    public string name = "Weapon";
    public WeaponKind kind;
    [Tooltip("Per shot, per pellet, or per second for the beam.")] public float damage = 10f;
    public float shotsPerSecond = 3f;
    public int pellets = 1;
    public int magazine = 6;
    public int startReserve = 12;
    public float reloadTime = 1.7f;
    public float range = 40f;
    public float spreadDegrees = 0.5f;
    [Tooltip("Damage falls linearly to falloffFloor at this distance. 0 = no falloff.")] public float falloffDistance;
    [Range(0f, 1f)] public float falloffFloor = 0.25f;
    public float weakPointMultiplier = 1f;

    [Header("Active reload (magazine weapons)")]
    public float activeReloadTime = 0.9f;
    [Range(0f, 1f)] public float sweetSpotStart = 0.42f;
    [Range(0f, 1f)] public float sweetSpotWidth = 0.14f;
    public float activeBuff = 1.2f;
    public float activeBuffSeconds = 4f;

    [Header("Beam")]
    public float heatSeconds = 3.5f;
    public float lockoutSeconds = 2.5f;
    public float lockConeDegrees = 12f;

    [Header("Feel")]
    public Color color = Color.white;
    public float shake = 0.05f;
    public float fovPunch;
    public float crosshairSpread = 1f;
}

// One ability's numbers.
[Serializable]
public class AbilityData
{
    public string name = "Ability";
    public float cooldown = 10f;
    public float powerCost;
    public float radius = 8f;
    public float amount;     // damage, heal or seconds, see the tooltip on each ability
}

// Every combat number in one asset: Resources/BalanceSheet.asset (menu Lucid > Build Balance Sheet).
// If the asset is missing the code defaults below are used. Boss attack patterns stay in BossAttackData.
[CreateAssetMenu(menuName = "Lucid/Balance Sheet")]
public class BalanceSheet : ScriptableObject
{
    [Header("Player")]
    public float hitImmunity = 0.8f;
    public float maxPower = 100f;
    public float weaponSwapTime = 0.2f;

    [Header("Dread (Nightmare damage multiplier)")]
    public float dreadMaxMultiplier = 1.5f;
    public float dreadGateMultiplier = 1.25f;       // cap outside the boss fight
    public float dreadDamageToFill = 320f;          // damage dealt in Nightmare to go from 0 to full
    [Range(0f, 1f)] public float dreadKeptOnHit = 0.5f;

    [Header("Weapons (Nightmare)")]
    public WeaponData[] weapons =
    {
        new WeaponData { name = "Thorn Revolver", kind = WeaponKind.Revolver, damage = 14f, shotsPerSecond = 3f, magazine = 6, startReserve = 12,
            reloadTime = 1.7f, range = 45f, spreadDegrees = 0.4f, weakPointMultiplier = 1.6f, color = new Color(1f, 0.45f, 0.35f), shake = 0.07f, fovPunch = 1.2f, crosshairSpread = 0.8f },
        new WeaponData { name = "Gloam Scatter", kind = WeaponKind.Scatter, damage = 5f, pellets = 8, shotsPerSecond = 1f, magazine = 2, startReserve = 6,
            reloadTime = 2.4f, activeReloadTime = 1.4f, range = 14f, spreadDegrees = 7f, falloffDistance = 9f, falloffFloor = 0.25f,
            color = new Color(1f, 0.6f, 0.15f), shake = 0.16f, fovPunch = 2f, crosshairSpread = 1.8f },
        new WeaponData { name = "Wraith Chain", kind = WeaponKind.Chain, damage = 22f, magazine = 0, startReserve = 0, range = 20f,
            heatSeconds = 3.5f, lockoutSeconds = 2.5f, lockConeDegrees = 12f, color = new Color(0.7f, 0.35f, 1f), shake = 0.012f, crosshairSpread = 1.2f },
    };
    public float aimAssistDegrees = 3.5f;           // snap to a weak point this close to the crosshair
    public float aimAssistGamepadDegrees = 8f;

    [Header("Boss")]
    public float bossHealth = 1500f;
    [Range(0f, 1f)] public float phase2At = 0.65f;
    [Range(0f, 1f)] public float phase3At = 0.30f;
    [Range(0f, 1f)] public float dreamDamageScale = 0.1f;
    [Range(0f, 1f)] public float windowCap = 0.15f;         // share of boss health one Nightmare window can remove at full value
    [Range(0f, 1f)] public float overCapScale = 0.4f;       // damage beyond the cap is multiplied by this
    [Range(0f, 1f)] public float wakeWindowAt = 0.10f;
    public float wakeWindowSeconds = 4f;
    public float lucidShotCharge = 2f;
    public float wakeWindowGrace = 10f;

    [Header("Weak points")]
    public float weakPointOrbitDegrees = 22f;       // per second around the boss
    public float weakPointSize = 1.15f;
    public float tagSeconds = 8f;
    public int maxTags = 3;
    public float retagLockout = 4f;
    public float cashInMultiplier = 1.6f;
    public float cashInPower = 10f;

    [Header("Dream: Ward and Heart Seeds")]
    public float wardMax = 100f;
    public float wardRegenPerSecond = 10f;
    public float wardRegenDelay = 1f;
    public float wardBreakSeconds = 2.5f;
    public float wardMoveScale = 0.8f;
    public float wardDrainSmall = 8f;
    public float wardDrainBig = 20f;
    public float seedHeal = 4f;                     // out of 100
    public float seedWard = 6f;
    public float seedPower = 5f;
    public float perfectCatchWindow = 0.15f;
    public float perfectCatchMultiplier = 2f;
    [Tooltip("Rounds per seed: x = Revolver, y = Scatter shells. Fractions carry over.")] public Vector2 seedAmmo = new Vector2(1.7f, 0.5f);
    [Range(0f, 1f)] public float pinkSeedShare = 0.25f;     // share of plain Dream orbs that become Heart Seeds
    public int seedStreamCount = 6;                 // extra seeds sent at the player each Dream window
    public float thornShotPower = 3f;
    public float seedWindowHealCap = 25f;
    public float seedStreamInterval = 0.8f;
    public float seedSpeed = 7f;
    public float perfectCatchShotDamage = 8f;
    public float dreamMistSeconds = 1f;
    public float bloomPullSpeed = 7f;
    public float bloomHealRadius = 3f;

    [Header("Dream: Lumen Bow")]
    public float bowDrawTime = 0.6f;
    public float bowInterval = 1.4f;
    public float bowRange = 45f;

    [Header("Abilities")]
    public AbilityData lucidPulse = new AbilityData { name = "Lucid Pulse", cooldown = 10f, radius = 9f };
    [Tooltip("amount = heal per second (out of 100) while standing near the blossom.")]
    public AbilityData bloom = new AbilityData { name = "Bloom", cooldown = 14f, radius = 7f, amount = 2f };
    public float bloomSeconds = 6f;
    [Tooltip("amount = boss stagger in seconds.")]
    public AbilityData burst = new AbilityData { name = "Burst", cooldown = 25f, powerCost = 40f, radius = 12f, amount = 1f };
    [Tooltip("amount = damage multiplier of the empowered shot.")]
    public AbilityData shadowstep = new AbilityData { name = "Shadowstep", cooldown = 6f, powerCost = 20f, radius = 24f, amount = 1.5f };
    [Tooltip("amount = damage multiplier while active.")]
    public AbilityData overdrive = new AbilityData { name = "Overdrive", cooldown = 45f, radius = 0f, amount = 1.35f };
    public float overdriveSeconds = 5f;
    public float overdriveFireRate = 1.4f;

    [Header("Enemies (zones before the boss)")]
    public float mothHealth = 20f;
    public float crawlerHealth = 60f;
    public float watcherHealth = 90f;
    public float budHealth = 14f;

    static BalanceSheet cached;
    public static BalanceSheet Load()
    {
        if (cached == null) cached = Resources.Load<BalanceSheet>("BalanceSheet");
        if (cached == null) cached = CreateInstance<BalanceSheet>();
        return cached;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Lucid/Build Balance Sheet")]
    static void Build()
    {
        const string path = "Assets/Resources/BalanceSheet.asset";
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<BalanceSheet>(path) != null) { Debug.Log("BalanceSheet already at " + path + " (delete it to reset to the code defaults)"); return; }
        UnityEditor.AssetDatabase.CreateAsset(CreateInstance<BalanceSheet>(), path);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("BalanceSheet created at " + path);
    }
#endif
}
