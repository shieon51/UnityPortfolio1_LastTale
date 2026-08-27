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
        bool wantsGuard = Input.GetKey(guardKey); // ★ Down/Up 엣지 대신, 지금 눌려있는지 그 자체를 매 프레임 확인

        if (_stats.IsGroggy) // ★ 그로기 중엔 방어 자체 불가 — 강제 해제하고 입력도 무시
        {
            if (_stats.isGuarding) _stats.StopGuard();
            return;
        }

        if (!CanGuardRightNow())
        {
            if (_stats.isGuarding) // 방어 중에 뜨거나 움직이기 시작하면 자동 해제
            {
                _stats.StopGuard();
                if (_combat == null || !_combat.IsAttacking) _visual?.ReturnToLocomotion();
            }
            return;
        }

        if (_motor != null && _motor.IsActionLocked && !(_combat != null && _combat.IsAttacking))
        {
            // 완전히 잠긴 상태에서도 데이터(isGuarding)만큼은 실제 키 상태와 항상 일치시켜서, 놓친 프레임이 있어도 즉시 자가 교정
            if (_stats.isGuarding != wantsGuard)
            {
                if (wantsGuard) _stats.StartGuard(); else _stats.StopGuard();
            }
            return;
        }

        if (wantsGuard && !_stats.isGuarding)
        {
            _stats.StartGuard();
            if (_combat == null || !_combat.IsAttacking) _visual?.PlayState(ResolveContextState());
        }
        else if (!wantsGuard && _stats.isGuarding)
        {
            _stats.StopGuard();
            if (_combat == null || !_combat.IsAttacking) _visual?.ReturnToLocomotion();
        }
    }

    // 현재 가드가 가능한 상태인지
    private bool CanGuardRightNow()
    {
        if (_formProvider != null && _formProvider.IsFlightForm)
            return _motor != null && _motor.CurrentSpeed < 0.1f; // 비행형: 거의 정지 상태일 때만
        return _motor != null && _motor.IsGrounded; // 지상형: 착지 상태에서만
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