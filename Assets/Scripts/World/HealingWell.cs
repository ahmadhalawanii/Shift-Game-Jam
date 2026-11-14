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
        if (Time.time >= nextSpark)
        {
            nextSpark = Time.time + 1.5f;
            Vector3 from = transform.position + Vector3.up * 1.1f;
            var seed = EnemyBullet.SpawnBoss(from, player.transform.position + Vector3.up - from, 3f, .01f, World.Dream);
            if (seed != null) seed.HeartSeed();
        }
        if (!announced && EclipseEncounter.Instance != null)
        {
            announced = true;
            EclipseEncounter.Instance.Tell("The well releases PINK Heart Seeds. Catch them with Ward.", 3f);
        }
    }
}
