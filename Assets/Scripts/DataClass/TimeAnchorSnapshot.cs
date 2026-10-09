using System.Collections.Generic;
using UnityEngine;

// "시간의 닻"을 내린 시점의 스냅샷.
// 여기 담기는 것은 "세계 쪽 상태"와 "그 시점의 몸 상태"다.
// 소라의 '의지'에 속한 것(기억, 개인친밀도, 스킬 숙련도)은 회귀해도 유지되므로 담지 않는다.
public class TimeAnchorSnapshot
{
    public int anchorId;   // ★ 누적 닻 번호 (기획서 6-6-4). 거둔 닻도 번호를 쓴다
    public int sceneID;
    public Vector2 position;
    public int day, hour;

    // 몸 상태 (강제 복귀 시에만 되돌린다)
    public int level, maxHealth, maxMana, experience;
    public int expToNextLevel;
    // ★ 공·방·민 기본값 (훈련으로 오른 값 포함). 강제 복귀 시 레벨과 함께 되돌린다 (기획서 11-3)
    public int attackBase, defenseBase, agilityBase;
    // ★ 닻을 내린 직후(설치 마나를 낸 뒤)의 체력·마나. 경로 2는 몸이 이 시점으로 돌아가므로 체력·마나도 이 값이 된다 (기획서 7-2)
    public int currentHealth, currentMana;

    // ★ HashSet은 순서를 보장하지 않아 복원 시 획득 순서가 사라진다 → List로 변경
    //   (지금은 기억을 되돌리지 않지만, 기록·디버그 표시에 순서가 쓰인다)
    public List<string> acquiredMemoryFlags;

    // NPC 쪽 시간선 상태
    public Dictionary<string, int> npcAffections;
    public Dictionary<string, int> npcSuspicions;
    public Dictionary<string, int> npcTrustEarned;
    public Dictionary<string, int> npcLineCrossed;
    public Dictionary<string, int> counters;

    public int loopCountAtSave;                // 어느 회차에서 저장했는지 (서사용)
    public List<ActionRecord> actionLog;       // 이번 흐름의 행적

    // ★ 기록 시스템 2단계 — 같은 순간의 범용 스냅샷. 지금은 옛 복원 결과와 비교하는 데만 쓴다.
    //   검증이 끝나면 복원을 이것으로 바꾸고, 위의 손으로 나열한 필드들을 걷어낸다
    public Snapshot recordSnapshot;

    // ※ soraPersonalBond는 제거했다.
    //   개인친밀도는 소라의 '의지'에 속해 회귀해도 유지되므로 스냅샷 대상이 아니다 (기획서 8장)
}