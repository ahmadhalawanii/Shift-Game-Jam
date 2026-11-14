using UnityEngine;

// A combat zone: walking in wakes it, enemies arrive in waves, and the exit opens when the last wave is gone.
public class CombatGate : MonoBehaviour
{
    public float radius = 12f;
    public GameObject[] waves;
    public ThornGate exit;
    public bool wakeNightmare = true;
    public float waveDelay = 0.8f;

    int wave = -1;
    bool done;
    float nextCheck;

    void Start()
    {
        if (waves != null) foreach (var w in waves) if (w != null) w.SetActive(false);
    }

    void Update()
    {
        if (done) return;
        var player = LevelFlow.Player;
        if (player == null) return;
        var encounter = EclipseEncounter.Instance;
        if (wave < 0)
        {
            Vector3 d = player.transform.position - transform.position;
            if (Mathf.Abs(d.y) > 4f) return;
            d.y = 0f;
            if (d.sqrMagnitude > radius * radius) return;
            if (encounter != null) encounter.Tell("THE GLOAM WAKES. Clear the hollow to open the way.", 4f);
            if (wakeNightmare && WorldState.Instance != null && WorldState.Instance.Current == World.Dream)
            {
                WorldShift.NextOrigin = transform.position + Vector3.up;
                WorldState.Instance.Set(World.Nightmare);
            }
            Begin(0);
            return;
        }
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.25f;
        if (waves[wave] != null && waves[wave].GetComponentsInChildren<Enemy>().Length > 0) return;
        if (wave + 1 < waves.Length) { Begin(wave + 1); return; }
        done = true;
        if (exit != null) exit.Open();
        if (encounter != null) encounter.Tell("The hollow is quiet. The way is open.", 3.5f);
    }

    void Begin(int index)
    {
        wave = index;
        nextCheck = Time.time + waveDelay;
        if (waves == null || index >= waves.Length || waves[index] == null) return;
        waves[index].SetActive(true);
        foreach (var e in waves[index].GetComponentsInChildren<Enemy>())
            Fx.Burst(e.transform.position, new Color(0.6f, 0.3f, 0.9f), 24);
    }
}
