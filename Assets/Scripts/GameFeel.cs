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
            if (g == null) Debug.LogError("Missing prefab Resources/Combat/" + name + ". Run menu Lucid > Build Combat And Player Props.");
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

// Particle bursts. One scene-owned world-space system emits every burst, so nothing is instantiated per hit.
public static class Fx
{
    static ParticleSystem system;
    public static void Burst(Vector3 pos, Color color, int count)
    {
        if (system == null)
        {
            var go = CombatAssets.Spawn("GlowBurst", pos, Quaternion.identity);
            if (go == null) return;
            system = go.GetComponent<ParticleSystem>();
            if (system == null) return;
            var main = system.main;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = 6000;
            main.loop = true;
            var em = system.emission;
            em.enabled = false;
            system.Play();
        }
        var p = new ParticleSystem.EmitParams { position = pos, startColor = color, applyShapeToPosition = true };
        system.Emit(p, Mathf.Clamp(count, 1, 400));
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
        if (stops <= 0) Time.timeScale = DreamcatcherPresentation.CoversHud ? 0f : 1f;
    }

    void OnDestroy() { Time.timeScale = 1f; }
}
