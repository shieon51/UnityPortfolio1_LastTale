using System.Collections.Generic;
using UnityEngine;

// "시간의 닻"을 내린 시점의 스냅샷.
// 여기 담기는 것은 "세계 쪽 상태"와 "그 시점의 몸 상태"다.
// 소라의 혼에 속한 것(기억, 개인친밀도, 스킬 숙련도)은 회귀해도 유지되므로 담지 않는다.
public class TimeAnchorSnapshot
{
    public int sceneID;
    public Vector2 position;
    public int day, hour;

    // 몸 상태 (강제 복귀 시에만 되돌린다)
    public int level, maxHealth, maxMana, experience;

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

    // ※ soraPersonalBond는 제거했다.
    //   개인친밀도는 소라의 혼에 속해 회귀해도 유지되므로 스냅샷 대상이 아니다 (기획서 8장)
}