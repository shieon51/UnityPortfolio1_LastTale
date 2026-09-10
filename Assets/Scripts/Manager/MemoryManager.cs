using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MemoryManager : Singleton<MemoryManager>
{
    [Tooltip("Resources 하위 폴더 경로 — 이 안의 모든 MemoryFragmentData를 자동으로 긁어옴")]
    public string resourcesFolder = "MemoryFragments";

    private Dictionary<string, MemoryFragmentData> _registry = new();
    private List<string> _acquiredOrder = new(); // ★ 추가
    private HashSet<string> _acquiredFlags = new(); // _acquiredFlags(HashSet)는 그대로 빠른 조회용으로 유지

    public event Action<string> OnMemoryAcquired; // 기록장 UI 등이 나중에 구독
    public event Action<string> OnMemoryErased;

    private Dictionary<string, int> _counters = new();

    public int GetCounter(string key) => _counters.TryGetValue(key, out var v) ? v : 0;

    //IncrementCounter가 자동으로 로그도 남기게
    public void IncrementCounter(string key)
    {
        _counters[key] = GetCounter(key) + 1;
        PlayerActionLog.Instance?.Record(key); // ★ 추가 — 별도 호출 없이 자동 기록
    }

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
        { 
            _acquiredOrder.Add(flagId); 
            OnMemoryAcquired?.Invoke(flagId); 
        } // ★ 순서 기록
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
        { 
            _acquiredOrder.Remove(flagId); 
            OnMemoryErased?.Invoke(flagId); 
        }
    }

    public MemoryTopicData.Stage GetCurrentStage(MemoryTopicData topic)
    {
        MemoryTopicData.Stage latest = null;
        foreach (var stage in topic.stages)
            if (HasMemory(stage.requiredFlagId)) latest = stage; // 배열 뒤쪽일수록 더 진전된 단계
        return latest; // null = 아직 아무것도 모름
    }
    public bool IsTopicFullyRevealed(MemoryTopicData topic)
    {
        var stage = GetCurrentStage(topic);
        return stage != null && stage.isFinal;
    }

    public void ClearAllAcquired() => _acquiredFlags.Clear();
    public void ClearAllCounters() => _counters.Clear(); // 5번에서 만들 카운터 시스템

    public MemoryFragmentData GetData(string flagId)
        => _registry.TryGetValue(flagId, out var d) ? d : null;

    public int GetUnderstandingScore(string npcCategory)
    => GetAllRegistered().Count(d => d.category == npcCategory && HasMemory(d.flagId));


    // 6번(시간 고정/회귀) 시스템 만들 때 이 두 개를 그대로 씀
    public IEnumerable<string> GetAllAcquired() => _acquiredOrder; // ★ 순서 보존된 것 반환
    public void RestoreAcquired(IEnumerable<string> flags)
    {
        _acquiredFlags.Clear(); 
        _acquiredOrder.Clear();
        foreach (var f in flags) 
        { 
            _acquiredFlags.Add(f); 
            _acquiredOrder.Add(f); 
        }
    }

    // MemoryManager.cs — 카운터 전체 스냅샷/복원
    public Dictionary<string, int> SnapshotCounters() => new Dictionary<string, int>(_counters);
    public void RestoreCounters(Dictionary<string, int> snapshot)
    {
        _counters.Clear();
        foreach (var kvp in snapshot) _counters[kvp.Key] = kvp.Value;
    }
}