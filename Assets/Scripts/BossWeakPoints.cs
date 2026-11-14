using UnityEngine;

// Three glowing sigils that orbit the boss. The Lumen Bow tags them in Dream; a Nightmare shot that hits a
// tagged one cashes it in. Shots are tested on the horizontal plane, so a sigil is hit when the shot meets
// the boss body close to where the sigil is.
public class BossWeakPoints : MonoBehaviour
{
    public const int Count = 3;
    EclipseBoss boss;
    EclipseVisual visual;
    BalanceSheet sheet;
    float angle;
    readonly float[] taggedUntil = new float[Count], lockoutUntil = new float[Count];
    readonly LineRenderer[] rings = new LineRenderer[Count], marks = new LineRenderer[Count];
    readonly Material[] mats = new Material[Count];
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public int TagCount { get { int n = 0; for (int i = 0; i < Count; i++) if (Tagged(i)) n++; return n; } }
    public bool Tagged(int i) { return Time.time < taggedUntil[i]; }
    public float TagLeft01(int i) { return Mathf.Clamp01((taggedUntil[i] - Time.time) / Mathf.Max(0.1f, sheet.tagSeconds)); }

    public Vector3 Position(int i)
    {
        float a = (angle + i * 360f / Count) * Mathf.Deg2Rad;
        Vector3 c = boss.transform.position;
        float y = visual != null && visual.Core != null ? visual.Core.position.y : c.y;
        return new Vector3(c.x + Mathf.Sin(a) * boss.radius, y, c.z + Mathf.Cos(a) * boss.radius);
    }

    // Index of the weak point at this hit point on the boss body, or -1.
    public int Hit(Vector3 point)
    {
        for (int i = 0; i < Count; i++)
        {
            Vector3 d = Position(i) - point;
            d.y = 0f;
            if (d.sqrMagnitude < sheet.weakPointSize * sheet.weakPointSize) return i;
        }
        return -1;
    }

    public bool Tag(int i)
    {
        if (Tagged(i) || TagCount >= sheet.maxTags || Time.time < lockoutUntil[i]) return false;
        taggedUntil[i] = Time.time + sheet.tagSeconds;
        lockoutUntil[i] = Time.time + sheet.retagLockout;
        return true;
    }

    public bool CashIn(int i)
    {
        if (!Tagged(i)) return false;
        taggedUntil[i] = 0f;
        return true;
    }

    void Start()
    {
        boss = GetComponent<EclipseBoss>();
        visual = GetComponent<EclipseVisual>();
        sheet = BalanceSheet.Load();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        for (int i = 0; i < Count; i++)
        {
            mats[i] = new Material(shader);
            rings[i] = Loop("Weak point " + i, mats[i], 24, 0.55f, null);
            marks[i] = Loop("Mark", mats[i], 4, 0.3f, rings[i].transform);
        }
    }

    LineRenderer Loop(string name, Material mat, int points, float radius, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var l = go.AddComponent<LineRenderer>();
        l.sharedMaterial = mat;
        l.useWorldSpace = false; l.loop = true; l.positionCount = points; l.widthMultiplier = 0.06f;
        l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 0; i < points; i++)
        {
            float a = i * Mathf.PI * 2f / points;
            l.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
        return l;
    }

    void LateUpdate()
    {
        if (boss == null) return;
        angle += sheet.weakPointOrbitDegrees * Time.deltaTime;
        bool show = boss.Engaged && !EclipseEncounter.Finished && boss.transform.localScale.x > 0.6f;
        var cam = Camera.main;
        for (int i = 0; i < Count; i++)
        {
            rings[i].enabled = marks[i].enabled = show;
            if (!show) continue;
            var t = rings[i].transform;
            t.position = Position(i);
            if (cam != null) t.rotation = Quaternion.LookRotation(t.position - cam.transform.position);
            bool tagged = Tagged(i);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * (tagged ? 9f : 2f) + i);
            marks[i].transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * (tagged ? 160f : 30f));
            t.localScale = Vector3.one * (tagged ? 1.5f : 1f);
            mats[i].SetColor(BaseColor, tagged ? new Color(3f, 2.2f, 0.9f) * pulse : new Color(0.45f, 0.6f, 0.9f) * pulse);
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < Count; i++)
        {
            if (rings[i] != null) Destroy(rings[i].gameObject);
            if (mats[i] != null) Destroy(mats[i]);
        }
    }
}
