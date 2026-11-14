using System;
using UnityEngine;

// Every boss-fight number lives here. The asset is Resources/BossAttackData (menu Lucid > Build Boss Attack Data).
// Missing asset: runtime falls back to these field defaults.
[CreateAssetMenu(menuName = "Lucid/Boss Attack Data")]
public class BossAttackData : ScriptableObject
{
    [Tooltip("Scales speeds and counts up, telegraphs down.")]
    [Range(0.7f, 1.5f)] public float difficulty = 1f;
    public float bossHealth = 900f;
    [Tooltip("Player weapons were tuned for a 300 hp boss. Incoming damage is multiplied by this. " +
        "1 = the fight is three times longer than the old 300 hp version; the Nightmare multiplier and Lucid Pulse make up some of it.")]
    public float playerDamageScale = 1f;

    public BossPhaseData[] phases =
    {
        new BossPhaseData { swapInterval = 9f, budget = 2, teleportMin = 18f, teleportMax = 25f, sigilTime = 0.9f, gapMin = 0.7f, gapMax = 1.1f, overlay = 0, telegraphScale = 1f },
        new BossPhaseData { swapInterval = 6f, budget = 3, teleportMin = 14f, teleportMax = 20f, sigilTime = 0.75f, gapMin = 0.55f, gapMax = 0.9f, overlay = 1, telegraphScale = 0.9f },
        new BossPhaseData { swapInterval = 3.5f, budget = 4, teleportMin = 10f, teleportMax = 14f, sigilTime = 0.6f, gapMin = 0.45f, gapMax = 0.75f, overlay = 2, telegraphScale = 0.8f },
    };

    [Header("Sleepwalk teleport")]
    public float sleepwalkDreamDamage = 12f;
    public float sleepwalkNightmareDamage = 18f;
    public float slamRadius = 4.5f;
    public float sleepwalkDistance = 5f;
    public float wakeWindow = 1.2f;
    public int sleepwalkBurstCount = 12;

    [Header("Dream: Resonance and Lucid Pulse")]
    public int absorbsPerResonance = 4;
    public float lucidPulseDamage = 60f;
    public float lucidPulseRadius = 12f;
    [Range(0f, 1f)] public float goldSeedChance = 0.12f;
    public float goldSeedHeal = 0.05f;

    [Header("Nightmare: multiplier and Dread")]
    public float multiplierRampSeconds = 10f;
    public float maxDamageMultiplier = 2f;
    public float dreadFillSeconds = 14f;
    public float dreadDrainSeconds = 5f;
    public float dreadDamage = 8f;
    public float shootPowerRefund = 0.5f;

    [Header("World swap")]
    public float swapTelegraph = 1.2f;
    public float shiftRewardPower = 3f;
    public float shiftRewardClearRadius = 7f;

    [Header("Phase 3 arena squeeze")]
    public float squeezeFrom = 20f;
    public float squeezeTo = 13f;
    public float squeezeSeconds = 20f;

    [Tooltip("One row per attack. Rows are added by the builder from the catalog defaults.")]
    public AttackTuning[] attacks = new AttackTuning[0];

    static BossAttackData cached;
    public static BossAttackData Load()
    {
        if (cached == null) cached = Resources.Load<BossAttackData>("BossAttackData");
        if (cached == null) cached = CreateInstance<BossAttackData>();
        return cached;
    }

    public BossPhaseData Phase(int phase) => phases[Mathf.Clamp(phase - 1, 0, phases.Length - 1)];

    public AttackTuning Find(string id)
    {
        for (int i = 0; i < attacks.Length; i++) if (attacks[i] != null && attacks[i].id == id) return attacks[i];
        return null;
    }
}

[Serializable]
public class BossPhaseData
{
    public float swapInterval = 9f;
    [Tooltip("Difficulty budget per cycle. Small attack 1, big 2, teleport 3.")]
    public int budget = 2;
    [Tooltip("0 = one attack at a time, 1 = overlay from another lane after a beat, 2 = two lanes at once.")]
    public int overlay;
    public float gapMin = 1f, gapMax = 1.6f;
    public float teleportMin = 18f, teleportMax = 25f;
    public float sigilTime = 0.9f;
    public float telegraphScale = 1f;
}

[Serializable]
public class AttackTuning
{
    public string id;
    public bool enabled = true;
    [Tooltip("Out of 100 player health.")] public float damage = 10f;
    public float speed = 6f;
    public int count = 8;
    public float telegraph = 0.8f;
    public float duration = 2f;
    [Tooltip("Director budget cost: 1 small, 2 big.")] public int cost = 1;

    public AttackTuning Copy() => (AttackTuning)MemberwiseClone();
}
