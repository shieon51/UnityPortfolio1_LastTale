using System.Collections.Generic;

// 기록장 "이번 흐름" 탭이 그대로 그릴 수 있는 형태.
// UI가 RecordType을 직접 해석하지 않도록 분리한다.

public enum FlowTagKind
{
    NewInfo,      // 보라 — 새 정보
    RepeatInfo,   // 회색 — 재확인
    Growth,       // 초록 — 공방민·레벨·스킬
    Record,       // 금색 — 영혼 레벨 경신
    State,        // 하늘 — 정신력·시간결정체
    Battle,       // 민트 — 전투 결과
    Anchor,       // 보라 — 닻
}

public class FlowTag
{
    public FlowTagKind kind;
    public string text;        // 화면에 그대로 출력할 문구
    public string tooltip;     // 없으면 null
}

public class FlowEntry
{
    public int hour;
    public int durationHours;        // 0이면 "-"
    public bool sameTimeAsPrevious;  // true면 시각 칸 대신 세로선을 그린다
    public string speaker;           // [리엘] — 없으면 null
    public string title;
    public string place;             // 툴팁용
    public bool dimmed;              // 거절·시간 보내기 등
    public bool highlight;           // 새 정보·첫 만남·전투
    public readonly List<FlowTag> tags = new();
}

public class FlowDay
{
    public int day;
    public readonly List<FlowEntry> entries = new();
    public readonly List<FlowTag> totals = new();     // "총 획득"
    public readonly List<string> visitedPlaces = new();
}

public class FlowLog
{
    public string startLine;                          // "이번 회차 시작: Day 2 - 13:00 (닻으로 복귀)"
    public readonly List<FlowTag> startStats = new(); // 몸 Lv / 공방민 / 정신력
    public readonly List<FlowDay> days = new();
}