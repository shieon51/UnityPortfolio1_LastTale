// PlayerActionLog.cs (신규)
using System;
using System.Collections.Generic;
using UnityEngine;

// 기록 타입
public enum RecordType { Counter, AffectionChange, SuspicionChange, MemoryAcquired, MemoryErased, LevelUp, EventCompleted, Loop }

[Serializable]
public class ActionRecord
{
    public int loopCount;      // 몇 회차에
    public int day, hour;      // 언제
    public string sceneName;   // 어디서
    public RecordType type;    // 무슨 종류의 변화인지
    public string key;         // 카운터 키 / NPC 이름 / 플래그 ID 등
    public int valueBefore;    // 변화 전 값
    public int valueAfter;     // 변화 후 값
}


public class PlayerActionLog : Singleton<PlayerActionLog>
{
    private List<ActionRecord> _records = new();
    public IReadOnlyList<ActionRecord> Records => _records;

    public void Record(RecordType type, string key, int before = 0, int after = 0)
    {
        var sora = PlayerManager.Instance?.CurrentCharacter as SoraStats;
        _records.Add(new ActionRecord
        {
            loopCount = sora?.loopCount ?? 0,
            day = TimeManager.Instance != null ? TimeManager.Instance.currentDay : 0,
            hour = TimeManager.Instance != null ? TimeManager.Instance.currentHour : 0,
            sceneName = (DataManager.Instance != null && SceneLoader.Instance != null
                && DataManager.Instance.SceneDict.TryGetValue(SceneLoader.Instance.CurrentSceneID, out var n)) ? n : "?",
            type = type,
            key = key,
            valueBefore = before,
            valueAfter = after,
        });
    }

    // 앵커 스냅샷/복원 (다른 시스템과 동일 패턴)
    public List<ActionRecord> Snapshot() => new List<ActionRecord>(_records);
    public void Restore(List<ActionRecord> snapshot) { _records.Clear(); if (snapshot != null) _records.AddRange(snapshot); }
    public void ClearAll() => _records.Clear();

    // 힌트 NPC용 조회 — "이 회차에 안 해본 것" 같은 판단에 사용
    public bool HasDoneInCurrentLoop(string key, int currentLoop)
        => _records.Exists(r => r.key == key && r.loopCount == currentLoop); // ★ actionKey → key 수정
}