using UnityEngine;

// A Dream blossom that throws the player upward. In Nightmare it folds into a bud and is just a small step.
public class BounceBlossom : MonoBehaviour
{
    public float launchSpeed = 18f;   // about 6.5 m of height
    public float radius = 1.5f;
    public Transform visual;

    PlayerController player;
    float squash;
    float readyAt;

    void Update()
    {
        if (player == null) { player = LevelFlow.Player; if (player == null) return; }
        if (visual != null)
        {
            squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime * 3.5f);
            float s = Mathf.Sin(squash * Mathf.PI);
            visual.localScale = new Vector3(1f + s * 0.25f, 1f - s * 0.45f, 1f + s * 0.25f);
        }
        if (Time.time < readyAt) return;
        if (WorldShift.WorldAt(transform.position) != World.Dream) return;
        Vector3 d = player.transform.position - transform.position;
        float height = d.y;
        d.y = 0f;
        if (d.sqrMagnitude > radius * radius || height < -0.4f || height > 0.9f || player.VerticalSpeed > 1f) return;
        player.Launch(launchSpeed);
        readyAt = Time.time + 0.3f;
        squash = 1f;
        Fx.Burst(transform.position + Vector3.up * 0.4f, new Color(1f, 0.85f, 0.54f), 26);
        LucidAudio.Play(LucidAudio.Cue.Capture);
    }
}
