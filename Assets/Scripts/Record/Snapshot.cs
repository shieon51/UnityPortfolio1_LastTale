using System.Collections.Generic;

// ★ 스냅샷을 찍은 이유 (기록시스템_설계 6-1). 숫자로 저장되므로 값을 바꾸지 않고 끝에만 추가한다
public enum SnapshotReason
{
    PartStart = 0,      // 파트 시작 (1회차 Day 1) — 경로 3 복원 기준
    LoopStart = 1,      // 회차 시작
    Anchor = 2,         // 닻 설치
    DayStart = 3,       // 하루 시작 (00:00)
    BeforeEnding = 4,   // 결말 직전
    Verify = 5,         // 개발용 비교 — 저장하지 않는다
}

// ★ 기록 시스템 2단계 — 범용 스냅샷 (기록시스템_설계 6장)
//   TimeAnchorSnapshot처럼 필드를 손으로 나열하지 않고, IRecordable마다 덩어리를 모은다
public class Snapshot
{
    public long seq;                 // 찍은 시점의 다음 기록 순번 — "이 순번 직전의 상태"
    public SnapshotReason reason;
    public int day, hour;

    // RecordId → 덩어리. 모르는 RecordId의 덩어리도 버리지 않고 보관한다
    public Dictionary<string, StateBlock> blocks = new();
}
