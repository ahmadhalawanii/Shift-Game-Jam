using UnityEngine;

// A thin expanding ring keeps the action readable during burst and perfect-shift feedback.
public class CombatPulse : MonoBehaviour
{
    LineRenderer line;
    Material material;
    float age;
    float radius;
    Color color;
    public static void Spawn(Vector3 center, float radius, Color color)
    {
        var pulse = new GameObject("Combat pulse").AddComponent<CombatPulse>();
        pulse.transform.position = center;
        pulse.radius = radius; pulse.color = color;
        var energy = Resources.Load<Shader>("Combat/HazardEnergy");
        pulse.material = new Material(energy != null ? energy : Shader.Find("Universal Render Pipeline/Unlit"));
        pulse.line = pulse.gameObject.AddComponent<LineRenderer>();
        pulse.line.sharedMaterial = pulse.material;
        pulse.line.useWorldSpace = false; pulse.line.loop = true;
        pulse.line.positionCount = 96;
    }
    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / 0.65f);
        float r = radius * (1f - Mathf.Pow(1f - t, 3f));
        material.SetColor("_BaseColor", color * (1f - t));
        material.SetColor("_Color", new Color(color.r, color.g, color.b, 1f - t));
        line.widthMultiplier = Mathf.Lerp(0.35f, 0.01f, t);
        for (int i = 0; i < 96; i++)
        {
            float a = i * Mathf.PI * 2f / 96f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
        }
        if (t >= 1f) Destroy(gameObject);
    }
    void OnDestroy() { if (material != null) Destroy(material); }
}
