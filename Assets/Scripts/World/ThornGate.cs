using System.Collections;
using UnityEngine;

// A wall of thorns across the path. Opened by a corrupted bud dying or a combat gate being cleared.
public class ThornGate : MonoBehaviour
{
    public Transform visual;
    public Collider[] solids;
    public float seconds = 0.9f;
    public bool IsOpen { get; private set; }

    void Awake()
    {
        if (solids == null || solids.Length == 0) solids = GetComponentsInChildren<Collider>();
        if (visual == null) visual = transform;
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        StartCoroutine(Animate(false));
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        StartCoroutine(Animate(true));
    }

    IEnumerator Animate(bool closing)
    {
        if (closing) foreach (var c in solids) if (c != null) c.enabled = true;
        Fx.Burst(transform.position + Vector3.up * 2f, new Color(1f, 0.55f, 0.3f), 60);
        ThirdPersonCamera.Shake(0.25f);
        LucidAudio.Play(LucidAudio.Cue.Fusion);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, seconds);
            float k = Mathf.SmoothStep(0f, 1f, closing ? 1f - t : t);
            // The thorns shrivel into the ground.
            visual.localScale = new Vector3(1f - k * 0.35f, Mathf.Max(0.001f, 1f - k), 1f - k * 0.35f);
            yield return null;
        }
        if (!closing)
        {
            foreach (var c in solids) if (c != null) c.enabled = false;
            visual.gameObject.SetActive(false);
        }
    }

    void OnEnable() { if (visual != null && !IsOpen) visual.gameObject.SetActive(true); }
}
