using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Loads combat prefabs from Resources/Combat.
public static class CombatAssets
{
    static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

    public static GameObject Get(string name)
    {
        GameObject g;
        if (!cache.TryGetValue(name, out g) || g == null)
        {
            g = Resources.Load<GameObject>("Combat/" + name);
            cache[name] = g;
        }
        return g;
    }

    public static GameObject Spawn(string name, Vector3 pos, Quaternion rot)
    {
        var g = Get(name);
        return g == null ? null : Object.Instantiate(g, pos, rot);
    }
}

// Particle bursts.
public static class Fx
{
    public static void Burst(Vector3 pos, Color color, int count)
    {
        var go = CombatAssets.Spawn("GlowBurst", pos, Quaternion.identity);
        if (go == null) return;
        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = color;
        var em = ps.emission;
        em.SetBurst(0, new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, 400)));
        ps.Play();
    }
}

// Hit-stop (brief freeze) used for impact feel.
public class GameFeel : MonoBehaviour
{
    static GameFeel inst;
    static GameFeel I
    {
        get
        {
            if (inst == null) { var g = new GameObject("GameFeel"); inst = g.AddComponent<GameFeel>(); }
            return inst;
        }
    }

    int stops;

    public static void HitStop(float seconds) { I.StartCoroutine(I.Stop(seconds)); }

    IEnumerator Stop(float seconds)
    {
        stops++;
        Time.timeScale = 0.04f;
        yield return new WaitForSecondsRealtime(seconds);
        stops--;
        if (stops <= 0) Time.timeScale = 1f;
    }

    void OnDestroy() { Time.timeScale = 1f; }
}
