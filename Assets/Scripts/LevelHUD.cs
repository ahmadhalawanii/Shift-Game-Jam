using UnityEngine;

// Minimal placeholder HUD: world, health bar, echo status, controls and a hint for the current stretch of the level.
public class LevelHUD : MonoBehaviour
{
    public static LevelHUD Instance { get; private set; }

    public Transform player;
    public MemoryAvatar avatar;
    public PlayerCombat combat;

    GUIStyle big, small;
    PlayerController pc;
    Texture2D ring;

    // Health bar: the red fill drops at once, a pale chip holds the old value for chipHold seconds then drains.
    // .linear: the project is in linear colour space, and IMGUI would otherwise show these washed out.
    static readonly Color HealthRed = new Color(0xE0 / 255f, 0x34 / 255f, 0x3A / 255f).linear;
    static readonly Color ChipColor = new Color(1f, 0.85f, 0.88f).linear;
    const float ChipHold = 0.4f, ChipDrain = 5f;
    float chip = 1f, lastHealth = 1f, chipDrainAt;

    void Update()
    {
        if (avatar == null) return;
        float h = avatar.health;
        if (h < lastHealth - 0.0001f) { chip = Mathf.Max(chip, lastHealth); chipDrainAt = Time.unscaledTime + ChipHold; }
        lastHealth = h;
        if (h >= chip) chip = h;
        else if (Time.unscaledTime >= chipDrainAt) chip = Mathf.Lerp(chip, h, 1f - Mathf.Exp(-ChipDrain * Time.unscaledDeltaTime));
    }

