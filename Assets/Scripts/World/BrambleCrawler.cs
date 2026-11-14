using UnityEngine;

// Bramble Crawler. Dream: asleep, curled up. Nightmare: it rears back for a moment (a line on the ground
// shows the path), then charges in a straight line. Step aside or dash through.
public class BrambleCrawler : Enemy
{
    public float wakeDistance = 17f;
    public float telegraph = 0.6f;
    public float chargeSpeed = 17f;
    public float chargeTime = 0.7f;
    public float recover = 1.2f;
    public float damage = 0.09f;
    public float leash = 10f;
    public Transform body;
    public Renderer[] eyes;
    public LineRenderer line;

    enum State { Asleep, Idle, Tell, Charge, Recover }
    State state = State.Asleep;
    Vector3 home, chargeDir;
    float timer;
    float curl = 1f;
    bool hit;
    PlayerController pc;

    protected override void Start()
    {
        base.Start();
        home = transform.position;
        if (player != null) pc = player.GetComponent<PlayerController>();
        if (line != null) line.enabled = false;
    }

    void Update()
    {
        World w = WorldShift.WorldAt(transform.position);
        bool awake = w == World.Nightmare;
        curl = Mathf.MoveTowards(curl, awake ? 0f : 1f, Time.deltaTime * 3f);
        float squash = state == State.Tell ? 0.25f * (1f - timer / telegraph) : 0f;
        if (body != null) body.localScale = new Vector3(1f + curl * 0.1f + squash, 1f - curl * 0.42f - squash * 0.6f, 1f + curl * 0.1f - squash);
        if (eyes != null) for (int i = 0; i < eyes.Length; i++) if (eyes[i] != null) eyes[i].enabled = curl < 0.5f;
        if (line != null) line.enabled = state == State.Tell;

        if (!awake) { state = State.Asleep; return; }
        if (state == State.Asleep) { state = State.Idle; timer = 0.5f; }
        if (player == null) return;

        Vector3 to = player.position - transform.position;
        float height = to.y;
        to.y = 0f;
        float d = to.magnitude;
        Vector3 dir = d > 0.01f ? to / d : transform.forward;
        timer -= Time.deltaTime;

        switch (state)
        {
            case State.Idle:
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 5f * Time.deltaTime);
                if (timer <= 0f && d < wakeDistance && Mathf.Abs(height) < 3f) { state = State.Tell; timer = telegraph; LucidAudio.Play(LucidAudio.Cue.Warning); }
                break;
            case State.Tell:
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 9f * Time.deltaTime);
                chargeDir = transform.forward;
                if (line != null)
                {
                    line.SetPosition(0, transform.position + Vector3.up * 0.15f);
                    line.SetPosition(1, transform.position + Vector3.up * 0.15f + chargeDir * chargeSpeed * chargeTime);
                    line.widthMultiplier = Mathf.Lerp(0.15f, 0.6f, 1f - timer / telegraph);
                }
                if (timer <= 0f) { state = State.Charge; timer = chargeTime; hit = false; }
                break;
            case State.Charge:
                Vector3 p = transform.position + chargeDir * chargeSpeed * Time.deltaTime;
                Vector3 fromHome = p - home; fromHome.y = 0f;
                if (fromHome.magnitude > leash) { p = home + fromHome.normalized * leash; timer = 0f; }
                p.y = home.y;
                transform.position = p;
                if (!hit && pc != null && d < radius + 0.7f && Mathf.Abs(height) < 1.6f)
                {
                    hit = true;
                    pc.Damage(damage);
                }
                if (timer <= 0f) { state = State.Recover; timer = recover; }
                break;
            case State.Recover:
                if (timer <= 0f) { state = State.Idle; timer = 0.3f; }
                break;
        }
    }
}
