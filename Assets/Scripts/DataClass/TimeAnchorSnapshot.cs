using System.Collections.Generic;
using UnityEngine;

// TimeAnchorSnapshot.cs (신규) — "시간 고정" 시점의 스냅샷
public class TimeAnchorSnapshot
{
    public int sceneID;
    public Vector2 position;
    public int day, hour;
    public int level, maxHealth, maxMana, experience;
    public HashSet<string> acquiredMemoryFlags; // 소라의 기억

    // ★ 신규 — NPC 쪽 시간선 상태 전체
    public Dictionary<string, int> npcAffections;    // NPC별 호감도
    public Dictionary<string, int> npcSuspicions;    // NPC별 의심
    public Dictionary<string, int> counters;         // 만남 횟수 + 선택지 기록 전부
    public int loopCountAtSave;                      // 어느 회차에서 저장했는지 (서사용)

    // 플레이어 행적 로그
    public List<ActionRecord> actionLog;

    // npc 신뢰도/선넘음, 소라 개인 친밀도 관련
    public Dictionary<string, int> npcTrustEarned;
    public Dictionary<string, int> npcLineCrossed;
    public Dictionary<string, int> soraPersonalBond;
}