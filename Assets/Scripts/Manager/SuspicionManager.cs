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

    public int GetSuspicion(string npcName) => _suspicion.TryGetValue(npcName, out var v) ? v : 0;

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

        _suspicion[npcName] = Mathf.Clamp(GetSuspicion(npcName) + final, minSuspicion, maxSuspicion);
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

            _suspicion[listener] = Mathf.Clamp(GetSuspicion(listener) + final, minSuspicion, maxSuspicion);
            Debug.Log($"[Suspicion] {listener} 소문으로 +{final} (출처: {sourceNpc}, 현재 {GetSuspicion(listener)})");
        }
    }

    public void ResetForNewLoop() => _suspicion.Clear();
    public Dictionary<string, int> Snapshot() => new Dictionary<string, int>(_suspicion);
    public void Restore(Dictionary<string, int> snapshot)
    {
        _suspicion.Clear();
        foreach (var kvp in snapshot) _suspicion[kvp.Key] = kvp.Value;
    }
}