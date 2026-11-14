using UnityEditor;

// Regenerates the combat prefabs (Resources/Combat) and the player figure (Prefabs/Props/Lucid.prefab).
// The world itself is built by Lucid > Build Lucid World.
public static class PlayerCombatPropsBuilder
{
    [MenuItem("Lucid/Build Combat And Player Props")]
    public static void BuildAll()
    {
        CombatBuilder.Build();
        MemoryBuilder.Build();
        AssetDatabase.SaveAssets();
    }
}
