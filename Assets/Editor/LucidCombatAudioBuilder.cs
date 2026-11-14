using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public static class LucidCombatAudioBuilder
{
    // Mixer authoring lives in UnityEditor.Audio; reflection keeps internal editor types out of game code.
    [MenuItem("Lucid/Build Combat Audio")]
    public static void Build()
    {
        const string path = "Assets/Resources/LucidCombat.mixer";
        if (AssetDatabase.LoadAssetAtPath<AudioMixer>(path) != null) return;
        Type type = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        { type = assembly.GetType("UnityEditor.Audio.AudioMixerController"); if (type != null) break; }
        if (type == null) throw new InvalidOperationException("Audio mixer editor unavailable");
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var mixer = type.GetMethod("CreateMixerControllerAtPath", flags).Invoke(null, new object[] { path });
        var normal = (UnityEngine.Object)type.GetProperty("TargetSnapshot", flags).GetValue(mixer);
        normal.name = "Open";
        var master = type.GetProperty("masterGroup", flags).GetValue(mixer);
        var ambience = type.GetMethod("CreateNewGroup", flags).Invoke(mixer, new object[] { "World", true });
        type.GetMethod("AddChildToParent", flags).Invoke(mixer, new[] { ambience, master });
        var effects = type.GetMethod("CreateNewGroup", flags).Invoke(mixer, new object[] { "Player", true });
        type.GetMethod("AddChildToParent", flags).Invoke(mixer, new[] { effects, master });
        type.GetMethod("CloneNewSnapshotFromTarget", flags).Invoke(mixer, new object[] { false });
        var quiet = (UnityEngine.Object)type.GetProperty("TargetSnapshot", flags).GetValue(mixer);
        quiet.name = "Held Breath";
        ambience.GetType().GetMethod("SetValueForVolume", flags).Invoke(ambience, new object[] { mixer, quiet, -32f });
        type.GetProperty("startSnapshot", flags).SetValue(mixer, normal);
        type.GetProperty("TargetSnapshot", flags).SetValue(mixer, normal);
        EditorUtility.SetDirty((UnityEngine.Object)mixer); EditorUtility.SetDirty(normal); EditorUtility.SetDirty(quiet);
        AssetDatabase.SaveAssets();
    }
}
