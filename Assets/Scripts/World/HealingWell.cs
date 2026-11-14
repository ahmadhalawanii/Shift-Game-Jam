using UnityEngine;

// Standing by the well restores health. The last calm before the Sleeper.
public class HealingWell : MonoBehaviour
{
    public float radius = 3.2f;
    public float healPerSecond = 0.45f;

    MemoryAvatar avatar;
    bool announced;
    float nextSpark;

    void Update()
    {
        var player = LevelFlow.Player;
        if (player == null) return;
        if ((player.transform.position - transform.position).sqrMagnitude > radius * radius) return;
        if (avatar == null) avatar = player.GetComponentInChildren<MemoryAvatar>();
        if (avatar == null || avatar.health >= 1f) return;
        avatar.health = Mathf.Min(1f, avatar.health + healPerSecond * Time.deltaTime);
        if (Time.time >= nextSpark)
        {
            nextSpark = Time.time + 0.12f;
            Fx.Burst(player.transform.position + Vector3.up, new Color(0.75f, 0.95f, 1f), 3);
        }
        if (!announced && EclipseEncounter.Instance != null)
        {
            announced = true;
            EclipseEncounter.Instance.Tell("The well remembers you. Health restored.", 3f);
        }
    }
}
