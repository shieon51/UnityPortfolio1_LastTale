// SuspicionManager.cs (신규)
using System.Collections.Generic;
using UnityEngine;

public class SuspicionManager : Singleton<SuspicionManager>
{
    private Dictionary<string, int> _suspicion = new();

    [Header("의심 배율")]
    [Tooltip("소라와 친할 때 의심이 줄어드는 배율")]
    public float trustedMultiplier = 0.5f;
    [Tooltip("소라와 사이가 나쁠 때(호감도 0 미만) 의심이 증폭되는 배율")]
    public float hostileMultiplier = 1.5f;
    [Tooltip("소문을 들은 쪽이 소라와 친할 때 소문이 감쇠되는 배율")]
    public float rumorTrustedMultiplier = 0.3f;
    [Tooltip("전달자 신뢰도가 설정 안 됐을 때 쓸 기본값")]
    public float defaultTrustInSource = 0.5f;

    [Header("의심 수치 범위")]
    public int minSuspicion = 0;
    public int maxSuspicion = 100;

    private Dictionary<string, int> _trustEarned = new();  // 신뢰 적립: 도움/구조/예측 성공
    private Dictionary<string, int> _lineCrossed = new();  // 선 넘은 횟수: 협박/위협/불쾌한 떠보기

    [Header("해명 판정")]
    [Tooltip("해명 성공 시 의심이 줄어드는 양")]
    public int confessionSuspicionRelief = 40;
    [Tooltip("해명 실패 시 의심이 늘어나는 양")]
    public int confessionFailPenalty = 25;


    public int GetSuspicion(string npcName) => _suspicion.TryGetValue(npcName, out var v) ? v : 0;
    public int GetTrustEarned(string npc) => _trustEarned.TryGetValue(npc, out var v) ? v : 0;
    public int GetLineCrossed(string npc) => _lineCrossed.TryGetValue(npc, out var v) ? v : 0;


    /// <summary>이 NPC가 해당 행적을 직접 관찰할 수 있는지</summary>
    public bool CanObserve(string npcName, string counterKey)
    {
        var data = NPCManager.Instance.GetNPCData(npcName);
        if (data.observableCounterKeys == null) return false;
        return System.Array.IndexOf(data.observableCounterKeys, counterKey) >= 0;
    }

    /// <summary>직접 목격한 의심스러운 행동 — 관찰 가능 여부와 NPC 성향/호감도를 모두 반영</summary>
    public void AddDirectSuspicion(string npcName, int rawAmount, string relatedCounterKey = null)
    {
        // 1) 관찰 불가능한 정보에 대해서는 의심 자체가 성립하지 않음
        if (!string.IsNullOrEmpty(relatedCounterKey) && !CanObserve(npcName, relatedCounterKey))
        {
            Debug.Log($"[Suspicion] {npcName}은(는) '{relatedCounterKey}'를 관찰할 수 없어 의심하지 않음");
            return;
        }

        var data = NPCManager.Instance.GetNPCData(npcName);

        // 2) NPC 고유 성향 반영
        float amount = rawAmount * data.suspicionSensitivity;

        // 3) 소라와 친할수록 의심이 완화됨 (믿어주는 것)
        if (data.hiddenAffection >= data.trustThresholdForSora) amount *= trustedMultiplier;
        else if (data.hiddenAffection < 0) amount *= hostileMultiplier; // 이미 사이가 나쁘면 더 의심

        int final = Mathf.RoundToInt(amount);
        if (final <= 0) return;

        int before = GetSuspicion(npcName);
        _suspicion[npcName] = Mathf.Clamp(before + final, minSuspicion, maxSuspicion);
        PlayerActionLog.Instance?.Record(RecordType.SuspicionChange, npcName, before, GetSuspicion(npcName)); // ★ 추가
        Debug.Log($"[Suspicion] {npcName} 직접 의심 +{final} (현재 {GetSuspicion(npcName)})");

        PropagateRumor(npcName, final);
    }

