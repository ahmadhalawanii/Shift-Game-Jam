using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The smoke dash effect. PlayerController owns the move; this turns the body into smoke, streams it along the
// path in three twisting strands around a bright core, and condenses it back on arrival.
// Dream: pearly mist with pink and gold motes. Nightmare: black ink with embers, and the cloud left behind burns small enemies.
// Everything is emitted by hand into two pooled particle systems, so a dash allocates nothing per frame.
public class SmokeDash : MonoBehaviour
{
    [Header("Stream")]
    public float stepLength = 0.14f;      // metres between emitted slices
    public float strandRadius = 0.3f;
    public float twist = 4.2f;            // radians per metre
    public int maxSmoke = 420;
    public float fovKick = 7f;
    [Range(0f, 1f)] public float edgeBlur = 1f;

    [Header("Cloud left behind")]
    public float cloudSeconds = 1f;
    public float cloudRadius = 2.2f;
    public float cloudDamagePerSecond = 8f;   // Nightmare only, never the boss

    [Header("Colours")]
    public Color dreamSmokeA = new Color(0.97f, 0.93f, 1f, 0.72f), dreamSmokeB = new Color(1f, 0.78f, 0.9f, 0.66f);
    public Color nightSmokeA = new Color(0.02f, 0.015f, 0.03f, 0.94f), nightSmokeB = new Color(0.11f, 0.03f, 0.02f, 0.9f);
    public Color dreamCore = new Color(1f, 0.82f, 0.72f), nightCore = new Color(1f, 0.36f, 0.08f);
    public Color dreamMoteA = new Color(1f, 0.86f, 0.45f), dreamMoteB = new Color(1f, 0.5f, 0.8f);
    public Color nightEmber = new Color(1f, 0.42f, 0.08f);

    PlayerController pc;
    MemoryAvatar avatar;
    ParticleSystem smoke, glow;
    Volume blur;
    VolumeProfile blurProfile;
    bool dashing, nightmare, cloudBurns;
    Vector3 last, cloudAt;
    float travelled, cloudUntil, nextCloudTick;

    Vector3 Chest { get { return transform.position + Vector3.up; } }

