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
    private IPlayerMotor _motor;
    private IFormStageProvider _formProvider;
    private PlayerCombat _combat;

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _motor = GetComponent<IPlayerMotor>();
        _formProvider = GetComponent<IFormStageProvider>();
        _combat = GetComponentInChildren<PlayerCombat>();
    }

    private void Update()
    {
        bool wantsGuard = Input.GetKey(guardKey); // ★ Down/Up 엣지 대신, 지금 눌려있는지 그 자체를 매 프레임 확인

        // 공격 중 방어 키를 눌렀을 경우 - 콤보윈도우 끝나고 캔슬
        if (wantsGuard && _combat != null && _combat.IsAttacking && _combat.IsComboWindowOpen && !_stats.isGuarding)
        {
            _combat.CancelAttack();
            _stats.StartGuard();
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        if (_stats.IsGroggy) // ★ 그로기 중엔 방어 자체 불가 — 강제 해제하고 입력도 무시
        {
            if (_stats.isGuarding) _stats.StopGuard();
            return;
        }

        if (!CanStartGuardNow() && !_stats.isGuarding) return; // 공중(지상형)이면 시작조차 불가

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
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // ★ 이동 중이었어도 즉시 정지
            //if (_combat == null || !_combat.IsAttacking) _visual?.PlayState(ResolveContextState());
        }
        else if (!wantsGuard && _stats.isGuarding)
        {
            _stats.StopGuard();
            //if (_combat == null || !_combat.IsAttacking) _visual?.ReturnToLocomotion();
        }
    }

    // 현재 가드가 가능한 상태인지
    private bool CanStartGuardNow()
    {
        if (_formProvider != null && _formProvider.IsFlightForm) return true; // 비행형: 움직이던 중이어도 항상 가능
        return _motor != null && _motor.IsGrounded; // 지상형: 공중/점프 중엔 불가
    }

    public string ResolveContextState()
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