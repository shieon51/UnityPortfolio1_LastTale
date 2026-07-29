using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 스킬의 성질(우선순위)을 Enum으로 관리
public enum SkillPriority { Normal, Cancel, Ultimate }

// 플레이어 상태 (땅, 점프 후 공중, 비행모드)
public enum PlayerMovementContext { Grounded, Airborne, Flying }

// 스킬 사용에 오직 마나만 사용가능한지 or HP도 스킬마나로 땡겨쓸 수 있는지
public enum ManaCostPolicy
{
    BlockIfInsufficient, // 기본값: 마나 부족하면 발동 자체가 안 됨
    OvercastWithHealth   // 궁극기 등 특수 스킬: 부족분을 HP로 대신 소모
}

// 같은 시퀀스로 이어지는 콤보는 방향 고정, 다른 슬롯으로 전환되면 허용이 기본. (UseDefault)
public enum FacingLockOverride { UseDefault, AlwaysLock, AlwaysAllow }

[System.Serializable]
public struct SkillAnimVariant // 플레이어 상태에 따른 스킬 사용 모션 변경
{
    public PlayerMovementContext context;
    public string animStateName;

    [Header("히트박스 오버라이드 (선택)")]
    [Tooltip("체크하면 이 상황에서는 아래 오프셋/크기를 기본 hitboxOffset/hitboxSize 대신 사용함. 모션마다 팔다리 뻗는 정도가 달라 히트박스도 달라져야 할 때 사용.")]
    public bool overrideHitbox;
    public Vector2 hitboxOffsetOverride;
    public Vector2 hitboxSizeOverride;
}

// 모든 스킬의 기본이 되는 추상 클래스
public abstract class SkillBase : ScriptableObject
{
    [Header("Basic Info")]
    public string skillName;
    public int requiredMana;
    public string animStateName; // 재생할 애니메이션 State 이름 (예: "CloseAttack1")
    public float activeDuration = 0.15f; // 판정 지속 시간
    public SkillPriority priority = SkillPriority.Normal; // 캔슬 가능 여부 판단용

    [Header("Targeting")]
    [Tooltip("이 스킬의 판정이 적용될 레이어들 (플레이어 스킬 → Enemy+NPC 체크)")]
    public LayerMask targetableLayers;

    [Header("Mana")]
    public ManaCostPolicy manaCostPolicy = ManaCostPolicy.BlockIfInsufficient;

    [Header("Safety")]
    [Tooltip("OnAttackEnd 이벤트가 이 시간 안에 호출되지 않으면 강제 종료시키는 안전장치(초). 클립 전체 길이보다 넉넉하게 설정하세요.")]
    public float maxAnimationDuration = 1.2f;

    [Header("Combat Formula")]
    [Tooltip("비워두면 CombatFormulaService의 기본 수식을 사용")]
    public DamageFormulaSO customDamageFormula;

    // ** (지금은 0으로 둬도 무방, 나중에 단계 시스템 확정되면 채우면 됨)
    [Header("Progression")]
    [Tooltip("이 스킬을 쓰기 위한 최소 단계. 단계 시스템 확정 전까진 0으로 둬도 무방.")]
    public int requiredStage = 0;

    // 모든 스킬이 히트박스를 가질 수 있으므로 베이스로 올림
    [Header("Hitbox Setup")]
    public Vector2 hitboxSize = new Vector2(2.6f, 1.6f);
    public Vector2 hitboxOffset = new Vector2(1.2f, 1.1f); // X는 양수로 두면 알아서 반전됨

    [Header("Context Variants")]
    [Tooltip("공중/비행 등 특정 상황에서 다른 모션이 필요할 때만 등록. 안 하면 기본 animStateName 사용 (이펙트/판정 로직은 그대로 공유).")]
    public List<SkillAnimVariant> contextVariants = new List<SkillAnimVariant>();

    [Header("Combo Flow")]
    [Tooltip("기본(UseDefault): 같은 시퀀스로 이어지는 콤보는 고정, 다른 슬롯으로 전환되면 허용. 특정 스킬만 예외로 강제하려면 여기서 지정.")]
    public FacingLockOverride facingLockOverride = FacingLockOverride.UseDefault;

    // 콤보 윈도우가 열리는 시점에 이 스킬이 '추천'하는 바라보는 방향(월드, +1/-1). 필요 없으면 null.
    public virtual float? GetPreferredFacingDirection() => null;

    public string ResolveAnimStateName(PlayerMovementContext context)
    {
        foreach (var v in contextVariants)
            if (v.context == context) return v.animStateName;
        return animStateName;
    }

    // 컨텍스트별로 히트박스가 다를 때 사용. override 등록이 없으면 기본 hitboxOffset/hitboxSize를 반환.
    public (Vector2 offset, Vector2 size) ResolveHitbox(PlayerMovementContext context)
    {
        foreach (var v in contextVariants)
            if (v.context == context && v.overrideHitbox)
                return (v.hitboxOffsetOverride, v.hitboxSizeOverride);
        return (hitboxOffset, hitboxSize);
    }

    // 스킬이 실제로 발동 가능한 상태인지 사전 검증 (예: 유효한 타겟이 필요한 스킬 등).
    // 실패 시 failReason에 사용자에게 보여줄 안내 문구를 채우면 PlayerCombat이 자동으로 알림을 띄운다.
    public virtual bool CanExecute(PlayerCombat combat, out string failReason)
    {
        failReason = null;
        return true;
    }

    // 스킬마다 타격감, 전진 여부, 발사체 생성 등 완전히 다른 동작을 수행하기 위한 가상 함수
    public abstract IEnumerator ExecuteSkillBehavior(PlayerCombat combat, Rigidbody2D rb, Animator anim, CharacterStats stats);

    // 공격 판정 (히트박스) 생성 등도 스킬마다 다를 수 있으니 가상 함수로 뺌
    public abstract IEnumerator ExecuteHitbox(PlayerCombat combat, Transform parentTransform, CharacterStats stats);


#if UNITY_EDITOR
    // 에디터 프리뷰용 커스텀 기즈모 훅. 스킬마다 필요한 범위(타겟 탐색 반경 등)를 자유롭게 그릴 수 있음.
    public virtual void DrawEditorGizmos(Vector3 basePos, float facingDir)
    {
        foreach (var variant in contextVariants)
        {
            if (!variant.overrideHitbox) continue;

            Gizmos.color = variant.context switch
            {
                PlayerMovementContext.Airborne => new Color(0f, 0.8f, 1f, 0.6f),
                PlayerMovementContext.Flying => new Color(1f, 0.5f, 0f, 0.6f),
                _ => Color.white,
            };

            Vector2 center = (Vector2)basePos + new Vector2(variant.hitboxOffsetOverride.x * facingDir, variant.hitboxOffsetOverride.y);
            Gizmos.DrawWireCube(center, variant.hitboxSizeOverride);
        }
    }
#endif
}