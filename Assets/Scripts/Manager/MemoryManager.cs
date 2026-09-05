using System;
using System.Collections.Generic;
using UnityEngine;

public class MemoryManager : Singleton<MemoryManager>
{
    [Tooltip("Resources 하위 폴더 경로 — 이 안의 모든 MemoryFragmentData를 자동으로 긁어옴")]
    public string resourcesFolder = "MemoryFragments";

    private Dictionary<string, MemoryFragmentData> _registry = new();
    private HashSet<string> _acquiredFlags = new();

    public event Action<string> OnMemoryAcquired; // 기록장 UI 등이 나중에 구독
    public event Action<string> OnMemoryErased;

    private Dictionary<string, int> _counters = new();

    public int GetCounter(string key) => _counters.TryGetValue(key, out var v) ? v : 0;
    public void IncrementCounter(string key) => _counters[key] = GetCounter(key) + 1;

    public IEnumerable<MemoryFragmentData> GetAllRegistered() => _registry.Values;

    protected void Awake()
    {
        LoadRegistry();
    }

    private void LoadRegistry()
    {
        var all = Resources.LoadAll<MemoryFragmentData>(resourcesFolder);
        foreach (var data in all)
        {
            if (string.IsNullOrEmpty(data.flagId)) continue;
            if (_registry.ContainsKey(data.flagId))
            {
                Debug.LogError($"[MemoryManager] flagId 중복: '{data.flagId}' — '{data.name}' 애셋이 기존 등록과 충돌함");
                continue;
            }
            _registry[data.flagId] = data;
        }
        Debug.Log($"[MemoryManager] 기억 조각 {_registry.Count}개 로드 완료");
    }

    public bool HasMemory(string flagId) => _acquiredFlags.Contains(flagId);

    public void AcquireMemory(string flagId)
    {
        if (!_registry.ContainsKey(flagId))
        {
            Debug.LogWarning($"[MemoryManager] 등록 안 된 flagId 획득 시도: '{flagId}' — 애셋을 먼저 만들었는지 확인");
            return;
        }
        if (_acquiredFlags.Add(flagId))
            OnMemoryAcquired?.Invoke(flagId);
    }

    public void EraseMemory(string flagId)
    {
        var data = GetData(flagId);
        if (data != null && !data.isEraseable)
        {
            Debug.LogWarning($"[MemoryManager] '{flagId}'는 지울 수 없는 기억으로 설정됨");
            return;
        }
        if (_acquiredFlags.Remove(flagId))
            OnMemoryErased?.Invoke(flagId);
    }

    public void ClearAllAcquired() => _acquiredFlags.Clear();
    public void ClearAllCounters() => _counters.Clear(); // 5번에서 만들 카운터 시스템

    public MemoryFragmentData GetData(string flagId)
        => _registry.TryGetValue(flagId, out var d) ? d : null;

    // 6번(시간 고정/회귀) 시스템 만들 때 이 두 개를 그대로 씀
    public IEnumerable<string> GetAllAcquired() => _acquiredFlags;
    public void RestoreAcquired(IEnumerable<string> flags)
    {
        _acquiredFlags.Clear();
        foreach (var f in flags) _acquiredFlags.Add(f);
    }
}