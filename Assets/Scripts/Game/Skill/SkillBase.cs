using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 스킬의 성질(우선순위)을 Enum으로 관리
public enum SkillPriority { Normal, Cancel, Ultimate }

// 모든 스킬의 기본이 되는 추상 클래스
public abstract class SkillBase : ScriptableObject
{
    [Header("Basic Info")]
    public string skillName;
    public int requiredMana;
    public string animStateName; // 재생할 애니메이션 State 이름 (예: "CloseAttack1")
    public float activeDuration = 0.15f; // 판정 지속 시간
    public SkillPriority priority = SkillPriority.Normal; // 캔슬 가능 여부 판단용

    // 모든 스킬이 히트박스를 가질 수 있으므로 베이스로 올림
    [Header("Hitbox Setup")]
    public Vector2 hitboxSize = new Vector2(2.6f, 1.6f);
    public Vector2 hitboxOffset = new Vector2(1.2f, 1.1f); // X는 양수로 두면 알아서 반전됨

    // 스킬마다 타격감, 전진 여부, 발사체 생성 등 완전히 다른 동작을 수행하기 위한 가상 함수
    public abstract IEnumerator ExecuteSkillBehavior(PlayerCombat combat, Rigidbody2D rb, Animator anim, CharacterStats stats);

    // 공격 판정 (히트박스) 생성 등도 스킬마다 다를 수 있으니 가상 함수로 뺌
    public abstract IEnumerator ExecuteHitbox(PlayerCombat combat, Transform parentTransform, CharacterStats stats);
}