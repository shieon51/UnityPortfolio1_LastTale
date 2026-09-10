// SuspicionManager.cs (신규)
using System.Collections.Generic;
using UnityEngine;

public class SuspicionManager : Singleton<SuspicionManager>
{
    private Dictionary<string, int> _suspicion = new();

    public int GetSuspicion(string npcName) => _suspicion.TryGetValue(npcName, out var v) ? v : 0;

    public void AddSuspicion(string npcName, int amount)
    {
        _suspicion[npcName] = Mathf.Clamp(GetSuspicion(npcName) + amount, 0, 100);
        PropagateToRelatedNPCs(npcName, amount);
    }

    private void PropagateToRelatedNPCs(string sourceNpc, int amount)
    {
        var data = NPCManager.Instance.GetNPCData(sourceNpc);
        if (data.relatedNPCs == null) return;
        foreach (var related in data.relatedNPCs)
        {
            // 소문은 약해져서 전달됨 (친밀도가 높을수록 더 잘 퍼진다는 식으로 확장 가능)
            int spread = Mathf.FloorToInt(amount * data.rumorSpreadRatio);
            if (spread <= 0) continue;
            _suspicion[related] = Mathf.Clamp(GetSuspicion(related) + spread, 0, 100);
        }
    }

    public void ResetForNewLoop() => _suspicion.Clear(); // 회귀 시 초기화 (호감도와 같은 성격)
}