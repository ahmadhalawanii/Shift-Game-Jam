using UnityEditor;
using UnityEngine;

// Creates Resources/BossAttackData.asset and adds a tuning row for any catalog attack that lacks one.
// Existing rows and values are never overwritten, so designer tuning survives re-runs.
public static class BossDataBuilder
{
    const string Path = "Assets/Resources/BossAttackData.asset";

    [MenuItem("Lucid/Build Boss Attack Data")]
    public static void Build()
    {
        var data = AssetDatabase.LoadAssetAtPath<BossAttackData>(Path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BossAttackData>();
            AssetDatabase.CreateAsset(data, Path);
        }
        var rows = new System.Collections.Generic.List<AttackTuning>(data.attacks);
        foreach (var attack in BossAttack.Catalog())
            if (data.Find(attack.Id) == null) rows.Add(attack.Defaults.Copy());
        data.attacks = rows.ToArray();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("BossAttackData: " + data.attacks.Length + " attack rows at " + Path);
    }
}
