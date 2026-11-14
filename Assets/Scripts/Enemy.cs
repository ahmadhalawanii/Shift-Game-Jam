using System.Collections.Generic;
using UnityEngine;

// Base class for anything the player can shoot.
public abstract class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();

    public float health = 6f;
    public float radius = 0.9f;
    public Color deathColor = new Color(1f, 0.6f, 0.9f);

    protected Transform player { get { return PlayerController.Instance != null ? PlayerController.Instance.transform : null; } }

    public virtual void TakeBurstDamage(float damage, Vector3 hitPoint) { TakeDamage(damage, hitPoint); }

    protected virtual void OnEnable() { All.Add(this); }
    protected virtual void OnDisable() { All.Remove(this); }

    public virtual void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (EclipseEncounter.Finished || health <= 0f) return;
        health -= damage;
        Fx.Burst(hitPoint, new Color(1f, 0.8f, 0.4f), 6);
        if (health <= 0f) Die();
    }

    protected virtual void Die()
    {
        Fx.Burst(transform.position, deathColor, 70);
        Fx.Burst(transform.position, Color.white, 20);
        GameFeel.HitStop(0.07f);
        ThirdPersonCamera.Shake(0.3f);
        Destroy(gameObject);
    }
}
