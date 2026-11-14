using System;
using UnityEngine;

// Trigger volume covering the arena (its edge is the entrance). The first time the player's collider
// enters it, the boss lock-on camera engages and OnFightStart fires for the boss bar and music.
// Created at runtime by EclipseEncounter, so Level1 needs no regeneration.
public class BossArenaTrigger : MonoBehaviour
{
    public static event Action OnFightStart;
    public bool Fired { get; private set; }

    public static BossArenaTrigger Create(Vector3 center, float radius)
    {
        var go = new GameObject("BossArenaTrigger");
        go.transform.position = center;
        var sphere = go.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = radius;
        // Kinematic body so a CharacterController always produces trigger messages.
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        return go.AddComponent<BossArenaTrigger>();
    }

    void OnTriggerEnter(Collider other) { Check(other); }
    void OnTriggerStay(Collider other) { Check(other); } // covers spawning inside (retry, debug keys)

    void Check(Collider other)
    {
        if (Fired || EclipseEncounter.Finished || other.GetComponent<PlayerController>() == null) return;
        Fired = true;
        ThirdPersonCamera.EngageLock();
        OnFightStart?.Invoke();
    }
}
