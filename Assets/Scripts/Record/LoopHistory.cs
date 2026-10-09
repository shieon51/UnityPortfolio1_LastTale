using System;
using System.Collections.Generic;
using UnityEngine;

// ★ 기록 시스템 3단계 — 회차와 닻의 이력 (기록시스템_설계 7장, 13-3).
//   회귀해도 되돌리지 않는다. 4단계 세이브에서 직접 저장한다 (IRecordable이 아니다 — 설계 13-3-1).
//   RecordSystem과 같은 이유로 static이다 (싱글톤은 씬에 없으면 빈 오브젝트가 자동 생성된다)
public static class LoopHistory
{
    private static readonly List<LoopRecord> _loops = new();
    private static readonly List<AnchorRecord> _anchors = new();

    // ★ 도메인 리로드를 끈 플레이 모드에서도 지난 플레이의 이력이 남지 않게 한다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Clear();

    public static IReadOnlyList<LoopRecord> Loops => _loops;
    public static IReadOnlyList<AnchorRecord> Anchors => _anchors;

    // 지금 진행 중인 회차 (마지막 회차). 첫 회차를 열기 전에는 null
    public static LoopRecord Current => _loops.Count > 0 ? _loops[_loops.Count - 1] : null;

    // 공식 하드 리셋 전용
    public static void Clear()
    {
        _loops.Clear();
        _anchors.Clear();
    }

    // 같은 번호가 있으면 나중 것을 돌려준다 (디버그로 회차 수를 고친 경우에만 겹칠 수 있다)
    public static LoopRecord Find(int loopNumber)
    {
        for (int i = _loops.Count - 1; i >= 0; i--)
            if (_loops[i].loopNumber == loopNumber) return _loops[i];
        return null;
    }

    public static AnchorRecord FindAnchor(int anchorId)
    {
        for (int i = _anchors.Count - 1; i >= 0; i--)
            if (_anchors[i].anchorId == anchorId) return _anchors[i];
        return null;
    }

    // ---------------- 회차 ----------------

    // 회차를 연다. 직전 회차에는 "끝난 뒤 고른 지점"을 적는다.
    //   returnAnchorId·returnPath: 직전 회차가 고른 지점 (첫 회차는 0, -1)
    //   branchSeq: 부모 흐름 중 이 순번 미만을 물려받는다 (0이면 물려받지 않음)
    public static LoopRecord BeginLoop(int loopNumber, int parentLoop, int branchAnchorId, long branchSeq,
                                       int branchDay, int branchHour, int returnPath)
    {
        var previous = Current;
        if (previous != null)
        {
            if (previous.IsOpen)
            {
                // 회귀 절차가 회차를 닫지 않고 열었다 — 구간이 겹치지 않게 여기서 닫는다
                Debug.LogWarning($"[LoopHistory] {previous.loopNumber}회차가 닫히지 않은 채 새 회차를 엽니다 — 끝난 방식 없이 닫습니다");
                previous.endSeq = LastSeq();
            }
            previous.returnAnchorId = branchAnchorId;
            previous.returnPath = returnPath;
        }
        if (Find(loopNumber) != null)
            Debug.LogWarning($"[LoopHistory] {loopNumber}회차가 이미 있습니다 (디버그로 회차 수를 고쳤는지 확인) — 부모 찾기가 어긋날 수 있습니다");

        var log = PlayerActionLog.Instance;
        var loop = new LoopRecord
        {
            loopNumber = loopNumber,
            parentLoop = parentLoop,
            branchAnchorId = branchAnchorId,
            branchSeq = branchSeq,
            branchDay = branchDay,
            branchHour = branchHour,
            // ★ 첫 회차는 처음부터 — 첫 회차를 열기 전(Awake 등)에 남은 기록도 첫 회차의 것이다
            startSeq = _loops.Count == 0 ? 1 : (log != null ? log.NextSeq : 1),
        };
        _loops.Add(loop);
        return loop;
    }

