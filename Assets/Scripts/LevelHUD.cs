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
    void OnDestroy() { if (Instance == this) Instance = null; }

    string Hint()
    {
        World w = WorldState.Instance != null ? WorldState.Instance.Current : World.Dream;
        return w == World.Nightmare ? "NIGHTMARE: hold left mouse to fire. Dash (Space) to dodge. Shift to return to Dream."
                                    : "DREAM: hold left mouse to shield and absorb orbs. Shift to enter Nightmare and shoot.";
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
        int mem = avatar != null ? Mathf.RoundToInt(avatar.memory * 100f) : 100;
        GUI.Label(new Rect(20, 16, 500, 32), world, big);
        GUI.Label(new Rect(20, 46, 500, 24), "Memory " + mem + "%", small);
        if (combat != null) GUI.Label(new Rect(20, 70, 700, 24), "Power " + Mathf.RoundToInt(combat.Power) + " / " + Mathf.RoundToInt(combat.maxPower) + "     Enemies " + Enemy.All.Count, small);
        GUI.Label(new Rect(20, Screen.height - 64, Screen.width - 40, 24), Hint(), small);
        GUI.Label(new Rect(20, Screen.height - 36, Screen.width - 40, 24), "WASD move   Space dash   Left mouse shield (Dream) / shoot (Nightmare)   Shift or right mouse swap world", small);
    }
}
