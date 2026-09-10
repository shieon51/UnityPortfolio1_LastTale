// PlayerActionLog.cs (신규)
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ActionRecord
{
    public int loopCount;      // 몇 회차에
    public int day, hour;      // 언제
    public string actionKey;   // 무엇을 (increment_counter와 같은 키 체계)
    public string sceneName;   // 어디서
}

public class PlayerActionLog : Singleton<PlayerActionLog>
{
    private List<ActionRecord> _records = new();
    public IReadOnlyList<ActionRecord> Records => _records;

    public void Record(string actionKey)
    {
        var sora = PlayerManager.Instance?.CurrentCharacter as SoraStats;
        _records.Add(new ActionRecord
        {
            loopCount = sora?.loopCount ?? 0,
            day = TimeManager.Instance.currentDay,
            hour = TimeManager.Instance.currentHour,
            actionKey = actionKey,
            sceneName = DataManager.Instance.SceneDict.TryGetValue(SceneLoader.Instance.CurrentSceneID, out var n) ? n : "?",
        });
    }

    // 앵커 스냅샷/복원 (다른 시스템과 동일 패턴)
    public List<ActionRecord> Snapshot() => new List<ActionRecord>(_records);
    public void Restore(List<ActionRecord> snapshot) { _records.Clear(); _records.AddRange(snapshot); }
    public void ClearAll() => _records.Clear();

    // 힌트 NPC용 조회 — "이 회차에 안 해본 것" 같은 판단에 사용
    public bool HasDoneInCurrentLoop(string actionKey, int currentLoop)
        => _records.Exists(r => r.actionKey == actionKey && r.loopCount == currentLoop);
}