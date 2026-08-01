using UnityEngine;

// Utility AI 가중치 계산 관련 - npc 행동 결정
public struct NPCDecisionContext
{
    public NPC Self;
    public Transform Player;
    public float DistanceToPlayer;
    public float SelfHealthPercent;
    public float SelfManaPercent;
    public bool PlayerIsAttacking;
    public NPCActionBase LastUsedAction; // 행동 큐(콤보) 판단용
}