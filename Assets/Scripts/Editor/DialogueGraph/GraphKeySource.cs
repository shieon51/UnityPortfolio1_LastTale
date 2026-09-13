// GraphKeySource.cs — 전체 교체
using System.Collections.Generic;
using UnityEditor;

public static class GraphKeySource
{
    public static List<string> GetMemoryFlagIds()
    {
        var result = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:MemoryFragmentData"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<MemoryFragmentData>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && !string.IsNullOrEmpty(asset.flagId)) result.Add(asset.flagId);
        }
        result.Sort();
        return result.Count > 0 ? result : new List<string> { "(등록된 기억 없음)" };
    }

    public static List<string> GetNPCNames()
    {
        var result = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:NPCDefinition"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<NPCDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && !string.IsNullOrEmpty(asset.npcName)) result.Add(asset.npcName);
        }
        result.Sort();
        return result.Count > 0 ? result : new List<string> { "(등록된 NPC 없음)" };
    }

    public static List<string> GetCueIds()
    {
        var result = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:NarrativeCue"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<NarrativeCue>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && !string.IsNullOrEmpty(asset.cueId)) result.Add(asset.cueId);
        }
        result.Sort();
        return result.Count > 0 ? result : new List<string> { "(등록된 큐 없음)" };
    }
}