    void DrawHealthBar()
    {
        float h = avatar != null ? Mathf.Clamp01(avatar.health) : 1f;
        float w = Mathf.Min(320f, Screen.width * 0.4f), bh = 16f, x = 20f, y = Screen.height - 156f;
        GUI.Label(new Rect(x, y - 24f, 200f, 22f), "HEALTH  " + Mathf.RoundToInt(h * 100f), small);
        Color keep = GUI.color;
        GUI.color = new Color(0.003f, 0.002f, 0.004f, 0.85f);
        GUI.DrawTexture(new Rect(x - 2f, y - 2f, w + 4f, bh + 4f), Texture2D.whiteTexture);
        GUI.color = ChipColor;
        GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(chip), bh), Texture2D.whiteTexture);
        GUI.color = HealthRed;
        GUI.DrawTexture(new Rect(x, y, w * h, bh), Texture2D.whiteTexture);
        GUI.color = keep;
    }
    EclipseVisual bossVisual;

    // Square texture whose alpha is f(distance from centre, 0 at centre, 1 at the edge midpoints).
    static Texture2D Radial(int size, System.Func<float, float> alpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(Mathf.Sqrt(dx * dx + dy * dy))));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // Thin lock-on ring on the boss core. Smaller with distance, fainter while the boss is armoured.
    void DrawReticle(EclipseBoss boss)
    {
        var tpc = ThirdPersonCamera.Instance;
        var cam = Camera.main;
        if (tpc == null || tpc.LockWeight <= 0f || cam == null) return;
        if (bossVisual == null) bossVisual = boss.GetComponent<EclipseVisual>();
        Vector3 core = bossVisual != null && bossVisual.Core != null ? bossVisual.Core.position : boss.transform.position;
        Vector3 sp = cam.WorldToScreenPoint(core);
        if (sp.z <= 0f) return;
        if (ring == null) ring = Radial(64, d => 1f - Mathf.Abs(d - 0.85f) / 0.08f);
        float size = Mathf.Clamp(600f / sp.z, 22f, 64f) * Screen.height / 1080f;
        float visible = Mathf.Clamp01(boss.transform.localScale.x / 1.25f);
        Color keep = GUI.color;
        GUI.color = new Color(0.85f, 0.95f, 1f, tpc.LockWeight * visible * (boss.Sealed ? 0.4f : 0.9f));
        GUI.DrawTexture(new Rect(sp.x - size / 2f, Screen.height - sp.y - size / 2f, size, size), ring);
        GUI.color = keep;
    }

    void Awake() { Instance = this; }
    void Start()
    {
        if (EclipseEncounter.Instance == null) gameObject.AddComponent<EclipseEncounter>();
    }
    void OnDestroy() { if (Instance == this) Instance = null; }

    string Hint()
    {
        World w = WorldState.Instance != null ? WorldState.Instance.Current : World.Dream;
        return w == World.Nightmare ? "The Sleeper is exposed. Dodge ember orbs."
                                    : "Hold right-click to capture seeds. Chain absorbs for Resonance; at x5 press E for a Lucid Pulse.";
    }

    void OnGUI()
    {
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            big.normal.textColor = Color.white; small.normal.textColor = Color.white;
        }
        string world = WorldState.Instance != null ? WorldState.Instance.Current.ToString().ToUpper() : "";
        var encounter = EclipseEncounter.Instance;
        if (encounter != null)
        {
            var boss = encounter.Boss;
            if (boss != null) DrawReticle(boss);
            if (boss != null && boss.Engaged)
            {
                float width = Mathf.Min(600, Screen.width - 40);
                float x = (Screen.width - width) / 2f;
                GUI.Box(new Rect(x, 55, width, 14), "");
                Color saved = GUI.color;
                GUI.color = boss.Stunned ? new Color(0.5f, 1f, 1f) : new Color(0.8f, 0.35f, 0.9f);
                GUI.DrawTexture(new Rect(x + 2, 57, (width - 4) * Mathf.Clamp01(boss.health / boss.maxHealth), 10), Texture2D.whiteTexture);
                // Phase notches at two thirds and one third.
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(x + 2 + (width - 4) * (2f / 3f), 53, 2, 18), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x + 2 + (width - 4) / 3f, 53, 2, 18), Texture2D.whiteTexture);
                GUI.color = saved;
                string state = boss.BindingReady ? "BIND IN DREAM" : boss.Stunned ? "WAKE WINDOW" : boss.Sealed ? "SEALED" : "EXPOSED";
                GUI.Label(new Rect(x, 25, width, 28), $"THE SLEEPER   /   PHASE {boss.Phase}   /   {state}", big);
                if (boss.Charging) GUI.Label(new Rect(x, 72, width, 24), boss.AttackName, small);
            }
            if (encounter.Notice != null) GUI.Label(new Rect(20, Screen.height - 96, Screen.width - 40, 28), encounter.Notice, small);
            if (encounter.Result != null)
            {
                GUI.Box(new Rect(Screen.width/2f - 240, Screen.height/2f - 55, 480, 110), "");
                GUI.Label(new Rect(Screen.width/2f - 215, Screen.height/2f - 35, 450, 40), encounter.Result, big);
                GUI.Label(new Rect(Screen.width/2f - 215, Screen.height/2f + 10, 450, 30), encounter.BossFightStarted ? "Press R to retry the Sleeper" : "Press R to retry Level 1", small);
            }
        }
        GUI.Label(new Rect(20, 16, 500, 32), world, big);
        DrawHealthBar();
        if (combat != null) GUI.Label(new Rect(20, 70, 700, 24), "Power " + Mathf.RoundToInt(combat.Power) + " / " + Mathf.RoundToInt(combat.maxPower), small);
        if (combat != null && combat.IsChargingBurst)
            GUI.Label(new Rect(20, 100, 500, 24), $"BURST CHARGE  {combat.BurstCharge01:P0}", small);
        else if (combat != null && combat.Power >= combat.maxPower)
            GUI.Label(new Rect(20, 100, 500, 24), "POWER FULL — E in Nightmare to burst", small);
        var fightBoss = encounter != null ? encounter.Boss : null;
        if (fightBoss != null && fightBoss.Engaged)
        {
            if (combat != null && WorldState.CurrentOrDream == World.Dream)
                GUI.Label(new Rect(20, 124, 600, 24), combat.LucidPulseReady ? "RESONANCE x5  /  E: LUCID PULSE" : "Resonance x" + combat.ResonanceLevel, small);
            if (WorldState.CurrentOrDream == World.Nightmare)
                GUI.Label(new Rect(20, 124, 600, 24), "Damage x" + fightBoss.DamageMultiplier.ToString("F1"), small);
            GUI.Label(new Rect(20, 148, 600, 24), "Dread " + Mathf.RoundToInt(fightBoss.Dread * 100f) + "%", small);
            if (fightBoss.SwapWarning)
                GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height / 2f - 120, 400, 32), "SHIFTING TO " + fightBoss.SwapTarget.ToString().ToUpper(), big);
            // Red edges when hurt, violet edges as Dread rises.
            if (pc == null && player != null) pc = player.GetComponent<PlayerController>();
            float hurt = pc != null ? Mathf.Clamp01(1f - (Time.time - pc.LastHitTime) / 0.45f) : 0f;
            float edge = Mathf.Max(hurt * 0.55f, Mathf.Clamp01((fightBoss.Dread - 0.4f) / 0.6f) * 0.45f);
            if (edge > 0.01f)
            {
                Color keep = GUI.color;
                GUI.color = hurt > 0.01f ? new Color(1f, 0.1f, 0.1f, edge) : new Color(0.55f, 0.1f, 0.6f, edge);
                float t = 26f;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, t), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, Screen.height - t, Screen.width, t), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, 0, t, Screen.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(Screen.width - t, 0, t, Screen.height), Texture2D.whiteTexture);
                GUI.color = keep;
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GUI.Label(new Rect(20, Screen.height - 120, Screen.width - 40, 24), "DEBUG  1/2/3 phase   N swap   T teleport   C cycle attacks   K kill   H hitboxes   L camera lock", small);
        var tpc = ThirdPersonCamera.Instance;
        if (tpc != null && tpc.LockWeight > 0f) GUI.Label(new Rect(20, 172, 600, 24), tpc.LockDebug, small);
#endif
        GUI.Label(new Rect(20, Screen.height - 64, Screen.width - 40, 24), Hint(), small);
        GUI.Label(new Rect(20, Screen.height - 36, Screen.width - 40, 24), "WASD Move   Shift Dash   Space Jump   LMB Strike   RMB Charge   Q Shift World   E Weave   Tab Focus", small);
    }
}
