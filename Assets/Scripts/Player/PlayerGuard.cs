// PlayerGuard.cs (신규)
using System.Collections.Generic;
using UnityEngine;

public class PlayerGuard : MonoBehaviour
{
    public KeyCode guardKey = KeyCode.F;

    [System.Serializable]
    public struct GuardContextVariant { public MovementContext context; public string animStateName; }
    public List<GuardContextVariant> contextVariants = new();

    private CharacterStats _stats;
    private PlayerVisual _visual;
    private IPlayerMotor _motor;
    private IFormStageProvider _formProvider;
    private PlayerCombat _combat;

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _visual = GetComponentInChildren<PlayerVisual>();
        _motor = GetComponent<IPlayerMotor>();
        _formProvider = GetComponent<IFormStageProvider>();
        _combat = GetComponentInChildren<PlayerCombat>();
    }

    private void Update()
    {
        // 조작 자체가 완전히 잠긴 경우(대화, 넉백 등)만 막고, '공격 중'은 더 이상 막지 않음
        if (_motor != null && _motor.IsActionLocked && !(_combat != null && _combat.IsAttacking)) return;

        if (Input.GetKeyDown(guardKey))
        {
            _stats.StartGuard(); // ★ 항상 반응 — 데미지 계산이 이 값만 보므로 공격 중이어도 패링/방어가 실제로 작동함
            if (_combat == null || !_combat.IsAttacking)
                _visual?.PlayState(ResolveContextState()); // 자세 전환은 공격 애니메이션과 안 겹치게 공격 중이 아닐 때만
        }
        else if (Input.GetKeyUp(guardKey))
        {
            _stats.StopGuard();
            if (_combat == null || !_combat.IsAttacking)
                _visual?.ReturnToLocomotion();
        }
    }

    private string ResolveContextState()
    {
        var context = ResolveMovementContext();
        foreach (var v in contextVariants) if (v.context == context) return v.animStateName;
        return PlayerAnimStateNames.Guard;
    }

    private MovementContext ResolveMovementContext()
    {
        if (_formProvider != null && _formProvider.IsFlightForm) return MovementContext.Flying;
        if (_motor != null && _motor.IsGrounded) return MovementContext.Grounded;
        return MovementContext.Airborne;
    }
}