using UnityEngine;

// Minimal placeholder HUD: world, memory, echo status, controls and a hint for the current stretch of the level.
public class LevelHUD : MonoBehaviour
{
    public static LevelHUD Instance { get; private set; }

    public Transform player;
    public MemoryAvatar avatar;
    public PlayerCombat combat;

    GUIStyle big, small;
    PlayerController pc;

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
        int mem = avatar != null ? Mathf.RoundToInt(avatar.memory * 100f) : 100;
        GUI.Label(new Rect(20, 16, 500, 32), world, big);
        GUI.Label(new Rect(20, 46, 500, 24), "Memory " + mem + "%", small);
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
        GUI.Label(new Rect(20, Screen.height - 120, Screen.width - 40, 24), "DEBUG  1/2/3 phase   N swap   T teleport   C cycle attacks   K kill   H hitboxes", small);
#endif
        GUI.Label(new Rect(20, Screen.height - 64, Screen.width - 40, 24), Hint(), small);
        GUI.Label(new Rect(20, Screen.height - 36, Screen.width - 40, 24), "WASD Move   Shift Dash   Space Jump   LMB Strike   RMB Charge   Q Shift World   E Weave   Tab Focus", small);
    }
}
