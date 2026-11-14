using UnityEngine;

// Reaching this marks a zone as reached. Retry and the G debug key return the player to the zone's spawn point.
public class LucidCheckpoint : MonoBehaviour
{
    public int zone;
    public float radius = 5f;
    bool reached;

    void Update()
    {
        if (reached) return;
        var player = LevelFlow.Player;
        if (player == null || LevelFlow.Instance == null) return;
        if ((player.transform.position - transform.position).sqrMagnitude > radius * radius) return;
        reached = true;
        LevelFlow.Instance.Reach(zone);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.5f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
