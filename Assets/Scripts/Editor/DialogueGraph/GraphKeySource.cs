// GraphKeySource.cs (신규, Editor 폴더)
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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

    /// <summary>조건 타입에 맞는 키 후보 목록</summary>
    public static List<string> GetKeysFor(GraphConditionType type) => type switch
    {
        GraphConditionType.HasMemory => GetMemoryFlagIds(),
        GraphConditionType.CounterAtLeast => new List<string>(), // 카운터는 자유 입력
        _ => GetNPCNames(),
    };

    public static List<string> GetKeysFor(GraphLogicType type) => type switch
    {
        GraphLogicType.AcquireMemory or GraphLogicType.EraseMemory => GetMemoryFlagIds(),
        GraphLogicType.IncrementCounter => new List<string>(),
        GraphLogicType.PlayCue => GetCueIds(),
        _ => GetNPCNames(),
    };

    public static bool UsesFreeText(GraphConditionType t) => t == GraphConditionType.CounterAtLeast;
    public static bool UsesFreeText(GraphLogicType t) => t == GraphLogicType.IncrementCounter;
    public static bool UsesAmount(GraphLogicType t) => t is not (GraphLogicType.AcquireMemory or GraphLogicType.EraseMemory or GraphLogicType.PlayCue or GraphLogicType.IncrementCounter);
}