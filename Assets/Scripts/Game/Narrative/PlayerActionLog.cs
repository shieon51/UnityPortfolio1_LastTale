// PlayerActionLog.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

// 기록 타입
// ★ 숫자로 저장되므로 순서를 바꾸지 말고 끝에만 추가할 것 (기록 시스템 설계 5-3)
public enum RecordType
{
    Counter, AffectionChange, SuspicionChange, MemoryAcquired, MemoryErased,
    LevelUp, EventCompleted, Loop, TrustEarned, LineCrossed,
    MemoryHeard,      // 이미 아는 정보를 다시 들은 경우까지 포함
    BattleResult,     // 전투 승패
    AnchorSet,        // 시간의 닻 설치
    StatGain,         // 공·방·민 상승 (훈련·수련)
    Travel,           // 지역 이동
    Hunt,             // 사냥

    // ---- 기록 시스템 1단계 ----
    MentalChange,       // 정신력
    FatigueChange,      // 피로도
    TimeCrystalChange,  // 시간결정체 개수
    PersonalBondChange, // 소라의 개인친밀도 (key: NPC)
    BondKeyUsed,        // 개인친밀도 증가 키 사용 (key: 증가 키, source: NPC)
    ExpChange,          // 경험치
    AnchorRetracted,    // 플레이어가 닻을 거둠
    AnchorUsed,         // 닻으로 돌아감 (valueAfter: ReturnPath)
    AnchorVanished,     // 과거로 돌아가며 사라진 닻
    TimeAdvance,        // 시각 변화 (값은 절대 시각, 시간 단위)
    SceneEnter,         // 씬 진입 (payload: 위치)
}

// ★ 기록이 상태에 하는 일. 복원할 때 다시 적용할 수 있는지를 가른다 (기록 시스템 설계 5-2)
public enum ChangeOp
{
    Set,     // key의 값을 valueAfter로
    Add,     // key를 집합에 추가
    Remove,  // key를 집합에서 제거
    Event,   // 상태 변화 없음 — 무슨 일이 있었는지만 남긴다
}

// ★ 자주 쓰는 기록 키. 오타로 기록이 갈라지지 않도록 한곳에 둔다
public static class RecordKeys
{
    public const string Mental = "mental";
    public const string Fatigue = "fatigue";
    public const string TimeCrystal = "time_crystal";
    public const string Exp = "exp";
    public const string Time = "time";
}

[Serializable]
public class ActionRecord
{
    public long seq;           // ★ 세계 전체 순번. 회귀해도 되돌리지 않는다
    public int loopCount;      // 몇 회차에
    public int day, hour;      // 언제
    public int sceneId = -1;   // 어디서 — ★ 표시 이름은 저장하지 않고 표시할 때 조회한다 (언어 변경 대응)
    public RecordType type;    // 무슨 종류의 변화인지
    public ChangeOp op;        // ★ 상태에 하는 일
    public string key;         // 카운터 키 / NPC 이름 / 플래그 ID 등
    public string source;      // 누구에게서 / 어떤 이벤트에서 (선택)
    public int valueBefore;    // 변화 전 값
    public int valueAfter;     // 변화 후 값
    public string payload;     // ★ 정수로 담기지 않는 값 (위치, 시각 등). 대부분 비어 있다
}


public class PlayerActionLog : Singleton<PlayerActionLog>
{
    private List<ActionRecord> _records = new();
    public IReadOnlyList<ActionRecord> Records => _records;

    // ★ 다음에 붙일 순번. 닻 복원·Day 1 초기화에도 되돌리지 않는다 (세이브가 생기면 저장 대상)
    private long _nextSeq = 1;
    public long NextSeq => _nextSeq;

    public void Record(RecordType type, string key, int before = 0, int after = 0,
                       string source = null, string payload = null, ChangeOp? op = null)
    {
        var sora = PlayerManager.Instance?.CurrentCharacter as SoraStats;

        _records.Add(new ActionRecord
        {
            seq = _nextSeq++,
            loopCount = sora?.loopCount ?? 0,
            day = TimeManager.Instance != null ? TimeManager.Instance.currentDay : 0,
            hour = TimeManager.Instance != null ? TimeManager.Instance.currentHour : 0,
            sceneId = SceneLoader.Instance != null ? SceneLoader.Instance.CurrentSceneID : -1,
            type = type,
            op = op ?? DefaultOp(type),
            key = key,
            source = source,
            valueBefore = before,
            valueAfter = after,
            payload = payload,
        });
    }