    // 지금 회차를 닫는다. 회귀 절차의 맨 처음에 부른다 (그 뒤의 기록은 모두 새 회차의 것이다)
    public static void EndLoop(LoopEndType endType, Snapshot endSnapshot, string endingTitleKey = null, string deathCauseKey = null)
    {
        var loop = Current;
        if (loop == null) { Debug.LogWarning("[LoopHistory] 닫을 회차가 없습니다"); return; }
        if (!loop.IsOpen) { Debug.LogWarning($"[LoopHistory] {loop.loopNumber}회차는 이미 닫혔습니다 ({loop.endType})"); return; }

        loop.endSeq = LastSeq();
        loop.endType = endType;
        loop.endSnapshot = endSnapshot;
        loop.endingTitleKey = endingTitleKey;
        loop.deathCauseKey = deathCauseKey;
    }

    // 지금까지 붙은 마지막 순번 (기록이 하나도 없으면 0)
    private static long LastSeq()
    {
        var log = PlayerActionLog.Instance;
        return log != null ? log.NextSeq - 1 : 0;
    }

    // ---------------- 닻 ----------------

    public static void RegisterAnchor(int anchorId, int day, int hour, int sceneId)
    {
        if (FindAnchor(anchorId) != null) { Debug.LogWarning($"[LoopHistory] 닻 #{anchorId}이 이미 있습니다"); return; }
        _anchors.Add(new AnchorRecord
        {
            anchorId = anchorId,
            loopSet = Current != null ? Current.loopNumber : 0,
            day = day,
            hour = hour,
            sceneId = sceneId,
        });
    }

    public static void SetAnchorStatus(int anchorId, AnchorStatus status, int usedByLoop = -1)
    {
        var a = FindAnchor(anchorId);
        if (a == null) { Debug.LogWarning($"[LoopHistory] 닻 #{anchorId}의 이력이 없습니다 — 상태 {status}를 남기지 못했습니다"); return; }
        a.status = status;
        if (usedByLoop >= 0) a.usedByLoop = usedByLoop;
    }

    // ---------------- 이번 흐름 ----------------

    // 이 회차의 흐름 = 부모 흐름 중 branchSeq 앞부분 + 자기 구간 (설계 13-3-2).
    //   부모를 따라 올라가며 순번 구간을 모은 뒤, 순번순인 전체 기록에서 그 구간에 든 것만 고른다
    public static List<ActionRecord> FlowRecords(LoopRecord loop, IReadOnlyList<ActionRecord> all)
    {
        var result = new List<ActionRecord>();
        if (all == null) return result;
        if (loop == null) { result.AddRange(all); return result; }   // 첫 회차를 열기 전 — 전부

        var ranges = new List<(long from, long to)>();
        long cutoff = long.MaxValue;   // 이 순번 미만만 물려받는다
        var cur = loop;
        var visited = new HashSet<LoopRecord>();   // 디버그로 번호가 겹쳐 고리가 생겨도 멈추게
        while (cur != null && visited.Add(cur))
        {
            long end = cur.IsOpen ? long.MaxValue : cur.endSeq;
            long to = Math.Min(end, cutoff == long.MaxValue ? long.MaxValue : cutoff - 1);
            if (to >= cur.startSeq) ranges.Add((cur.startSeq, to));

            if (cur.branchSeq <= 0) break;   // Day 1·첫 회차 — 물려받는 것 없음
            cutoff = Math.Min(cutoff, cur.branchSeq);
            cur = Find(cur.parentLoop);
        }

        foreach (var r in all)
        {
            foreach (var (from, to) in ranges)
            {
                if (r.seq >= from && r.seq <= to) { result.Add(r); break; }
            }
        }
        return result;
    }

    public static List<ActionRecord> CurrentFlowRecords(IReadOnlyList<ActionRecord> all) => FlowRecords(Current, all);
}
