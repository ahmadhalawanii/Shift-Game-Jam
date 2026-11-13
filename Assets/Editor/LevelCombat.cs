using UnityEditor;
using UnityEngine;

// Level 1 as a combat level: waves of wisps along the road, then (later) the boss arena.
public static class LevelCombat
{
    const string WispPath = "Assets/Resources/Combat/Wisp.prefab";

    static void Wisps(Transform parent, float z, params float[] xs)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(WispPath);
        foreach (float x in xs)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
            g.transform.position = new Vector3(x, 1.7f, z);
        }
    }

    public static void Build(Transform player)
    {
        var root = new GameObject("Level1_Combat").transform;
        DreamBuilder.Populate(root, player);
        NightmareBuilder.Populate(root, player);

        Wisps(root, 26f, -7f, 7f);
        Wisps(root, 50f, -10f, 0f, 10f);
        Wisps(root, 74f, -12f, -4f, 4f, 12f);
        Wisps(root, 98f, -14f, -7f, 0f, 7f, 14f);

        var hud = new GameObject("LevelHUD").AddComponent<LevelHUD>();
        hud.player = player;
        hud.avatar = player.GetComponentInChildren<MemoryAvatar>();
        hud.combat = player.GetComponent<PlayerCombat>();
    }
}
