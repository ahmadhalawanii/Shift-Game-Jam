using UnityEngine;

// Debug target (F8): prints each weapon's burst and sustained DPS from the BalanceSheet, and shows the DPS
// actually dealt to it over the last 5 seconds.
public class DpsDummy : Enemy
{
    float windowStart, dealt, shown;

    public static void Spawn(Transform player, BalanceSheet sheet)
    {
        var text = new System.Text.StringBuilder("DPS (burst / sustained, before Dread and buffs)\n");
        foreach (var w in sheet.weapons)
        {
            float burst, sustained;
            if (w.kind == WeaponKind.Chain) { burst = w.damage; sustained = w.damage * w.heatSeconds / (w.heatSeconds + w.lockoutSeconds); }
            else
            {
                float shot = w.damage * Mathf.Max(1, w.pellets);
                burst = shot * w.shotsPerSecond;
                sustained = shot * w.magazine / (w.magazine / w.shotsPerSecond + w.reloadTime);
            }
            text.AppendFormat("{0}: {1:F1} / {2:F1}\n", w.name, burst, sustained);
        }
        Debug.Log(text.ToString());
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(go.GetComponent<Collider>());
        go.name = "DPS dummy";
        go.transform.position = player.position + player.forward * 7f + Vector3.up;
        var d = go.AddComponent<DpsDummy>();
        d.health = 1e9f; d.radius = 0.8f;
    }

    public override void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (Time.time - windowStart > 5f) { shown = dealt / 5f; windowStart = Time.time; dealt = 0f; }
        dealt += damage;
        Fx.Burst(hitPoint, Color.white, 4);
    }

    void OnGUI()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 s = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.4f);
        if (s.z > 0f) GUI.Label(new Rect(s.x - 80f, Screen.height - s.y, 200f, 24f), $"last 5 s: {shown:F1} dps  (now {dealt:F0})");
    }
}
