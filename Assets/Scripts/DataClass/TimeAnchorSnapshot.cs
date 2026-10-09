using System.Collections.Generic;
using UnityEngine;

// "시간의 닻"을 내린 시점의 기록.
// ★ 2-C-2 — 몸·세계 상태(레벨, 공방민, 체력·마나, 호감도, 의심, 카운터, 기억)를 손으로 나열하던 필드를 걷어냈다.
//   그 값들은 모두 recordSnapshot의 덩어리에 있고, 회귀 복원도 그것을 쓴다(RecordSystem.RestoreLayers).
//   여기 남은 것은 닻의 "주소"(언제·어디)와 아직 층위가 정해지지 않은 행적 로그뿐이다
public class TimeAnchorSnapshot
{
    public int anchorId;   // ★ 누적 닻 번호 (기획서 6-6-4). 거둔 닻도 번호를 쓴다

    // 닻의 주소 — 닻 간격 계산, 씬 마커 배치, 회귀 기록 payload, 회귀 후 이동 목적지에 쓴다
    public int sceneID;
    public Vector2 position;
    public int day, hour;

    public int loopCountAtSave;                // 어느 회차에서 저장했는지 (서사용)
    public List<ActionRecord> actionLog;       // 이번 흐름의 행적 — [미결] 층위 (기록시스템_설계 13-2-6)

    // ★ 같은 순간의 범용 스냅샷. 경로별로 고른 층위만 되돌린다 (닻을 내리기 직전에 찍는다)
    public Snapshot recordSnapshot;
}