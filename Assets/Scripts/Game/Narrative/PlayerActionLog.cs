// PlayerActionLog.cs (신규)
using System;
using System.Collections.Generic;
using System.Linq;

// 기록 타입
public enum RecordType
{
    Counter, AffectionChange, SuspicionChange, MemoryAcquired, MemoryErased,
    LevelUp, EventCompleted, Loop, TrustEarned, LineCrossed,
    MemoryHeard,      // ★ 이미 아는 정보를 다시 들은 경우까지 포함
    BattleResult      // ★ 전투 승패
}

[Serializable]
public class ActionRecord
{
    public int loopCount;      // 몇 회차에
    public int day, hour;      // 언제
    public string sceneName;   // 어디서
    public int sceneId = -1;   // ★ 표시 이름을 로컬라이제이션으로 뽑기 위한 원본 ID
    public RecordType type;    // 무슨 종류의 변화인지
    public string key;         // 카운터 키 / NPC 이름 / 플래그 ID 등
    public string source;      // ★ 누구에게서 / 어떤 이벤트에서 (선택)
    public int valueBefore;    // 변화 전 값
    public int valueAfter;     // 변화 후 값
}


public class PlayerActionLog : Singleton<PlayerActionLog>
{
    private List<ActionRecord> _records = new();
    public IReadOnlyList<ActionRecord> Records => _records;

    public void Record(RecordType type, string key, int before = 0, int after = 0, string source = null)
    {
        var sora = PlayerManager.Instance?.CurrentCharacter as SoraStats;
        int sceneId = SceneLoader.Instance != null ? SceneLoader.Instance.CurrentSceneID : -1;

        _records.Add(new ActionRecord
        {
            loopCount = sora?.loopCount ?? 0,
            day = TimeManager.Instance != null ? TimeManager.Instance.currentDay : 0,
            hour = TimeManager.Instance != null ? TimeManager.Instance.currentHour : 0,
            sceneId = sceneId,
            // ★ 내부 이름 대신 표시 이름을 저장한다 (기록장에 그대로 보여줄 수 있게)
            sceneName = SceneNameUtil.GetDisplayName(sceneId,
                (DataManager.Instance != null && sceneId >= 0
                 && DataManager.Instance.SceneDict.TryGetValue(sceneId, out var n)) ? n : null),
            type = type,
            key = key,
            source = source,          // ★ 추가
            valueBefore = before,
            valueAfter = after,
        });
    }

    // ---------------- 기록장 "이번 흐름" 탭 ----------------

    // ★ 플레이어에게 보여줄 기록 종류. 숨김 수치(호감도·의심·신뢰·선넘음)와
    //   표시 문장이 없는 카운터는 제외한다 (기획서 6-4-7)
    private static readonly HashSet<RecordType> VisibleTypes = new()
    {
        RecordType.MemoryHeard,
        RecordType.MemoryErased,
        RecordType.EventCompleted,
        RecordType.LevelUp,
        RecordType.BattleResult,
        RecordType.Loop,
    };

    public static bool IsPlayerVisible(RecordType type) => VisibleTypes.Contains(type);

    public IEnumerable<ActionRecord> VisibleRecords
        => _records.Where(r => IsPlayerVisible(r.type));

    // 앵커 스냅샷/복원 (다른 시스템과 동일 패턴)
    public List<ActionRecord> Snapshot() => new List<ActionRecord>(_records);
    public void Restore(List<ActionRecord> snapshot) { _records.Clear(); if (snapshot != null) _records.AddRange(snapshot); }
    public void ClearAll() => _records.Clear();

    // 힌트 NPC용 조회 — "이 흐름에서 안 해본 것" 판단에 사용
    // ★ 로그는 닻 복귀 시 그 시점으로 복원되므로, 지금 남아 있는 기록 = 이번 흐름의 기록이다.
    //   회차로 거르면 복원된 기록이 옛 회차 값을 갖고 있어 누락된다
    public bool HasDoneInCurrentFlow(string key)
        => _records.Exists(r => r.key == key);

    public bool HasDoneInCurrentFlow(RecordType type, string key)
        => _records.Exists(r => r.type == type && r.key == key);

    //[System.Obsolete("HasDoneInCurrentFlow를 사용할 것. 닻 복귀 후 회차 기준은 맞지 않는다")]
    //public bool HasDoneInCurrentLoop(string key, int currentLoop)
    //    => HasDoneInCurrentFlow(key);
}