    /// <summary>소문 전파 — 듣는 쪽이 "말한 사람을 얼마나 믿는지" × "소라를 얼마나 믿는지"로 결정</summary>
    private void PropagateRumor(string sourceNpc, int amount)
    {
        var source = NPCManager.Instance.GetNPCData(sourceNpc);
        if (source.relatedNPCs == null) return;

        for (int i = 0; i < source.relatedNPCs.Length; i++)
        {
            string listener = source.relatedNPCs[i];
            var listenerData = NPCManager.Instance.GetNPCData(listener);

            // 듣는 쪽이 소문 전달자를 얼마나 신뢰하는지
            float trustInSource = (source.trustInRelatedNPCs != null && i < source.trustInRelatedNPCs.Length)
                ? source.trustInRelatedNPCs[i] : defaultTrustInSource;

            float spread = amount * trustInSource * listenerData.suspicionSensitivity;

            // 듣는 쪽이 소라와 친하면 "그럴 리 없다"며 크게 깎임
            if (listenerData.hiddenAffection >= listenerData.trustThresholdForSora) spread *= rumorTrustedMultiplier;

            int final = Mathf.RoundToInt(spread);
            if (final <= 0)
            {
                Debug.Log($"[Suspicion] {listener}: {sourceNpc}의 말을 들었지만 흘려넘김");
                continue;
            }

            int beforeSpread = GetSuspicion(listener);                                              // ★ 추가
            _suspicion[listener] = Mathf.Clamp(beforeSpread + final, minSuspicion, maxSuspicion);
            PlayerActionLog.Instance?.Record(RecordType.SuspicionChange, listener, beforeSpread, GetSuspicion(listener)); // ★ 추가
            Debug.Log($"[Suspicion] {listener} 소문으로 +{final} (출처: {sourceNpc}, 현재 {GetSuspicion(listener)})");
        }
    }

    // 신뢰 적립: 도움/구조/예측 성공
    public void AddTrustEarned(string npc, int amount)
    {
        int before = GetTrustEarned(npc);
        _trustEarned[npc] = Mathf.Clamp(before + amount, 0, maxSuspicion);
        PlayerActionLog.Instance?.Record(RecordType.TrustEarned, npc, before, _trustEarned[npc]);
    }

    // 선 넘은 횟수: 협박/위협/불쾌한 떠보기
    public void AddLineCrossed(string npc, int amount)
    {
        int before = GetLineCrossed(npc);
        _lineCrossed[npc] = Mathf.Clamp(before + amount, 0, maxSuspicion);
        PlayerActionLog.Instance?.Record(RecordType.LineCrossed, npc, before, _lineCrossed[npc]);
    }

    /// <summary>해명 결과: 2=완전히 믿음, 1=반신반의, 0=믿지 않음(관계 악화)</summary>
    public int ResolveConfession(string npcName)
    {
        var data = NPCManager.Instance.GetNPCData(npcName);
        float score = 0f;
        score += GetTrustEarned(npcName);                    // 그동안 쌓은 신뢰
        score += data.hiddenAffection;                        // 호감도
        score -= GetLineCrossed(npcName) * 2f;                // 선 넘은 건 두 배로 감점
        score -= GetSuspicion(npcName) * 0.5f;                // 이미 쌓인 의심
        score *= (2f - data.suspicionSensitivity);            // 의심 많은 성격일수록 불리

        int result = score >= 30f ? 2 : (score >= 0f ? 1 : 0);

        if (result == 2) _suspicion[npcName] = Mathf.Max(minSuspicion, GetSuspicion(npcName) - confessionSuspicionRelief);
        else if (result == 0) _suspicion[npcName] = Mathf.Clamp(GetSuspicion(npcName) + confessionFailPenalty, minSuspicion, maxSuspicion);

        Debug.Log($"[Confession] {npcName} 해명 점수 {score:F1} → 결과 {result}");
        return result;
    }

    // ★ 의심 수치 스냅샷/복원 (이게 빠져있었음)
    public Dictionary<string, int> Snapshot() => new Dictionary<string, int>(_suspicion);
    public void Restore(Dictionary<string, int> s)
    {
        _suspicion.Clear();
        if (s != null) foreach (var k in s) _suspicion[k.Key] = k.Value;
    }

    public Dictionary<string, int> SnapshotTrust() => new Dictionary<string, int>(_trustEarned);
    public Dictionary<string, int> SnapshotLineCrossed() => new Dictionary<string, int>(_lineCrossed);
    public void RestoreTrust(Dictionary<string, int> s) { _trustEarned.Clear(); if (s != null) foreach (var k in s) _trustEarned[k.Key] = k.Value; }
    public void RestoreLineCrossed(Dictionary<string, int> s) { _lineCrossed.Clear(); if (s != null) foreach (var k in s) _lineCrossed[k.Key] = k.Value; }

    public void ResetForNewLoop() { _suspicion.Clear(); _trustEarned.Clear(); _lineCrossed.Clear(); } // ★ 수정
}