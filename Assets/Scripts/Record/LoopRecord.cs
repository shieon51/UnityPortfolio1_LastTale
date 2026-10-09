using System;
using System.Collections.Generic;   // ★ 3-C — 요약 목록

// ★ 기록 시스템 3단계 — 회차가 끝난 방식 (기획서 6-6-4 결말 분류). 숫자로 저장되므로 값을 바꾸지 않고 끝에만 추가한다
public enum LoopEndType
{
    None = 0,         // 아직 진행 중
    Death = 1,        // 사망 (일반 사망·스토리 사망)
    Incomplete = 2,   // 불완전 결말
    Voluntary = 3,    // 자발적 회귀
    Ending = 4,       // 결말 (1부 완결, 단 하나)
    DebugSkip = 5,    // ★ 디버그 "다음 회차로". 실제 게임에서는 생기지 않는다
}

// ★ 닻의 최종 상태 (기획서 6-6-4 닻 표시). 숫자로 저장되므로 값을 바꾸지 않고 끝에만 추가한다
public enum AnchorStatus
{
    Unused = 0,       // 사용 전 (진행 중인 회차에만 나타난다)
    Used = 1,         // 직접 사용 (자발적·경로 1)
    ForcedUsed = 2,   // 강제 사용 (경로 2)
    Vanished = 3,     // 과거로 돌아가며 사라짐
    Retracted = 4,    // 플레이어가 거둠 (사라짐 아이콘으로 그린다)
}

// ★ 회차 하나 (기록시스템_설계 7-1). 이야기의 행적에서 한 행이 된다
[Serializable]
public class LoopRecord
{
    public int loopNumber;           // 회차 번호 = SoraStats.loopCount (첫 회차 0)
    public int part = 1;             // 파트 (1부·2부)

    // 갈라져 나온 곳 (설계 13-3-2)
    public int parentLoop = -1;      // 부모 회차. 첫 회차는 -1
    public int branchAnchorId;       // 갈라져 나온 닻. Day 1에서 시작했으면 0
    public long branchSeq;           // 부모 흐름 중 이 순번 "미만"을 물려받는다. 0이면 물려받지 않는다
    public int branchDay, branchHour;

    // 자기 구간 — startSeq ~ endSeq (진행 중이면 endSeq = 0)
    public long startSeq, endSeq;

    // 끝난 방식
    public LoopEndType endType = LoopEndType.None;
    public string endingTitleKey;    // 스토리 사망·결말의 제목 키 (3-C)
    public string deathCauseKey;     // 일반 사망의 사인 키 (3-B)
    public string deathCauseSource;  // ★ 3-B — 사인의 대상 (보스전이면 보스 NPC 키). 이름 글자가 아니라 키 — 표시할 때 지금 아는 이름으로 바꾼다

    // 끝난 뒤 고른 지점 — 다음 회차를 열 때 채운다
    public int returnAnchorId;       // 고른 닻. Day 1이면 0
    public int returnPath = -1;      // TimeLoopManager.ReturnPath 값. 아직 없으면 -1

    // 스냅샷 (4단계에서 회차 파일로 옮긴다)
    public Snapshot startSnapshot;   // 회차 시작 — 첫 씬 로드가 끝난 순간
    public Snapshot endSnapshot;     // 결말 직전 — 회차를 닫는 순간

    // ★ 3-C — 이야기의 행적 한 행에 필요한 요약. 회차를 닫을 때 계산해 둔다 (진행 중이면 null — 그때그때 계산)
    //   요약은 자기 구간 기록에서 계산한 결과일 뿐이라 언제든 다시 만들 수 있다 (설계 7-1)
    public LoopSummary summary;

    public bool IsOpen => endType == LoopEndType.None;
}

// ★ 3-C — 마일스톤의 출처 (기획서 6-6-4). 숫자로 저장되므로 값을 바꾸지 않고 끝에만 추가한다
public enum MilestoneKind
{
    Authored = 0,     // 작가가 노드에 표시한 장면 (#milestone)
    BossBattle = 1,   // 실전 보스전 결과 (자동)
    SoulRecord = 2,   // 영혼 레벨 경신 (자동)
}

[Serializable]
public class MilestoneEntry
{
    public MilestoneKind kind;
    public string key;               // 작가: 제목 키 / 보스전: 보스 NPC 키 / 영혼 레벨: 비움
    public int value;                // 보스전: 승 1·패 0 / 영혼 레벨: 새 레벨
    public int importance;           // 클수록 먼저 보여준다
    public long seq;                 // 기록 순번 (시간순 정렬)
    public int day, hour;
}

// ★ 3-C — 회차 요약 (기록시스템_설계 7-1 summary)
[Serializable]
public class LoopSummary
{
    public int startLevel, endLevel;              // 몸 레벨 (모르면 0)
    public bool soulRecord;                       // ★ — 이 회차에서 영혼 레벨을 경신했는가 (몸 레벨이 다시 오른 것만으로는 아니다)
    public int newInfoCount;                      // 새로 알게 된 정보 수
    public List<MilestoneEntry> milestones = new();   // 시간순. 화면에 몇 개를 고를지는 TopMilestones
    public List<int> anchorsSet = new();          // 이 회차에 설치한 닻 번호
    public List<string> cutsceneKeys = new();     // 본 컷씬 — 컷씬 데이터가 생기면 채운다 (기획서 C-8)

    // 화면에 먼저 보여줄 n개 — 중요도 높은 순 → 시간순으로 고른 뒤, 고른 것을 다시 시간순으로 (기획서 6-6-4)
    public List<MilestoneEntry> TopMilestones(int count)
    {
        var picked = new List<MilestoneEntry>(milestones);
        picked.Sort((a, b) => a.importance != b.importance ? b.importance.CompareTo(a.importance) : a.seq.CompareTo(b.seq));
        if (picked.Count > count) picked.RemoveRange(count, picked.Count - count);
        picked.Sort((a, b) => a.seq.CompareTo(b.seq));
        return picked;
    }
}

// ★ 닻 하나의 이력 (기록시스템_설계 7-2). 회차가 아니라 세계 단위로 관리한다 —
//   8회차에 내린 닻이 10회차의 선택으로 사라지듯, 최종 상태가 나중 회차에서 바뀌기 때문이다
[Serializable]
public class AnchorRecord
{
    public int anchorId;             // 누적 설치 번호
    public int loopSet;              // 설치한 회차
    public int day, hour, sceneId;   // 설치 시각·장소
    public AnchorStatus status = AnchorStatus.Unused;
    public int usedByLoop = -1;      // 이 닻에서 갈라진 회차. 없으면 -1
}
