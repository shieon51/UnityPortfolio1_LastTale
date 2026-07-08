using System.Collections;
using UnityEngine;

// 모든 스킬의 기본이 되는 추상 클래스
public abstract class SkillBase : ScriptableObject
{
    [Header("Basic Info")]
    public string skillName;
    public int requiredMana;
    public string animStateName; // 재생할 애니메이션 State 이름 (예: "CloseAttack1")
    public float activeDuration = 0.15f; // 판정 지속 시간
    public SkillBase nextComboSkill;     // 콤보로 이어질 다음 스킬 (없으면 놔둠)

    // 스킬마다 타격감, 전진 여부, 발사체 생성 등 완전히 다른 동작을 수행하기 위한 가상 함수
    public abstract IEnumerator ExecuteSkillBehavior(PlayerCombat combat, Rigidbody2D rb, Animator anim, CharacterStats stats);

    // 공격 판정 (히트박스) 생성 등도 스킬마다 다를 수 있으니 가상 함수로 뺌
    public abstract IEnumerator ExecuteHitbox(PlayerCombat combat, Transform parentTransform, CharacterStats stats);
}