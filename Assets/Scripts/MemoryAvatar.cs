using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The player as a fading memory: a figure of drifting light with RGB-split afterimages.
// memory (0..1) is the player's health: lower memory means more of the figure has dissolved.
// The body is animated in the shader from the parameters set here (stride, lean, aim, shield, dash, flinch).
public class MemoryAvatar : MonoBehaviour
{
    [Range(0f, 1f)] public float memory = 1f;
    public Renderer[] ghosts;               // 0 = the figure, 1 and 2 = delayed afterimages
    public float[] delays = { 0f, 0.12f, 0.24f };
    public float[] alphas = { 0.5f, 0.22f, 0.18f };
    public Color[] tints = { Color.white, new Color(1f, 0.3f, 0.4f), new Color(0.3f, 0.8f, 1f) };
    public Light glow;

    [Header("Look")]
    public Color feetDream = new Color(1f, 0.72f, 0.45f);
    public Color headDream = new Color(1f, 0.55f, 0.82f);
    public Color feetNightmare = new Color(1f, 0.23f, 0.06f);
    public Color headNightmare = new Color(0.48f, 0.06f, 0.03f);
    public float bobHeight = 0.05f;

    [Header("Animation")]
    public float strideRate = 1.4f;     // walk cycle radians per unit travelled
    public float leanAmount = 0.35f;    // radians of forward lean at full speed

    struct Sample { public float t; public Vector3 pos; public float yaw; }

    readonly List<Sample> history = new List<Sample>();
    MaterialPropertyBlock block;
    CharacterController cc;
    PlayerController player;
    PlayerCombat combat;
    float scatterUntil;
    float scatterLength = 0.01f;
    float yaw;
    float sharpenUntil;

    // Smoothed animation state.
    float walkPhase, speed01, lean, aim, shield, stretch, hit;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        cc = GetComponentInParent<CharacterController>();
        player = GetComponentInParent<PlayerController>();
        combat = GetComponentInParent<PlayerCombat>();
    }

    void OnEnable() { WorldState.Changed += OnWorldChanged; }
    void OnDisable() { WorldState.Changed -= OnWorldChanged; }

    // The figure bursts and reforms when the world flips.
    void OnWorldChanged(World w)
    {
        Scatter(0.3f);
        ThirdPersonCamera.Shake(0.15f);
    }

    // Dash: the figure bursts into particles and reforms.
    public void Scatter(float seconds)
    {
        scatterLength = Mathf.Max(0.01f, seconds);
        scatterUntil = Time.time + scatterLength;
    }

    // Brief snap back into focus (for a perfect absorb).
    public void Sharpen() { sharpenUntil = Time.time + 0.35f; }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftBracketKey.isPressed) memory = Mathf.Max(0.05f, memory - 0.4f * Time.deltaTime);
            if (kb.rightBracketKey.isPressed) memory = Mathf.Min(1f, memory + 0.4f * Time.deltaTime);
        }

        float dt = Time.deltaTime;
        float t = Time.time;
        Vector3 v = cc != null ? cc.velocity : Vector3.zero;
        v.y = 0f;
        float speed = v.magnitude;
        if (speed > 0.5f && combat != null && !combat.Firing && !combat.ShieldUp) yaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
        else if (player != null) yaw = player.transform.eulerAngles.y;

        // Animation targets.
        float maxSp = player != null ? player.maxSpeed : 8f;
        float sp = Mathf.Clamp01(speed / maxSp);
        bool dashing = player != null && player.IsDashing;
        walkPhase += speed * strideRate * dt;
        speed01 = Mathf.MoveTowards(speed01, sp, 6f * dt);
        lean = Mathf.MoveTowards(lean, sp * leanAmount + (dashing ? 0.25f : 0f), 3f * dt);
        aim = Mathf.MoveTowards(aim, (combat != null && combat.Firing) ? 1f : 0f, 8f * dt);
        shield = Mathf.MoveTowards(shield, (combat != null && combat.ShieldUp) ? 1f : 0f, 8f * dt);
        stretch = Mathf.MoveTowards(stretch, dashing ? 1f : 0f, 12f * dt);
        float sinceHit = player != null ? Time.time - player.LastHitTime : 99f;
        hit = Mathf.Clamp01(1f - sinceHit / 0.3f);
        float kick = combat != null ? combat.Kick : 0f;

        Vector3 basePos = transform.position + Vector3.up * (0.05f + Mathf.Sin(t * 2f) * bobHeight * (1f - speed01));
        history.Add(new Sample { t = t, pos = basePos, yaw = yaw });
        while (history.Count > 240) history.RemoveAt(0);

        float mix = WorldState.Instance != null ? WorldState.Instance.Mix : 0f;
        float scatter = Time.time < scatterUntil ? Mathf.Sin((scatterUntil - Time.time) / scatterLength * Mathf.PI) : 0f;
        float sharp = Time.time < sharpenUntil ? 0.15f : 0f;
        float strength = Mathf.Clamp01(memory + sharp - scatter * 0.9f) * 1.02f;

        for (int i = 0; i < ghosts.Length; i++)
        {
            var s = SampleAt(t - delays[i]);
            ghosts[i].transform.position = s.pos;
            ghosts[i].transform.rotation = Quaternion.Euler(0, s.yaw, 0);

            ghosts[i].GetPropertyBlock(block);
            block.SetFloat("_Strength", strength);
            block.SetFloat("_Mix", mix);
            block.SetColor("_Col1", Color.Lerp(feetDream, feetNightmare, mix));
            block.SetColor("_Col2", Color.Lerp(headDream, headNightmare, mix));
            block.SetColor("_Tint", tints[i]);
            block.SetFloat("_Alpha", alphas[i]);
            block.SetFloat("_DarkAlpha", i == 0 ? 0.35f : 0f);
            block.SetFloat("_Walk", walkPhase - delays[i] * speed * strideRate);
            block.SetFloat("_Speed01", speed01);
            block.SetFloat("_Lean", lean);
            block.SetFloat("_Aim", aim);
            block.SetFloat("_Shield", shield);
            block.SetFloat("_Stretch", stretch);
            block.SetFloat("_Hit", hit);
            block.SetFloat("_Kick", kick);
            ghosts[i].SetPropertyBlock(block);
        }

        if (glow != null)
        {
            glow.color = Color.Lerp(new Color(1f, 0.8f, 0.65f), new Color(1f, 0.3f, 0.12f), mix);
            glow.intensity = Mathf.Lerp(0.4f, 1.6f, memory) * (1f - scatter * 0.7f) * (1f + aim * 0.8f + shield * 0.6f);
        }
    }

    Sample SampleAt(float time)
    {
        for (int i = history.Count - 1; i >= 0; i--)
            if (history[i].t <= time) return history[i];
        return history[0];
    }
}
