using System.Collections.Generic;
using UnityEngine;

public enum ExistsIn { Both, Dream, Nightmare }

// Marks a wall the player can run along. The run area is a box: thin on local X (the wall thickness),
// local Y is the height band the player's chest must be in, local Z is the length of the run.
// Only walls with this component can be wall-run, so the player always knows which ones work:
// they carry glowing handholds.
public class WallRunSurface : MonoBehaviour
{
    public static readonly List<WallRunSurface> All = new List<WallRunSurface>();

    public Vector3 size = new Vector3(0.6f, 5f, 16f);
    public ExistsIn existsIn = ExistsIn.Both;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // A wall that belongs to one world only works where that world is, so a swap mid-run changes which walls hold.
    public bool UsableAt(Vector3 point)
    {
        if (existsIn == ExistsIn.Both) return true;
        return (WorldShift.WorldAt(point) == World.Nightmare) == (existsIn == ExistsIn.Nightmare);
    }

    public Vector3 Closest(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        Vector3 half = size * 0.5f;
        local.x = Mathf.Clamp(local.x, -half.x, half.x);
        local.y = Mathf.Clamp(local.y, -half.y, half.y);
        local.z = Mathf.Clamp(local.z, -half.z, half.z);
        return transform.TransformPoint(local);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 1f, 0.9f, 0.5f);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