    void Start()
    {
        pc = GetComponent<PlayerController>();
        avatar = GetComponentInChildren<MemoryAvatar>();
        var smokeMat = Resources.Load<Material>("Combat/SmokePuff");
        var burst = CombatAssets.Get("GlowBurst");
        var glowMat = burst != null ? burst.GetComponent<ParticleSystemRenderer>().sharedMaterial : null;
        if (pc == null || smokeMat == null || glowMat == null)
        {
            Debug.LogError("SmokeDash: missing Resources/Combat/SmokePuff or GlowBurst. Run menu Lucid > Build Combat And Player Props.");
            enabled = false;
            return;
        }
        smoke = Build("Dash smoke", smokeMat, maxSmoke, 1.3f, 2.2f, 0.6f, 1.4f);
        smoke.GetComponent<ParticleSystemRenderer>().SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.StableRandomX, ParticleSystemVertexStream.AgePercent });
        glow = Build("Dash glow", glowMat, 500, 0.8f, 1.2f, 1f, 0.2f);

        // Edge blur while in smoke form: a global volume faded in and out.
        var go = new GameObject("Dash blur");
        go.transform.SetParent(transform, false);
        blur = go.AddComponent<Volume>();
        blur.isGlobal = true;
        blur.priority = 50f;
        blur.weight = 0f;
        blurProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        blurProfile.Add<LensDistortion>(true).intensity.value = -0.38f;
        blurProfile.Add<ChromaticAberration>(true).intensity.value = 0.8f;
        blur.sharedProfile = blurProfile;

        pc.DashStarted += Begin;
        pc.DashEnded += End;
    }

    void OnDestroy()
    {
        if (pc != null) { pc.DashStarted -= Begin; pc.DashEnded -= End; }
        if (blurProfile != null) Destroy(blurProfile);
    }

    ParticleSystem Build(string name, Material material, int max, float noise, float drag, float sizeStart, float sizeEnd)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.startSpeed = 0f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        // The noise module is curl noise: it gives the smoke its swirl.
        var n = ps.noise;
        n.enabled = true;
        n.strength = noise;
        n.frequency = 0.6f;
        n.scrollSpeed = 0.9f;
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 100f;
        limit.drag = drag;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, sizeStart, 1f, sizeEnd));
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.55f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    void Emit(ParticleSystem ps, Vector3 pos, Vector3 velocity, float size, float life, Color color)
    {
        var p = new ParticleSystem.EmitParams { position = pos, velocity = velocity, startSize = size, startLifetime = life, startColor = color };
        ps.Emit(p, 1);
    }

    void Puff(Vector3 pos, Vector3 velocity, float size, float life, float alpha = 1f)
    {
        Color c = nightmare ? Color.Lerp(nightSmokeA, nightSmokeB, Random.value) : Color.Lerp(dreamSmokeA, dreamSmokeB, Random.value);
        c.a *= alpha;
        Emit(smoke, pos, velocity, size, life, c);
    }

    void Mote(Vector3 pos, Vector3 velocity)
    {
        Color c = nightmare ? nightEmber : (Random.value < 0.5f ? dreamMoteA : dreamMoteB);
        // Dream motes drift up, embers fall.
        Emit(glow, pos, velocity + Vector3.up * (nightmare ? -0.6f : 0.8f), Random.Range(0.05f, 0.12f), Random.Range(0.45f, 0.9f), c);
    }

    void Begin()
    {
        dashing = true;
        nightmare = WorldState.CurrentOrDream == World.Nightmare;
        last = Chest;
        travelled = 0f;
        if (avatar != null) avatar.SetSmoke(true);
        Vector3 dir = pc.DashDirection;
        // The body turns to smoke and is pulled into the stream.
        for (int i = 0; i < 26; i++)
        {
            Vector3 p = transform.position + transform.rotation * new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 1.7f), Random.Range(-0.25f, 0.25f));
            Puff(p, dir * Random.Range(4f, 9f) + Random.insideUnitSphere * 1.5f, Random.Range(0.5f, 0.9f), Random.Range(0.25f, 0.45f));
            if (i < 16) Mote(p, Random.insideUnitSphere * 2.5f + dir * 2f);
        }
        // What the dash leaves behind: healing mist in Dream, a burning cloud in Nightmare.
        cloudAt = Chest;
        cloudUntil = Time.time + cloudSeconds;
        cloudBurns = nightmare;
        nextCloudTick = Time.time;
        for (int i = 0; i < 9; i++)
            Puff(cloudAt + Random.insideUnitSphere * 0.9f, Random.insideUnitSphere * 0.5f, Random.Range(1.3f, 1.9f), cloudSeconds, 0.55f);
        ThirdPersonCamera.FovPunch(fovKick);
        LucidAudio.Play(nightmare ? LucidAudio.Cue.DashNightmare : LucidAudio.Cue.DashDream);
    }

    void End()
    {
        dashing = false;
        if (avatar != null) avatar.SetSmoke(false);
        Vector3 c = Chest;
        // The smoke condenses back onto the body, with a pulse ring and a flash.
        for (int i = 0; i < 20; i++)
        {
            Vector3 d = Random.onUnitSphere;
            d.y *= 0.6f;
            Puff(c + d * 1.25f, -d * 5.5f, Random.Range(0.4f, 0.7f), 0.22f);
        }
        Color ring = nightmare ? new Color(1f, 0.35f, 0.1f) : new Color(1f, 0.75f, 0.9f);
        CombatPulse.Spawn(transform.position + Vector3.up * 0.1f, 2.6f, ring);
        Fx.Burst(c, nightmare ? nightEmber : dreamMoteA, 26);
        ThirdPersonCamera.Shake(0.05f);
    }

    void LateUpdate()
    {
        if (dashing)
        {
            Vector3 now = Chest, dir = pc.DashDirection;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            float length = (now - last).magnitude;
            int steps = Mathf.Min(12, Mathf.CeilToInt(length / Mathf.Max(0.05f, stepLength)));
            Color core = nightmare ? nightCore : dreamCore;
            for (int s = 1; s <= steps; s++)
            {
                Vector3 p = Vector3.Lerp(last, now, s / (float)steps);
                travelled += length / steps;
                for (int k = 0; k < 3; k++)
                {
                    float a = travelled * twist + k * 2.0944f;
                    float sin = Mathf.Sin(a), cos = Mathf.Cos(a);
                    Vector3 off = right * cos + Vector3.up * sin;
                    Vector3 swirl = Vector3.up * cos - right * sin;
                    Puff(p + off * strandRadius, swirl * 2.2f - dir * 1.2f + Random.insideUnitSphere * 0.4f, Random.Range(0.5f, 0.85f), Random.Range(0.4f, 0.7f));
                }
                Emit(glow, p, Vector3.zero, 0.6f, 0.16f, core);
                Mote(p + Random.insideUnitSphere * 0.45f, Random.insideUnitSphere * 2f);
            }
            last = now;
        }
        if (blur != null)
            blur.weight = Mathf.MoveTowards(blur.weight, dashing ? edgeBlur : 0f, (dashing ? 14f : 5f) * Time.unscaledDeltaTime);

        if (cloudBurns && Time.time < cloudUntil && Time.time >= nextCloudTick)
        {
            nextCloudTick = Time.time + 0.25f;
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                var e = Enemy.All[i];
                if (e == null || e is EclipseBoss) continue;
                float reach = cloudRadius + e.radius;
                if ((e.transform.position - cloudAt).sqrMagnitude < reach * reach) e.TakeDamage(cloudDamagePerSecond * 0.25f, e.transform.position);
            }
        }
    }
}
