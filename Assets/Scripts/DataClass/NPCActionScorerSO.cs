using UnityEngine;

// Utility AI 가중치 계산 관련
public struct NPCDecisionContext
{
    public NPC Self;
    public Transform Player;
    public float DistanceToPlayer;
    public bool PlayerIsAttacking;
}

public abstract class NPCActionScorerSO : ScriptableObject
{
    public string actionName; // "Attack1", "Teleport", "Guard" 등
    public abstract float Score(NPCDecisionContext ctx); // 점수가 높을수록 우선 실행
}

/**
 * 각 행동(근접공격/순간이동/방어 등)마다 이 클래스를 상속한 애셋을 하나씩 만들고, 
 * Liel_BattleIdleState에서 등록된 스코어러들을 순회하며 가장 높은 점수의 행동으로 전환하는 식입니다. 
 * 기획하신 "거리<3m 시 가중치 폭등" 같은 조건들이 전부 애셋의 인스펙터 값으로 들어가게 되어, 밸런스 조정할 때 코드를 안 건드려도 됩니다. 
 * 지금 당장 구현하실 필요는 없고, 나중에 Attack2/3/궁극기가 실제로 붙기 시작할 때 도입하시면 딱 맞을 구조입니다.
 */
