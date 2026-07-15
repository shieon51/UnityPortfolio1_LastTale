using System.Collections.Generic;
using UnityEngine;

// CutsceneAnimationCatalog.cs — DataManager의 Dictionary 로딩 패턴과 동일한 사상
[CreateAssetMenu(menuName = "LastMarchan/Visual/Cutscene Animation Catalog")]
public class CutsceneAnimationCatalog : ScriptableObject
{
    public List<CutsceneAnimationData> entries;
    private Dictionary<string, CutsceneAnimationData> _lookup;

    public CutsceneAnimationData Get(string key)
    {
        if (_lookup == null)
        {
            _lookup = new Dictionary<string, CutsceneAnimationData>();
            foreach (var e in entries)
                if (e != null && !string.IsNullOrEmpty(e.key)) _lookup[e.key] = e;
        }
        _lookup.TryGetValue(key, out var data);
        return data;
    }
}