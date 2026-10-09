#if UNITY_EDITOR
using System.Text;
using UnityEngine;

// 기록장 "이번 흐름" 변환 결과 확인용 임시 스크립트.
// 빈 오브젝트에 붙여 플레이 중 컴포넌트 우클릭 → 메뉴 실행.
// 확인이 끝나면 오브젝트째 지우면 된다.
public class FlowLogTester : MonoBehaviour
{
    [ContextMenu("1) 더미 기록 만들기")]
    private void MakeDummyRecords()
    {
        var log = PlayerActionLog.Instance;
        if (log == null) { Debug.LogWarning("[FlowLogTester] PlayerActionLog 없음 — 플레이 중인지 확인"); return; }

        // 대화 중 정보 획득 → 이벤트 종료 순서를 흉내낸다
        log.Record(RecordType.MemoryHeard, "liel_likes_apple", 0, 1, "Liel");
        log.Record(RecordType.EventCompleted, "좋아하는 과일", 0, 1, "Liel");

        log.Record(RecordType.MemoryHeard, "liel_sister_exists", 0, 0, "Liel");   // 재확인
        log.Record(RecordType.EventCompleted, "가족 이야기", 0, 1, "Liel");

        log.Record(RecordType.StatGain, "ATK", 0, 2);
        log.Record(RecordType.LevelUp, "level", 1, 2);
        log.Record(RecordType.EventCompleted, "수련", 0, 1, null);

        log.Record(RecordType.LevelUp, "soul_record", 30, 31);
        log.Record(RecordType.BattleResult, "훈련모드(승) 3분 12초", 0, 0, "Liel");

        log.Record(RecordType.AnchorSet, "1", 0, 0);
        log.Record(RecordType.Loop, "return_to_anchor", 0, 0, "55회차 Day 10 12:00");

        Debug.Log("[FlowLogTester] 더미 기록 생성 완료 — 2번 메뉴로 출력");
    }

    [ContextMenu("2) 이번 흐름 출력")]
    private void DumpFlow()
    {
        var log = PlayerActionLog.Instance;
        if (log == null) { Debug.LogWarning("[FlowLogTester] PlayerActionLog 없음"); return; }

        var flow = FlowLogBuilder.Build(log.CurrentFlowRecords);   // ★ 3-A — Records는 세계 전체 기록이라 이번 흐름만
        if (flow.days.Count == 0) { Debug.Log("[FlowLogTester] 표시할 기록이 없음"); return; }

        var sb = new StringBuilder();
        foreach (var d in flow.days)
        {
            sb.AppendLine($"── Day {d.day}   방문: {string.Join(", ", d.visitedPlaces)}");
            sb.AppendLine($"   총 획득: {string.Join("  ", d.totals.ConvertAll(t => t.text))}");

            foreach (var e in d.entries)
            {
                string time = e.sameTimeAsPrevious ? "   │  " : $"{e.hour:00}:00";
                string dur = e.durationHours > 0 ? $"{e.durationHours}h" : " -";
                string who = string.IsNullOrEmpty(e.speaker) ? "" : $"[{e.speaker}] ";
                string tags = string.Join("  ", e.tags.ConvertAll(t => $"<{t.kind}>{t.text}"));
                string mark = e.highlight ? "★" : " ";
                sb.AppendLine($"{mark} {time}  {dur}  {who}{e.title}    {tags}");
            }
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }

    [ContextMenu("3) 기록 전부 지우기")]
    private void ClearAll()
    {
        PlayerActionLog.Instance?.ClearAll();
        Debug.Log("[FlowLogTester] 기록 삭제");
    }
}
#endif