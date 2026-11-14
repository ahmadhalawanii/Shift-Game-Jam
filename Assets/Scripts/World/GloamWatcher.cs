using UnityEngine;

// Gloam Watcher. Dream: a dormant statue that cannot be hurt. Nightmare: its eye opens and a thin beam
// tracks the player, locks, flashes, then fires along the locked line. It can only be destroyed in Nightmare.
public class GloamWatcher : Enemy
{
    public float range = 30f;
    public float aimTime = 1.1f;
    public float lockTime = 0.5f;     // the beam stops following: this is the dodge window
    public float fireTime = 0.35f;
    public float cooldown = 1.8f;
    public float turnRate = 75f;      // degrees per second while aiming
    public float damage = 0.08f;
    public float beamLength = 34f;
    public Transform eye;
    public Renderer eyeGlow;
    public LineRenderer beam;

    enum State { Dormant, Wait, Aim, Lock, Fire }
    State state = State.Dormant;
    float timer;
    Vector3 aim = Vector3.forward;
    bool hit;
    PlayerController pc;
    Vector3 eyeScale = Vector3.one;

    protected override void Start()
    {
        base.Start();
        if (player != null) pc = player.GetComponent<PlayerController>();
        if (eye != null) eyeScale = eye.localScale;
        if (beam != null) beam.enabled = false;
    }

    public override void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (WorldShift.WorldAt(transform.position) != World.Nightmare)
        {
            Fx.Burst(hitPoint, new Color(0.6f, 0.65f, 0.8f), 3); // stone: nothing happens
            return;
        }
        base.TakeDamage(amount, hitPoint);
    }

    void Update()
    {
        bool awake = WorldShift.WorldAt(transform.position) == World.Nightmare;
        if (eyeGlow != null) eyeGlow.enabled = awake;
        if (!awake || player == null)
        {
            state = State.Dormant;
            if (beam != null) beam.enabled = false;
            return;
        }
        if (state == State.Dormant) { state = State.Wait; timer = 0.6f; }

        Vector3 origin = eye != null ? eye.position : transform.position + Vector3.up * 2f;
        Vector3 chest = player.position + Vector3.up;
        Vector3 to = chest - origin;
        float d = to.magnitude;
        timer -= Time.deltaTime;

        switch (state)
        {
            case State.Wait:
                if (timer <= 0f && d < range) { state = State.Aim; timer = aimTime; aim = to.normalized; }
                break;
            case State.Aim:
                aim = Vector3.RotateTowards(aim, to.normalized, turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
                Show(origin, 0.06f, new Color(1f, 0.55f, 0.3f, 0.7f));
                if (timer <= 0f) { state = State.Lock; timer = lockTime; LucidAudio.Play(LucidAudio.Cue.Warning); }
                break;
            case State.Lock:
                Show(origin, 0.1f + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 30f)), new Color(1f, 0.8f, 0.4f, 1f));
                if (timer <= 0f) { state = State.Fire; timer = fireTime; hit = false; ThirdPersonCamera.Shake(0.12f); }
                break;
            case State.Fire:
                Show(origin, 0.9f, new Color(1f, 0.4f, 0.2f, 1f));
                if (!hit && pc != null)
                {
                    float along = Mathf.Clamp(Vector3.Dot(chest - origin, aim), 0f, beamLength);
                    if ((origin + aim * along - chest).sqrMagnitude < 0.85f * 0.85f) { hit = true; pc.Damage(damage); }
                }
                if (timer <= 0f) { state = State.Wait; timer = cooldown; if (beam != null) beam.enabled = false; }
                break;
        }
        if (eye != null)
        {
            eye.rotation = Quaternion.Slerp(eye.rotation, Quaternion.LookRotation(aim), 12f * Time.deltaTime);
            eye.localScale = eyeScale * (state == State.Lock ? 1.35f : state == State.Fire ? 1.6f : 1f);
        }
    }

    void Show(Vector3 origin, float width, Color color)
    {
        if (beam == null) return;
        beam.enabled = true;
        beam.SetPosition(0, origin);
        beam.SetPosition(1, origin + aim * beamLength);
        beam.widthMultiplier = width;
        beam.startColor = color;
        beam.endColor = new Color(color.r, color.g, color.b, 0f);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (beam != null) beam.enabled = false;
    }
}
