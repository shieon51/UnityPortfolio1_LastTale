using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MemoryManager : Singleton<MemoryManager>
{
    [Tooltip("Resources 하위 폴더 경로 — 이 안의 모든 MemoryFragmentData를 자동으로 긁어옴")]
    public string resourcesFolder = "MemoryFragments";
    public string topicResourcesFolder = "MemoryTopics";                  // ★ 신규

    private Dictionary<string, MemoryTopicData> _topicRegistry = new();   // ★ 신규
    private HashSet<string> _stageFlagIds = new();                        // ★ 주제 단계에 쓰인 조각 (단독 표시에서 제외)

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
        int before = GetCounter(key);
        _counters[key] = before + 1;
        PlayerActionLog.Instance?.Record(RecordType.Counter, key, before, before + 1); // ★ 인자 4개
    }

    public IEnumerable<MemoryFragmentData> GetAllRegistered() => _registry.Values;

    protected void Awake()
    {
        LoadRegistry();
        LoadTopicRegistry();   // ★ 추가 — 주제는 지금까지 런타임에 로드되지 않았다
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

    private void LoadTopicRegistry()
    {
        _topicRegistry.Clear();
        _stageFlagIds.Clear();

        var all = Resources.LoadAll<MemoryTopicData>(topicResourcesFolder);
        foreach (var topic in all)
        {
            if (string.IsNullOrEmpty(topic.topicId)) continue;
            if (_topicRegistry.ContainsKey(topic.topicId))
            {
                Debug.LogError($"[MemoryManager] topicId 중복: '{topic.topicId}' — '{topic.name}' 애셋이 기존 등록과 충돌함");
                continue;
            }
            _topicRegistry[topic.topicId] = topic;

            if (topic.stages == null) continue;
            foreach (var stage in topic.stages)
            {
                if (string.IsNullOrEmpty(stage.requiredFlagId)) continue;
                _stageFlagIds.Add(stage.requiredFlagId);

                if (!_registry.ContainsKey(stage.requiredFlagId))
                    Debug.LogWarning($"[MemoryManager] '{topic.topicId}' 주제의 단계가 등록되지 않은 flagId '{stage.requiredFlagId}'를 가리킴");
            }
        }
        Debug.Log($"[MemoryManager] 기억 주제 {_topicRegistry.Count}개 로드 완료");
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
            PlayerActionLog.Instance?.Record(RecordType.MemoryAcquired, flagId); 
        }
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
        int latestIndex = -1;

        for (int i = 0; i < topic.stages.Length; i++)
        {
            if (!HasMemory(topic.stages[i].requiredFlagId)) continue;
            latest = topic.stages[i];
            latestIndex = i;
        }

#if UNITY_EDITOR
        // ★ 앞 단계를 건너뛰고 뒷 단계를 먼저 얻었다면 작성 실수일 가능성이 높다
        for (int i = 0; i < latestIndex; i++)
        {
            if (!HasMemory(topic.stages[i].requiredFlagId))
                Debug.LogWarning($"[MemoryManager] '{topic.topicId}' 주제의 {i + 1}단계('{topic.stages[i].requiredFlagId}')를 건너뛰고 {latestIndex + 1}단계를 획득함 — ink 조건을 확인할 것");
        }
#endif
        return latest;
    }

    public bool IsTopicFullyRevealed(MemoryTopicData topic)
    {
        var stage = GetCurrentStage(topic);
        return stage != null && stage.isFinal;
    }

    public void ClearAllAcquired() { _acquiredFlags.Clear(); _acquiredOrder.Clear(); } // ★ 수정
    public void ClearAllCounters() => _counters.Clear(); // 5번에서 만들 카운터 시스템

    public MemoryFragmentData GetData(string flagId)
        => _registry.TryGetValue(flagId, out var d) ? d : null;

    public int GetUnderstandingScore(string npcName)
        => _registry.Values.Count(d => HasMemory(d.flagId) && IsRelatedTo(d.relatedNpcs, d.category, npcName));


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

    // ---------------- 기록장 조회 ----------------

    public IEnumerable<MemoryTopicData> GetAllTopics() => _topicRegistry.Values;
    public MemoryTopicData GetTopic(string topicId)
        => _topicRegistry.TryGetValue(topicId, out var t) ? t : null;

    // 주제의 단계로 쓰인 조각인지 (단독 항목으로 중복 표시하지 않기 위함)
    public bool IsUsedInTopic(string flagId) => _stageFlagIds.Contains(flagId);

    // 다른 조각에 의해 반증되었는지 (기록장에서 취소선)
    public bool IsRefuted(string flagId)
    {
        if (string.IsNullOrEmpty(flagId)) return false;
        foreach (var data in _registry.Values)
        {
            if (data.refutesFlagIds == null || !HasMemory(data.flagId)) continue;
            foreach (var target in data.refutesFlagIds)
                if (target == flagId) return true;
        }
        return false;
    }

    // 이 조각을 반증한 조각 (부연설명 문구를 꺼내기 위함)
    public MemoryFragmentData GetRefutingFragment(string flagId)
    {
        foreach (var data in _registry.Values)
        {
            if (data.refutesFlagIds == null || !HasMemory(data.flagId)) continue;
            foreach (var target in data.refutesFlagIds)
                if (target == flagId) return data;
        }
        return null;
    }

    private static bool IsRelatedTo(string[] relatedNpcs, string fallbackCategory, string npcName)
    {
        if (relatedNpcs != null)
            foreach (var npc in relatedNpcs)
                if (npc == npcName) return true;
        return fallbackCategory == npcName;   // relatedNpcs를 아직 안 채운 애셋 대비
    }

    // 인물 페이지에 단독으로 표시할 조각 (획득했고, 주제 단계로 쓰이지 않은 것)
    public IEnumerable<MemoryFragmentData> GetFragmentsForNpc(string npcName)
        => _registry.Values.Where(d =>
               HasMemory(d.flagId) &&
               !IsUsedInTopic(d.flagId) &&
               IsRelatedTo(d.relatedNpcs, d.category, npcName));

    // 인물 페이지에 표시할 주제 (한 단계라도 알게 된 것)
    public IEnumerable<MemoryTopicData> GetTopicsForNpc(string npcName)
        => _topicRegistry.Values.Where(t =>
               IsRelatedTo(t.relatedNpcs, t.category, npcName) &&
               GetCurrentStage(t) != null);
}