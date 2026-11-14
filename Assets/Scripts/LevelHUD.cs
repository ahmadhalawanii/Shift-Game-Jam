using UnityEngine;

// Minimal placeholder HUD: world, memory, echo status, controls and a hint for the current stretch of the level.
public class LevelHUD : MonoBehaviour
{
    public static LevelHUD Instance { get; private set; }

    public Transform player;
    public MemoryAvatar avatar;
    public PlayerCombat combat;

    GUIStyle big, small;

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
                                    : "Hold right-click to capture cyan seeds. E weaves the glowing anchor.";
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
                GUI.color = new Color(0.8f, 0.35f, 0.9f);
                GUI.DrawTexture(new Rect(x + 2, 57, (width - 4) * Mathf.Clamp01(boss.health / boss.maxHealth), 10), Texture2D.whiteTexture);
                GUI.color = saved;
                string state = boss.BindingReady ? "BIND IN DREAM" : boss.Sealed ? "SEALED" : "EXPOSED";
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
        GUI.Label(new Rect(20, Screen.height - 64, Screen.width - 40, 24), Hint(), small);
        GUI.Label(new Rect(20, Screen.height - 36, Screen.width - 40, 24), "WASD Move   Shift Dash   Space Jump   LMB Strike   RMB Charge   Q Shift World   E Weave   Tab Focus", small);
    }
}