    // ★ 기록 종류별 기본 연산. 기존 호출부는 고치지 않아도 알맞은 연산이 붙는다
    public static ChangeOp DefaultOp(RecordType type) => type switch
    {
        RecordType.MemoryAcquired or RecordType.AnchorSet or RecordType.BondKeyUsed => ChangeOp.Add,

        RecordType.MemoryErased or RecordType.AnchorRetracted
            or RecordType.AnchorUsed or RecordType.AnchorVanished => ChangeOp.Remove,

        RecordType.Counter or RecordType.AffectionChange or RecordType.SuspicionChange
            or RecordType.TrustEarned or RecordType.LineCrossed or RecordType.LevelUp
            or RecordType.StatGain or RecordType.MentalChange or RecordType.FatigueChange
            or RecordType.TimeCrystalChange or RecordType.PersonalBondChange
            or RecordType.ExpChange or RecordType.TimeAdvance or RecordType.SceneEnter => ChangeOp.Set,

        _ => ChangeOp.Event,   // EventCompleted, Loop, MemoryHeard, BattleResult, Travel, Hunt
    };

    // ---------------- 표시용 조회 ----------------

    // ★ 장소 표시 이름은 기록 시점이 아니라 표시 시점에 조회한다
    public static string ResolvePlaceName(ActionRecord r)
    {
        if (r == null || r.sceneId < 0) return null;
        string internalName = (DataManager.Instance != null
                               && DataManager.Instance.SceneDict.TryGetValue(r.sceneId, out var n)) ? n : null;
        return SceneNameUtil.GetDisplayName(r.sceneId, internalName);
    }

    // ---------------- payload 형식 (항상 InvariantCulture — 언어 설정에 따라 쉼표·소수점이 바뀌지 않게) ----------------

    public static string EncodeDayHour(int day, int hour)
        => string.Format(CultureInfo.InvariantCulture, "{0},{1}", day, hour);

    public static bool TryDecodeDayHour(string payload, out int day, out int hour)
    {
        day = hour = 0;
        if (string.IsNullOrEmpty(payload)) return false;
        var parts = payload.Split(',');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out day)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out hour);
    }

    public static string EncodePosition(Vector2 pos)
        => string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R}", pos.x, pos.y);

    public static bool TryDecodePosition(string payload, out Vector2 pos)
    {
        pos = Vector2.zero;
        if (string.IsNullOrEmpty(payload)) return false;
        var parts = payload.Split(',');
        if (parts.Length != 2) return false;
        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return false;
        pos = new Vector2(x, y);
        return true;
    }

    // ---------------- 기록장 "이번 흐름" 탭 ----------------

    // 플레이어에게 보여줄 기록 종류. 숨김 수치(호감도·의심·신뢰·선넘음)와
    // 표시 문장이 없는 카운터는 제외한다 (기획서 6-4-7).
    // ★ 1단계에서 추가한 종류는 저장만 하고 화면에는 내지 않는다
    private static readonly HashSet<RecordType> VisibleTypes = new()
    {
        RecordType.MemoryHeard,
        RecordType.MemoryErased,
        RecordType.EventCompleted,
        RecordType.LevelUp,
        RecordType.BattleResult,
        RecordType.Loop,
        RecordType.AnchorSet,
        RecordType.StatGain,
        RecordType.Travel,
        RecordType.Hunt,
    };

    public static bool IsPlayerVisible(RecordType type) => VisibleTypes.Contains(type);

    public IEnumerable<ActionRecord> VisibleRecords
        => _records.Where(r => IsPlayerVisible(r.type));

    // 앵커 스냅샷/복원 — ★ 순번(_nextSeq)은 되돌리지 않는다
    public List<ActionRecord> Snapshot() => new List<ActionRecord>(_records);
    public void Restore(List<ActionRecord> snapshot) { _records.Clear(); if (snapshot != null) _records.AddRange(snapshot); }
    public void ClearAll() => _records.Clear();

    // 힌트 NPC용 조회 — "이 흐름에서 안 해본 것" 판단에 사용
    // 로그는 닻 복귀 시 그 시점으로 복원되므로, 지금 남아 있는 기록 = 이번 흐름의 기록이다
    public bool HasDoneInCurrentFlow(string key)
        => _records.Exists(r => r.key == key);

    public bool HasDoneInCurrentFlow(RecordType type, string key)
        => _records.Exists(r => r.type == type && r.key == key);
}