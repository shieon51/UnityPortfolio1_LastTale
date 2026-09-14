// GraphKeySource.cs — 전체 교체
using System.Collections.Generic;
using UnityEditor;

public static class GraphKeySource
{
    private static List<string> _memoryCache, _npcCache, _cueCache, _counterCache, _knotCache;
    private static double _lastRefresh;
    private const double CacheSeconds = 3.0;

    /// <summary>애셋을 새로 만든 뒤 목록이 갱신 안 되면 이걸 호출</summary>
    public static void InvalidateCache()
    {
        _memoryCache = _npcCache = _cueCache = _counterCache = _knotCache = null; // ★ _knotCache 추가
    }

    private static void CheckExpiry()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - _lastRefresh > CacheSeconds) { InvalidateCache(); _lastRefresh = now; }
    }

    private static List<string> Scan<T>(System.Func<T, string> selector) where T : UnityEngine.Object
    {
        var result = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) continue;
            string v = selector(asset);
            if (!string.IsNullOrEmpty(v)) result.Add(v);
        }
        result.Sort();
        return result;
    }

    public static List<string> GetMemoryFlagIds()
    {
        CheckExpiry();
        _memoryCache ??= Scan<MemoryFragmentData>(a => a.flagId);
        return _memoryCache.Count > 0 ? new List<string>(_memoryCache) : new List<string> { "(등록된 기억 없음)" };
    }

    public static List<string> GetNPCNames()
    {
        CheckExpiry();
        _npcCache ??= Scan<NPCDefinition>(a => a.npcName);
        return _npcCache.Count > 0 ? new List<string>(_npcCache) : new List<string> { "(등록된 NPC 없음)" };
    }

    public static List<string> GetCueIds()
    {
        CheckExpiry();
        _cueCache ??= Scan<NarrativeCue>(a => a.cueId);
        return _cueCache.Count > 0 ? new List<string>(_cueCache) : new List<string> { "(등록된 큐 없음)" };
    }

    public static List<string> GetCounterIds()
    {
        CheckExpiry();
        _counterCache ??= Scan<CounterDefinition>(a => a.counterId);
        return new List<string>(_counterCache); // 빈 리스트 그대로 반환 (자유 입력 폴백 판정용)
    }

    /// <summary>모든 대화 그래프의 시작(Start) 노드 knot 이름 목록</summary>
    public static List<string> GetKnotNames()   // ★ 신규
    {
        CheckExpiry();
        _knotCache ??= ScanKnots();
        return _knotCache.Count > 0 ? new List<string>(_knotCache) : new List<string> { "(그래프에 knot 없음)" };
    }

    private static List<string> ScanKnots()
    {
        var result = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:DialogueGraphData"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<DialogueGraphData>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) continue;
            foreach (var n in asset.nodes)
                if (n.nodeType == "Start" && !string.IsNullOrWhiteSpace(n.knotName))
                    result.Add(n.knotName);
        }
        result.Sort();
        return result;
    }
}